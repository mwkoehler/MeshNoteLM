using MeshNoteLM.Plugins;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Mock AI provider plugin for testing AIProviderPluginBase
/// </summary>
public class MockAIProviderPlugin : AIProviderPluginBase
{
    private readonly string? _settingsApiKey;
    private readonly string? _environmentApiKey;
    private readonly List<string> _availableModels;
    private string? _lastConfiguredBaseAddress;

    public MockAIProviderPlugin(
        string? apiKey = null,
        HttpClient? httpClient = null,
        string? settingsApiKey = null,
        string? environmentApiKey = null,
        List<string>? availableModels = null)
        : base(apiKey, httpClient)
    {
        _settingsApiKey = settingsApiKey;
        _environmentApiKey = environmentApiKey;
        _availableModels = availableModels ?? new List<string> { "model-1", "model-2", "model-3" };
    }

    public override string Name => "MockAI";
    public override string Version => "1.0.0";
    public override string Description => "Mock AI provider for testing";
    public override string Author => "Test Suite";

    public string? LastConfiguredBaseAddress => _lastConfiguredBaseAddress;

    // Expose PathType for testing
    public new enum PathType
    {
        Root,
        Conversations,
        ConversationDir,
        ConversationMessage,
        Models,
        Invalid
    }

    // Expose protected methods for testing
    public (PathType type, string convId, string fileName) TestParsePath(string path)
    {
        var result = ParsePath(path);
        return ((PathType)(int)result.type, result.convId, result.fileName);
    }

    public int TestGetMessageIndex(string fileName)
    {
        return GetMessageIndex(fileName);
    }

    protected override string? GetApiKeyFromSettings()
    {
        return _settingsApiKey;
    }

    protected override string? GetApiKeyFromEnvironment()
    {
        return _environmentApiKey;
    }

    protected override async Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage)
    {
        // Simulate API response
        await Task.Delay(1);
        return $"Mock response to: {userMessage} (history: {conversationHistory.Count} messages)";
    }

    protected override IEnumerable<string> GetAvailableModels()
    {
        return _availableModels;
    }

    protected override void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new System.Uri("https://mock-api.example.com/");
        _lastConfiguredBaseAddress = _httpClient.BaseAddress.ToString();
    }
}
