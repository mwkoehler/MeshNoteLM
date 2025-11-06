/*
================================================================================
Claude (Anthropic) Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via AIProviderPluginBase

Uses Anthropic Messages API (claude-3-5-sonnet, claude-3-opus, etc.)

Virtual filesystem structure:
/conversations/                  - Root directory listing all conversations
/conversations/{conv-id}/        - Individual conversation directory
/conversations/{conv-id}/001.txt - First message in conversation
/models/                         - Directory listing available Claude models

Authentication:
- Requires ANTHROPIC_API_KEY environment variable or SettingsService.ClaudeApiKey
- API endpoint: https://api.anthropic.com/v1/messages
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeshNoteLM.Plugins;

public class ClaudePlugin : AIProviderPluginBase
{
    private const string API_BASE = "https://api.anthropic.com/v1";
    private const string API_VERSION = "2023-06-01";
    private const string DEFAULT_MODEL = "claude-3-opus-20240229";

    public override string Name => "Claude";
    public override string Version => "0.1";
    public override string Description => "Anthropic Claude AI conversations as filesystem";
    public override string Author => "Starglass Technology";

    public ClaudePlugin(string? apiKey = null) : base(apiKey)
    {
    }

    protected override string GetCredentialKey()
    {
        return "claude-api-key";
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
    }

    protected override void ConfigureHttpClient()
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", API_VERSION);
        }
    }

    protected override async Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "[Error: API key not configured]";

        try
        {
            var messages = new List<object>();
            string? systemMessage = null;

            // Process conversation history - separate system messages from conversation
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "system")
                {
                    // Claude expects system messages as a separate parameter
                    systemMessage = msg.Content;
                }
                else
                {
                    // Only add user/assistant messages to the messages array
                    messages.Add(new { role = msg.Role, content = msg.Content });
                }
            }

            // Add new user message
            messages.Add(new { role = "user", content = userMessage });

            // Build request body with system message as separate parameter if present
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = DEFAULT_MODEL,
                ["max_tokens"] = 4096,
                ["messages"] = messages
            };

            if (!string.IsNullOrEmpty(systemMessage))
            {
                requestBody["system"] = systemMessage;
                System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] System message length: {systemMessage.Length}");
            }

            var json = JsonSerializer.Serialize(requestBody);
            System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] Request body: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/messages", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] API Error ({response.StatusCode}): {errorJson}");
                return $"[API Error {response.StatusCode}: {errorJson}]";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("content", out var contentArray) &&
                contentArray.GetArrayLength() > 0)
            {
                var firstContent = contentArray[0];
                if (firstContent.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "";
                }
            }

            return "[Error: Unexpected response format]";
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] HTTP Error: {httpEx.Message}");
            return $"[HTTP Error: {httpEx.Message}]";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] Exception: {ex.GetType().Name} - {ex.Message}");
            return $"[Error: {ex.Message}]";
        }
    }

    protected override IEnumerable<string> GetAvailableModels()
    {
        yield return "claude-3-opus-20240229";
        yield return "claude-3-5-sonnet-20241022";
        yield return "claude-3-5-haiku-20241022";
        yield return "claude-3-haiku-20240307";
    }

    public override async Task<(bool Success, string Message)> TestConnectionAsync()
    {
        if (!HasValidAuthorization())
        {
            return (false, "Invalid - Missing API key");
        }

        try
        {
            // Test with a minimal API call
            var requestBody = new
            {
                model = DEFAULT_MODEL,
                max_tokens = 10,
                messages = new[] { new { role = "user", content = "Hi" } }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/messages", content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "✓ Valid - Plugin enabled");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return (false, "Invalid - Authentication failed");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Model not found - try to get available models
                var availableModels = await GetAvailableModelsFromApiAsync();
                if (!string.IsNullOrEmpty(availableModels))
                {
                    return (false, $"Error - Model '{DEFAULT_MODEL}' not found. Available models: {availableModels}");
                }
                else
                {
                    return (false, $"Error - Model '{DEFAULT_MODEL}' not found. Could not retrieve available models.");
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, $"Error - {response.StatusCode}: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error - {ex.Message}");
        }
    }

    /// <summary>
    /// Get available models from Anthropic API
    /// </summary>
    private async Task<string> GetAvailableModelsFromApiAsync()
    {
        try
        {
            // Anthropic doesn't have a public models endpoint, but we can try common working models
            var commonModels = new[]
            {
                "claude-3-5-sonnet-20241022",
                "claude-3-5-haiku-20241022",
                "claude-3-sonnet-20240229",
                "claude-3-haiku-20240307",
                "claude-3-opus-20240229"
            };

            var workingModels = new List<string>();

            // Test each model with a minimal request
            foreach (var model in commonModels)
            {
                try
                {
                    var testBody = new
                    {
                        model = model,
                        max_tokens = 5,
                        messages = new[] { new { role = "user", content = "Hi" } }
                    };

                    var json = JsonSerializer.Serialize(testBody);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync($"{API_BASE}/messages", content);

                    if (response.IsSuccessStatusCode)
                    {
                        workingModels.Add(model);
                        System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] Found working model: {model}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] Model {model} failed: {ex.Message}");
                }
            }

            return workingModels.Count > 0 ? string.Join(", ", workingModels) : "No working models found";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ClaudePlugin] Error getting models: {ex.Message}");
            return $"Error getting models: {ex.Message}";
        }
    }
}
