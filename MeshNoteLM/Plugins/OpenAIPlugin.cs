/*
================================================================================
OpenAI Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via AIProviderPluginBase

Uses OpenAI Chat Completions API (gpt-4, gpt-4o, gpt-3.5-turbo, etc.)

Virtual filesystem structure:
/conversations/                  - Root directory listing all conversations
/conversations/{conv-id}/        - Individual conversation directory
/conversations/{conv-id}/001.txt - First message in conversation
/models/                         - Directory listing available OpenAI models

Authentication:
- Requires OPENAI_API_KEY environment variable or SettingsService.OpenAIApiKey
- API endpoint: https://api.openai.com/v1
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MeshNoteLM.Plugins;

public class OpenAIPlugin : AIProviderPluginBase
{
    private const string API_BASE = "https://api.openai.com/v1";
    private const string DEFAULT_MODEL = "gpt-4o-mini";

    public override string Name => "OpenAI";
    public override string Version => "0.1";
    public override string Description => "OpenAI ChatGPT conversations as filesystem";
    public override string Author => "Starglass Technology";

    public OpenAIPlugin(string? apiKey = null) : base(apiKey)
    {
    }

    protected override string? GetApiKeyFromSettings()
    {
        var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
        return settingsService?.OpenAIApiKey;
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("OPENAI_API_KEY");
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
        yield return "gpt-4o";
        yield return "gpt-4o-mini";
        yield return "gpt-4-turbo";
        yield return "gpt-3.5-turbo";
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
                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, $"Error - {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Error - {ex.Message}");
        }
    }
}
