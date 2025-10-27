/*
================================================================================
Grok (xAI) Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via AIProviderPluginBase

Uses xAI API (grok-beta, grok-2) - OpenAI-compatible

Virtual filesystem structure:
/conversations/                  - Root directory listing all conversations
/conversations/{conv-id}/        - Individual conversation directory
/conversations/{conv-id}/001.txt - First message in conversation
/models/                         - Directory listing available Grok models

Authentication:
- Requires XAI_API_KEY environment variable or SettingsService.GrokApiKey
- API endpoint: https://api.x.ai/v1
- Uses Bearer token authentication (OpenAI-compatible)
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeshNoteLM.Plugins;

public class GrokPlugin : AIProviderPluginBase
{
    private const string API_BASE = "https://api.x.ai/v1";
    private const string DEFAULT_MODEL = "grok-beta";

    public override string Name => "Grok";
    public override string Version => "0.1";
    public override string Description => "xAI Grok conversations as filesystem";
    public override string Author => "Starglass Technology";

    public GrokPlugin(string? apiKey = null) : base(apiKey)
    {
    }

    protected override string? GetApiKeyFromSettings()
    {
        var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
        return settingsService?.GrokApiKey;
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("XAI_API_KEY");
    }

    protected override void ConfigureHttpClient()
    {
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
    }

    protected override async Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "[Error: API key not configured]";

        try
        {
            var messages = new List<object>();

            foreach (var msg in conversationHistory)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            messages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model = DEFAULT_MODEL,
                messages = messages
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var text))
                {
                    return text.GetString() ?? "";
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
        yield return "grok-beta";
        yield return "grok-2";
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

            var response = await _httpClient.PostAsync($"{API_BASE}/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                return (true, "✓ Valid - Plugin enabled");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                     response.StatusCode == System.Net.HttpStatusCode.Forbidden)
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
