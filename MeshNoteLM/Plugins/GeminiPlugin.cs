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
    private const string API_BASE = "https://generativelanguage.googleapis.com/v1beta";
    private const string DEFAULT_MODEL = "gemini-1.5-flash-latest";

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
        // Gemini uses query parameter authentication, so no headers needed
    }

    protected override async Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "[Error: API key not configured]";

        try
        {
            var contents = new List<object>();

            // Add conversation history
            foreach (var msg in conversationHistory)
            {
                contents.Add(new
                {
                    role = msg.Role,
                    parts = new[] { new { text = msg.Content } }
                });
            }

            // Add new user message
            contents.Add(new
            {
                role = "user",
                parts = new[] { new { text = userMessage } }
            });

            var requestBody = new
            {
                contents = contents
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var url = $"{API_BASE}/models/{DEFAULT_MODEL}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

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
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    protected override IEnumerable<string> GetAvailableModels()
    {
        yield return "gemini-1.5-pro-latest";
        yield return "gemini-1.5-flash-latest";
        yield return "gemini-pro";
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
            else
            {
                return (false, $"Error - {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error - {ex.Message}");
        }
    }
}
