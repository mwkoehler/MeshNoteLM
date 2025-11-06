/*
================================================================================
Google Gemini Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via AIProviderPluginBase

Uses Google Generative AI API (gemini-1.5-pro, gemini-1.5-flash, etc.)

Virtual filesystem structure:
/conversations/                  - Root directory listing all conversations
/conversations/{conv-id}/        - Individual conversation directory
/conversations/{conv-id}/001.txt - First message in conversation
/models/                         - Directory listing available Gemini models

Authentication:
- Requires GOOGLE_API_KEY environment variable or SettingsService.GeminiApiKey
- API endpoint: https://generativelanguage.googleapis.com/v1beta
- Uses ?key={apiKey} query parameter for authentication
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeshNoteLM.Plugins;

public class GeminiPlugin : AIProviderPluginBase
{
    private const string API_BASE = "https://generativelanguage.googleapis.com/v1";
    private const string DEFAULT_MODEL = "gemini-2.0-flash";

    public override string Name => "Gemini";
    public override string Version => "0.1";
    public override string Description => "Google Gemini AI conversations as filesystem";
    public override string Author => "Starglass Technology";

    public GeminiPlugin(string? apiKey = null) : base(apiKey)
    {
    }

    protected override string? GetApiKeyFromSettings()
    {
        var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
        return settingsService?.GeminiApiKey;
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
    }

    protected override void ConfigureHttpClient()
    {
        // Clear any existing headers to ensure clean state
        _httpClient.DefaultRequestHeaders.Clear();
        // Gemini uses query parameter authentication, no headers needed
    }

    protected override async Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "[Error: API key not configured]";

        try
        {
            var contents = new List<object>();

            // Process conversation history
            foreach (var msg in conversationHistory)
            {
                if (msg.Role == "system")
                {
                    // Add system message as the first content item with "user" role (Gemini workaround)
                    contents.Add(new
                    {
                        role = "user",
                        parts = new[] { new { text = $"System: {msg.Content}" } }
                    });
                }
                else
                {
                    // Add user/assistant messages normally
                    contents.Add(new
                    {
                        role = msg.Role,
                        parts = new[] { new { text = msg.Content } }
                    });
                }
            }

            // Add new user message
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            // Build request body (no systemInstruction - using content-based approach)
            var requestBody = new Dictionary<string, object>
            {
                ["contents"] = contents
            };

            System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] Using content-based system message approach");
            System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] Total content items: {contents.Count}");

            var json = JsonSerializer.Serialize(requestBody);
            System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] Final request body: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{API_BASE}/models/{DEFAULT_MODEL}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] API Error ({response.StatusCode}): {errorJson}");
                return $"[API Error {response.StatusCode}: {errorJson}]";
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var responseContent) &&
                    responseContent.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }
            }

            return "[Error: Unexpected response format]";
        }
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] HTTP Error: {httpEx.Message}");
            return $"[HTTP Error: {httpEx.Message}]";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GeminiPlugin] Exception: {ex.GetType().Name} - {ex.Message}");
            return $"[Error: {ex.Message}]";
        }
    }

    protected override IEnumerable<string> GetAvailableModels()
    {
        // Models available from ListModels API call
        yield return "gemini-2.5-flash";
        yield return "gemini-2.5-pro";
        yield return "gemini-2.0-flash";
        yield return "gemini-2.0-flash-001";
        yield return "gemini-2.0-flash-lite-001";
        yield return "gemini-2.0-flash-lite";
        yield return "gemini-2.5-flash-lite";
        // Note: embedding models are for embeddings, not chat
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
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = "Hi" } }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{API_BASE}/models/{DEFAULT_MODEL}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "✓ Valid - Plugin enabled");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden ||
                     response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                return (false, "Invalid - Authentication failed");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                var availableModels = await ListAvailableModelsAsync();
                return (false, $"Error - {response.StatusCode}: Model '{DEFAULT_MODEL}' not found. Available models for your API key: {availableModels}");
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

    private async Task<string> ListAvailableModelsAsync()
    {
        try
        {
            var listModelsUrl = $"{API_BASE}/models?key={_apiKey}";
            var response = await _httpClient.GetAsync(listModelsUrl);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);

                var models = new List<string>();
                if (doc.RootElement.TryGetProperty("models", out var modelsArray))
                {
                    foreach (var model in modelsArray.EnumerateArray())
                    {
                        if (model.TryGetProperty("name", out var nameProp))
                        {
                            var fullName = nameProp.GetString() ?? "";
                            // Extract just the model name from the full path (e.g., "models/gemini-pro" -> "gemini-pro")
                            var modelName = fullName.Replace("models/", "");
                            models.Add(modelName);
                        }
                    }
                }

                return models.Count > 0 ? string.Join(", ", models) : "No models found";
            }
            else
            {
                return $"Failed to list models: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            return $"Error listing models: {ex.Message}";
        }
    }
}
