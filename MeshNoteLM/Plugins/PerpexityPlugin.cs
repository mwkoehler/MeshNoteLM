/*
================================================================================
Perplexity AI Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via AIProviderPluginBase

Uses Perplexity API (sonar-pro, sonar) with web search capabilities

Virtual filesystem structure:
/conversations/                  - Root directory listing all conversations
/conversations/{conv-id}/        - Individual conversation directory
/conversations/{conv-id}/001.txt - First message in conversation
/models/                         - Directory listing available Perplexity models

Authentication:
- Requires PPLX_API_KEY environment variable or SettingsService.PerplexityApiKey
- API endpoint: https://api.perplexity.ai
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

public class PerpexityPlugin : AIProviderPluginBase
{
    private const string API_BASE = "https://api.perplexity.ai";
    private const string DEFAULT_MODEL = "sonar-pro";

    public override string Name => "Perplexity";
    public override string Version => "0.1";
    public override string Description => "Perplexity AI conversations as filesystem";
    public override string Author => "Starglass Technology";

    public PerpexityPlugin(string? apiKey = null) : base(apiKey)
    {
    }

    protected override string? GetApiKeyFromSettings()
    {
        var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
        return settingsService?.PerplexityApiKey;
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return Environment.GetEnvironmentVariable("PPLX_API_KEY");
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
        yield return "sonar-pro";
        yield return "sonar";
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
    /// Get available models from Perplexity API
    /// </summary>
    private async Task<string> GetAvailableModelsFromApiAsync()
    {
        try
        {
            // Try to get models from Perplexity API (OpenAI-compatible)
            var response = await _httpClient.GetAsync($"{API_BASE}/models");
            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);

                var chatModels = new List<string>();

                if (doc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    foreach (var modelElement in dataArray.EnumerateArray())
                    {
                        if (modelElement.TryGetProperty("id", out var idProp))
                        {
                            var modelId = idProp.GetString() ?? "";
                            // Filter for Perplexity models
                            if (modelId.StartsWith("sonar-") || modelId == "sonar")
                            {
                                chatModels.Add(modelId);
                            }
                        }
                    }
                }

                if (chatModels.Count > 0)
                {
                    return string.Join(", ", chatModels);
                }
            }

            // Fallback to testing common Perplexity models
            return await TestCommonPerplexityModelsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerplexityPlugin] Error getting models from API: {ex.Message}");
            // Fallback to testing common models
            return await TestCommonPerplexityModelsAsync();
        }
    }

    /// <summary>
    /// Test common Perplexity models as fallback
    /// </summary>
    private async Task<string> TestCommonPerplexityModelsAsync()
    {
        try
        {
            var commonModels = new[]
            {
                "sonar-pro",
                "sonar",
                "sonar-reasoning",
                "sonar-reasoning-pro"
            };

            var workingModels = new List<string>();

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
                    var response = await _httpClient.PostAsync($"{API_BASE}/chat/completions", content);

                    if (response.IsSuccessStatusCode)
                    {
                        workingModels.Add(model);
                        System.Diagnostics.Debug.WriteLine($"[PerplexityPlugin] Found working model: {model}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PerplexityPlugin] Model {model} failed: {ex.Message}");
                }
            }

            return workingModels.Count > 0 ? string.Join(", ", workingModels) : "No working models found";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PerplexityPlugin] Error testing common models: {ex.Message}");
            return $"Error testing models: {ex.Message}";
        }
    }
}
