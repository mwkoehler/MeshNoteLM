using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MeshNoteLM.Interfaces
{
    // ---- Core Interfaces -----------------------------------------------------

    /// <summary>
    /// High-level entry point for any LLM provider. Create chats and ask questions.
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// Create a new chat session with optional system prompt and context.
        /// </summary>
        Task<IChatSession> CreateChatAsync(
            ChatOptions options,
            CancellationToken ct = default);

        /// <summary>
        /// Convenience one-shot: send a prompt with optional context and receive a full response.
        /// </summary>
        Task<LlmResponse> AskAsync(
            string prompt,
            ChatOptions? options = null,
            CancellationToken ct = default);

        /// <summary>
        /// Get a list of available model identifiers for this provider.
        /// </summary>
        Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Represents a stateful multi-turn chat with an LLM.
    /// </summary>
    public interface IChatSession : IAsyncDisposable
    {
        string Id { get; }
        ChatOptions Options { get; }
        IReadOnlyList<ChatMessage> History { get; }

        /// <summary>
        /// Send a user message and receive a complete assistant response.
        /// </summary>
        Task<LlmResponse> SendAsync(
            string userMessage,
            CancellationToken ct = default);

        /// <summary>
        /// Stream tokens from the assistant response as they are generated.
        /// Caller can stitch tokens or handle partials as desired.
        /// </summary>
        IAsyncEnumerable<LlmStreamChunk> StreamAsync(
            string userMessage,
            CancellationToken ct = default);
    }

    // ---- Options, Messages, Results -----------------------------------------

    /// <summary>
    /// Minimal, provider-agnostic options for controlling behavior and context.
    /// </summary>
    public sealed class ChatOptions
    {
        /// <summary>Provider-specific model ID (e.g., "gpt-4.1", "claude-3.5-sonnet").</summary>
        public string? Model { get; init; }

        /// <summary>Optional system instruction to steer behavior.</summary>
        public string? SystemPrompt { get; init; }

        /// <summary>Arbitrary context variables (e.g., user profile, app state).</summary>
        public IReadOnlyDictionary<string, string>? Context { get; init; }

        /// <summary>Max new tokens to generate (null = provider default).</summary>
        public int? MaxOutputTokens { get; init; }

        /// <summary>Sampling temperature (0–2 range typical; null = provider default).</summary>
        public float? Temperature { get; init; }

        /// <summary>Nucleus sampling cutoff (0–1; null = provider default).</summary>
        public float? TopP { get; init; }

        /// <summary>Arbitrary provider overrides (advanced/experimental).</summary>
        public IReadOnlyDictionary<string, object?> ProviderOverrides { get; init; } = new Dictionary<string, object?>();
    }

    public enum ChatRole { System, User, Assistant, Tool }

    public sealed class ChatMessage
    {
        public ChatRole Role { get; init; }
        public string Content { get; init; } = "";
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

        public static ChatMessage System(string content) => new() { Role = ChatRole.System, Content = content };
        public static ChatMessage User(string content)   => new() { Role = ChatRole.User, Content = content };
        public static ChatMessage Assistant(string content) => new() { Role = ChatRole.Assistant, Content = content };
        public static ChatMessage Tool(string content)   => new() { Role = ChatRole.Tool, Content = content };
    }

    public sealed class LlmResponse
    {
        public string Text { get; init; } = "";
        public LlmFinishReason FinishReason { get; init; } = LlmFinishReason.Completed;
        public TokenUsage? Usage { get; init; }
        public IReadOnlyDictionary<string, object>? ProviderMetadata { get; init; }
    }

    public readonly struct LlmStreamChunk(string textDelta, bool isFinal = false)
    {
        public string TextDelta { get; } = textDelta;
        public bool IsFinal { get; } = isFinal;
    }

    public enum LlmFinishReason { Completed, Length, ContentFiltered, ToolCall, Stopped, Error }

    public sealed class TokenUsage
    {
        public int? PromptTokens { get; init; }
        public int? CompletionTokens { get; init; }
        public int? TotalTokens => (PromptTokens ?? 0) + (CompletionTokens ?? 0);
    }

    // ---- Optional: Simple Provider Factory ----------------------------------

    public interface ILlmClientFactory
    {
        ILLMClient Create(string providerName); // e.g., "openai", "anthropic", "azure"
    }

    // ---- Example Adapter Stubs (Illustrative Only) --------------------------

    /// <summary>
    /// Example: OpenAI adapter (backed by OpenAI/ Azure OpenAI SDK or HTTP).
    /// Replace the bodies with real API calls.
    /// </summary>
    public sealed class OpenAiClient(string apiKey, string? baseUrl = null) : ILLMClient
    {
        private readonly string _apiKey = apiKey;
        private readonly Uri _baseUri = new(baseUrl ?? "https://api.openai.com/v1/");

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IChatSession> CreateChatAsync(ChatOptions options, CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            => new OpenAiChatSession(_apiKey, _baseUri, options);

        public async Task<LlmResponse> AskAsync(string prompt, ChatOptions? options = null, CancellationToken ct = default)
        {
            var chat = await CreateChatAsync(options ?? new ChatOptions(), ct);
            try { return await chat.SendAsync(prompt, ct); }
            finally { await chat.DisposeAsync(); }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            // Call provider /models endpoint; return IDs.
            return ["gpt-4.1", "gpt-4o-mini"];
        }

        private sealed class OpenAiChatSession : IChatSession
        {
            public string Id { get; } = Guid.NewGuid().ToString("n");
            public ChatOptions Options { get; }
            public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

            private readonly string _apiKey;
            private readonly Uri _baseUri;
            private readonly List<ChatMessage> _history = [];

            public OpenAiChatSession(string apiKey, Uri baseUri, ChatOptions options)
            {
                _apiKey = apiKey;
                _baseUri = baseUri;
                Options = options;

                if (!string.IsNullOrEmpty(options.SystemPrompt))
                    _history.Add(ChatMessage.System(options.SystemPrompt));
                if (options.Context is not null && options.Context.Count > 0)
                    _history.Add(ChatMessage.System($"Context: {SerializeContext(options.Context)}"));
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            public async Task<LlmResponse> SendAsync(string userMessage, CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            {
                _history.Add(ChatMessage.User(userMessage));

                // Map Options + History -> provider payload, call chat.completions
                // Parse response into LlmResponse.
                // This stub just echoes.
                var text = $"[OpenAI:{Options.Model ?? "default"}] {userMessage}";
                var resp = new LlmResponse
                {
                    Text = text,
                    FinishReason = LlmFinishReason.Completed,
                    Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 1 },
                    ProviderMetadata = new Dictionary<string, object> { ["mock"] = true }
                };
                _history.Add(ChatMessage.Assistant(resp.Text));
                return resp;
            }

            public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
            {
                // Real impl: call streaming endpoint; yield deltas.
                foreach (var piece in new[] { "Streaming ", "response ", "from ", "OpenAI." })
                {
                    yield return new LlmStreamChunk(piece);
                    await Task.Delay(20, ct);
                }
                yield return new LlmStreamChunk("", isFinal: true);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            private static string SerializeContext(IReadOnlyDictionary<string, string> ctx)
                => string.Join("; ", ctx);
        }
    }

    /// <summary>
    /// Example: Anthropic adapter stub.
    /// </summary>
    public sealed class AnthropicClient(string apiKey) : ILLMClient
    {
        private readonly string _apiKey = apiKey;

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IChatSession> CreateChatAsync(ChatOptions options, CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            => new AnthropicChatSession(_apiKey, options);

        public async Task<LlmResponse> AskAsync(string prompt, ChatOptions? options = null, CancellationToken ct = default)
        {
            var chat = await CreateChatAsync(options ?? new ChatOptions(), ct);
            try { return await chat.SendAsync(prompt, ct); }
            finally { await chat.DisposeAsync(); }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            => ["claude-3.5-sonnet", "claude-3-haiku"];

        private sealed class AnthropicChatSession : IChatSession
        {
            public string Id { get; } = Guid.NewGuid().ToString("n");
            public ChatOptions Options { get; }
            public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();
            private readonly string _apiKey;
            private readonly List<ChatMessage> _history = [];

            public AnthropicChatSession(string apiKey, ChatOptions options)
            {
                _apiKey = apiKey;
                Options = options;
                if (!string.IsNullOrEmpty(options.SystemPrompt))
                    _history.Add(ChatMessage.System(options.SystemPrompt));
                if (options.Context is not null && options.Context.Count > 0)
                    _history.Add(ChatMessage.System($"Context: {string.Join(" | ", options.Context)}"));
            }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
            public async Task<LlmResponse> SendAsync(string userMessage, CancellationToken ct = default)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            {
                _history.Add(ChatMessage.User(userMessage));
                var text = $"[Anthropic:{Options.Model ?? "default"}] {userMessage}";
                var resp = new LlmResponse
                {
                    Text = text,
                    FinishReason = LlmFinishReason.Completed,
                    Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 1 },
                    ProviderMetadata = new Dictionary<string, object> { ["mock"] = true }
                };
                _history.Add(ChatMessage.Assistant(resp.Text));
                return resp;
            }

            public async IAsyncEnumerable<LlmStreamChunk> StreamAsync(string userMessage, [EnumeratorCancellation] CancellationToken ct = default)
            {
                foreach (var piece in new[] { "Streaming ", "response ", "from ", "Anthropic." })
                {
                    yield return new LlmStreamChunk(piece);
                    await Task.Delay(20, ct);
                }
                yield return new LlmStreamChunk("", isFinal: true);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    // ---- Example Usage -------------------------------------------------------

    public static class Example
    {
        public static async Task DemoAsync()
        {
            ILLMClient client = new OpenAiClient(apiKey: "OPENAI_KEY");
            // Swap providers in one line:
            // ILlmClient client = new AnthropicClient(apiKey: "ANTHROPIC_KEY");

            var options = new ChatOptions
            {
                Model = "gpt-4.1",
                SystemPrompt = "You are a concise assistant.",
                Context = new Dictionary<string, string> {
                    ["userTier"] = "pro",
                    ["locale"] = "en-US"
                },
                Temperature = 0.3f
            };

            // One-shot
            var answer = await client.AskAsync("Summarize the key idea of transformers.", options);
            Console.WriteLine(answer.Text);

            // Multi-turn
            await using var chat = await client.CreateChatAsync(options);
            var r1 = await chat.SendAsync("Explain RAG in two sentences.");
            Console.WriteLine(r1.Text);

            await foreach (var chunk in chat.StreamAsync("Now stream the first sentence only."))
                Console.Write(chunk.TextDelta);
        }
    }
}
