using MeshNoteLM.Services;
using MeshNoteLM.Tests.Mocks;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly TestFileSystemService _fileSystem;
    private readonly string _settingsFilePath;

    public SettingsServiceTests()
    {
        _fileSystem = new TestFileSystemService();
        _settingsFilePath = Path.Combine(_fileSystem.AppDataDirectory, "settings.json");
    }

    [Fact]
    public void Constructor_ShouldCreateEmptySettings_WhenNoFileExists()
    {
        // Act
        var settings = new SettingsService(_fileSystem);

        // Assert
        settings.ClaudeApiKey.Should().BeNull();
        settings.OpenAIApiKey.Should().BeNull();
        settings.ObsidianVaultPath.Should().BeNull();
    }

    [Fact]
    public void SetProperty_ShouldSaveToFile()
    {
        // Arrange
        var settings = new SettingsService(_fileSystem)
        {
            // Act
            ClaudeApiKey = "test-api-key"
        };

        // Assert
        File.Exists(_settingsFilePath).Should().BeTrue();
        var fileContent = File.ReadAllText(_settingsFilePath);
        fileContent.Should().Contain("test-api-key");
    }

    [Fact]
    public void MultipleInstances_ShouldShareSameFile()
    {
        // Arrange
        var settings1 = new SettingsService(_fileSystem)
        {
            OpenAIApiKey = "openai-key"
        };

        // Act
        var settings2 = new SettingsService(_fileSystem);

        // Assert
        settings2.OpenAIApiKey.Should().Be("openai-key");
    }

    [Fact]
    public void AllApiKeys_ShouldPersistCorrectly()
    {
        // Arrange
        var settings1 = new SettingsService(_fileSystem)
        {
            // Act
            ClaudeApiKey = "claude-key",
            OpenAIApiKey = "openai-key",
            GeminiApiKey = "gemini-key",
            GrokApiKey = "grok-key",
            MetaApiKey = "meta-key",
            MistralApiKey = "mistral-key",
            PerplexityApiKey = "perplexity-key"
        };

        // Assert - Load in new instance
        var settings2 = new SettingsService(_fileSystem);
        settings2.ClaudeApiKey.Should().Be("claude-key");
        settings2.OpenAIApiKey.Should().Be("openai-key");
        settings2.GeminiApiKey.Should().Be("gemini-key");
        settings2.GrokApiKey.Should().Be("grok-key");
        settings2.MetaApiKey.Should().Be("meta-key");
        settings2.MistralApiKey.Should().Be("mistral-key");
        settings2.PerplexityApiKey.Should().Be("perplexity-key");
    }

    [Fact]
    public void ServiceCredentials_ShouldPersistCorrectly()
    {
        // Arrange
        var settings1 = new SettingsService(_fileSystem)
        {
            // Act
            NotionApiKey = "notion-key",
            GoogleDriveCredentialsPath = "/path/to/creds",
            RedditClientId = "reddit-id",
            RedditClientSecret = "reddit-secret",
            RedditRefreshToken = "reddit-token",
            ReaderApiKey = "reader-key"
        };

        // Assert - Load in new instance
        var settings2 = new SettingsService(_fileSystem);
        settings2.NotionApiKey.Should().Be("notion-key");
        settings2.GoogleDriveCredentialsPath.Should().Be("/path/to/creds");
        settings2.RedditClientId.Should().Be("reddit-id");
        settings2.RedditClientSecret.Should().Be("reddit-secret");
        settings2.RedditRefreshToken.Should().Be("reddit-token");
        settings2.ReaderApiKey.Should().Be("reader-key");
    }

    [Fact]
    public void ObsidianVaultPath_ShouldPersistCorrectly()
    {
        // Arrange
        var settings1 = new SettingsService(_fileSystem)
        {
            // Act
            ObsidianVaultPath = "/path/to/vault"
        };

        // Assert - Load in new instance
        var settings2 = new SettingsService(_fileSystem);
        settings2.ObsidianVaultPath.Should().Be("/path/to/vault");
    }

    [Fact]
    public void SetProperty_ToNull_ShouldPersist()
    {
        // Arrange
        var settings1 = new SettingsService(_fileSystem)
        {
            ClaudeApiKey = "original-key"
        };

        // Act
        settings1.ClaudeApiKey = null;

        // Assert - Load in new instance
        var settings2 = new SettingsService(_fileSystem);
        settings2.ClaudeApiKey.Should().BeNull();
    }

    [Fact]
    public void Save_ShouldCreateFormattedJson()
    {
        // Arrange
        var settings = new SettingsService(_fileSystem)
        {
            ClaudeApiKey = "test-key"
        };

        // Act - Save is called automatically by property setter

        // Assert
        var fileContent = File.ReadAllText(_settingsFilePath);
        fileContent.Should().Contain("ClaudeApiKey");
        fileContent.Should().Contain("test-key");
        // Should be indented (WriteIndented = true)
        fileContent.Should().Contain("\n");
    }

    public void Dispose()
    {
        _fileSystem?.Dispose();
    }
}
