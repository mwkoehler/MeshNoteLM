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
            _httpClient.DefaultRequestHeaders.Clear();
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

            // Process conversation history - include system messages in the array
            foreach (var msg in conversationHistory)
            {
                // Add all messages including system messages to the messages array
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            messages.Add(new { role = "user", content = userMessage });

            // Build request body
            var requestBody = new Dictionary<string, object>
            {
                ["model"] = DEFAULT_MODEL,
                ["messages"] = messages
            };

            // Count system messages for debugging
            var systemMessageCount = conversationHistory.Count(m => m.Role == "system");
            if (systemMessageCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Added {systemMessageCount} system messages to messages array");
            }

            var json = JsonSerializer.Serialize(requestBody);
            System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Request body: {json}");
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] API Error ({response.StatusCode}): {errorJson}");
                return $"[API Error {response.StatusCode}: {errorJson}]";
            }

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
        catch (HttpRequestException httpEx)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] HTTP Error: {httpEx.Message}");
            return $"[HTTP Error: {httpEx.Message}]";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Exception: {ex.GetType().Name} - {ex.Message}");
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
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Model not found - try to get available models from OpenAI API
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
    /// Get available models from OpenAI API
    /// </summary>
    private async Task<string> GetAvailableModelsFromApiAsync()
    {
        try
        {
            // OpenAI has a models endpoint
            var response = await _httpClient.GetAsync($"{API_BASE}/models");
            if (!response.IsSuccessStatusCode)
            {
                // Fallback to testing common models
                return await TestCommonModelsAsync();
            }

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
                        // Filter for chat completion models
                        if (modelId.StartsWith("gpt-") &&
                            (modelId.Contains("chat") || modelId.Contains("instruct") ||
                             modelId == "gpt-4" || modelId == "gpt-4o" || modelId == "gpt-4-turbo" ||
                             modelId.StartsWith("gpt-3.5") || modelId.StartsWith("gpt-4")))
                        {
                            chatModels.Add(modelId);
                        }
                    }
                }
            }

            if (chatModels.Count > 0)
            {
                // Prioritize current models
                var priorityModels = new[] { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo" };
                var orderedModels = priorityModels.Where(m => chatModels.Contains(m))
                                               .Concat(chatModels.Where(m => !priorityModels.Contains(m)));
                return string.Join(", ", orderedModels.Take(10)); // Limit to 10 models
            }

            return "No chat models found";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Error getting models from API: {ex.Message}");
            // Fallback to testing common models
            return await TestCommonModelsAsync();
        }
    }

    /// <summary>
    /// Test common models as fallback
    /// </summary>
    private async Task<string> TestCommonModelsAsync()
    {
        try
        {
            var commonModels = new[]
            {
                "gpt-4o",
                "gpt-4o-mini",
                "gpt-4-turbo",
                "gpt-4",
                "gpt-3.5-turbo"
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
                        System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Found working model: {model}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Model {model} failed: {ex.Message}");
                }
            }

            return workingModels.Count > 0 ? string.Join(", ", workingModels) : "No working models found";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpenAIPlugin] Error testing common models: {ex.Message}");
            return $"Error testing models: {ex.Message}";
        }
    }
}
