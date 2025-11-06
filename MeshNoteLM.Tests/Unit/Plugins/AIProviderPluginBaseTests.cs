using MeshNoteLM.Tests.Mocks;
using FluentAssertions;
using System.Net.Http;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Plugins;

public class AIProviderPluginBaseTests : IDisposable
{
    private readonly MockAIProviderPlugin _plugin;
    private readonly HttpClient _httpClient;

    public AIProviderPluginBaseTests()
    {
        _httpClient = new HttpClient();
        _plugin = new MockAIProviderPlugin("test-api-key", _httpClient);
    }

    [Fact]
    public void Constructor_ShouldConfigureHttpClient()
    {
        // Assert
        _plugin.LastConfiguredBaseAddress.Should().Be("https://mock-api.example.com/");
    }

    [Fact]
    public void Constructor_ShouldResolveApiKey_FromProvidedKey()
    {
        // Arrange & Act
        var plugin = new MockAIProviderPlugin("provided-key");

        // Assert
        plugin.HasValidAuthorization().Should().BeTrue();
    }

    [Fact]
    public void Constructor_ShouldAcceptHttpClient()
    {
        // Arrange
        var httpClient = new HttpClient();

        // Act
        var plugin = new MockAIProviderPlugin(
            apiKey: "test-key",
            httpClient: httpClient);

        // Assert
        plugin.Should().NotBeNull();
        plugin.HasValidAuthorization().Should().BeTrue();

        // Cleanup
        plugin.Dispose();
    }

    [Fact]
    public void Constructor_ShouldCreateDefaultHttpClient_WhenNoneProvided()
    {
        // Act
        var plugin = new MockAIProviderPlugin(apiKey: "test-key");

        // Assert
        plugin.Should().NotBeNull();
        plugin.LastConfiguredBaseAddress.Should().NotBeNullOrEmpty();

        // Cleanup
        plugin.Dispose();
    }

    [Fact]
    public void HasValidAuthorization_ShouldReturnFalse_WhenNoApiKey()
    {
        // Arrange
        var httpClient = new HttpClient();
        var plugin = new MockAIProviderPlugin(
            apiKey: null,
            httpClient: httpClient);

        // Act & Assert
        plugin.HasValidAuthorization().Should().BeFalse();

        // Cleanup
        plugin.Dispose();
    }

    [Fact]
    public void ParsePath_ShouldParseRootPath()
    {
        // Act
        var result = _plugin.TestParsePath("");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.Root);
        result.convId.Should().BeEmpty();
        result.fileName.Should().BeEmpty();
    }

    [Fact]
    public void ParsePath_ShouldParseConversationsPath()
    {
        // Act
        var result = _plugin.TestParsePath("conversations");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.Conversations);
    }

    [Fact]
    public void ParsePath_ShouldParseConversationDirPath()
    {
        // Act
        var result = _plugin.TestParsePath("conversations/conv-123");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.ConversationDir);
        result.convId.Should().Be("conv-123");
    }

    [Fact]
    public void ParsePath_ShouldParseConversationMessagePath()
    {
        // Act
        var result = _plugin.TestParsePath("conversations/conv-123/001.txt");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.ConversationMessage);
        result.convId.Should().Be("conv-123");
        result.fileName.Should().Be("001.txt");
    }

    [Fact]
    public void ParsePath_ShouldParseModelsPath()
    {
        // Act
        var result = _plugin.TestParsePath("models");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.Models);
    }

    [Fact]
    public void ParsePath_ShouldHandleBackslashes()
    {
        // Act
        var result = _plugin.TestParsePath("conversations\\conv-123\\001.txt");

        // Assert
        result.type.Should().Be(MockAIProviderPlugin.PathType.ConversationMessage);
        result.convId.Should().Be("conv-123");
        result.fileName.Should().Be("001.txt");
    }

    [Fact]
    public void GetMessageIndex_ShouldConvertFromOneBasedToZeroBased()
    {
        // Act
        var index = _plugin.TestGetMessageIndex("001.txt");

        // Assert
        index.Should().Be(0);
    }

    [Fact]
    public void GetMessageIndex_ShouldHandleDoubleDigits()
    {
        // Act
        var index = _plugin.TestGetMessageIndex("042.txt");

        // Assert
        index.Should().Be(41);
    }

    [Fact]
    public void GetMessageIndex_ShouldReturnNegativeOne_ForInvalidFileName()
    {
        // Act
        var index = _plugin.TestGetMessageIndex("invalid.txt");

        // Assert
        index.Should().Be(-1);
    }

    [Fact]
    public void DirectoryExists_ShouldReturnTrue_ForRoot()
    {
        // Act
        var exists = _plugin.DirectoryExists("");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_ShouldReturnTrue_ForConversations()
    {
        // Act
        var exists = _plugin.DirectoryExists("conversations");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_ShouldReturnTrue_ForModels()
    {
        // Act
        var exists = _plugin.DirectoryExists("models");

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_ShouldReturnFalse_ForNonexistentConversation()
    {
        // Act
        var exists = _plugin.DirectoryExists("conversations/nonexistent");

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void CreateDirectory_ShouldCreateNewConversation()
    {
        // Act
        _plugin.CreateDirectory("conversations/new-conv");

        // Assert
        _plugin.DirectoryExists("conversations/new-conv").Should().BeTrue();
    }

    [Fact]
    public void DeleteDirectory_ShouldRemoveConversation()
    {
        // Arrange
        _plugin.CreateDirectory("conversations/test-conv");

        // Act
        _plugin.DeleteDirectory("conversations/test-conv");

        // Assert
        _plugin.DirectoryExists("conversations/test-conv").Should().BeFalse();
    }

    [Fact]
    public void GetDirectories_ShouldReturnConversationsAndModels_FromRoot()
    {
        // Act
        var directories = _plugin.GetDirectories("").ToList();

        // Assert
        directories.Should().Contain("conversations");
        directories.Should().Contain("models");
        directories.Should().HaveCount(2);
    }

    [Fact]
    public void GetDirectories_ShouldReturnAllConversations()
    {
        // Arrange
        _plugin.CreateDirectory("conversations/conv-1");
        _plugin.CreateDirectory("conversations/conv-2");

        // Act
        var directories = _plugin.GetDirectories("conversations").ToList();

        // Assert
        directories.Should().Contain("conversations/conv-1");
        directories.Should().Contain("conversations/conv-2");
        directories.Should().HaveCount(2);
    }

    [Fact]
    public void GetFiles_ShouldReturnAvailableModels_FromModelsDirectory()
    {
        // Act
        var files = _plugin.GetFiles("models").ToList();

        // Assert
        files.Should().Contain("models/model-1.txt");
        files.Should().Contain("models/model-2.txt");
        files.Should().Contain("models/model-3.txt");
        files.Should().HaveCount(3);
    }

    [Fact]
    public async Task WriteFile_ShouldCreateConversationMessage()
    {
        // Arrange
        var convId = "test-conversation";
        _plugin.CreateDirectory($"conversations/{convId}");

        // Act
        _plugin.WriteFile($"conversations/{convId}/001.txt", "Hello, AI!");

        // Assert
        _plugin.FileExists($"conversations/{convId}/001.txt").Should().BeTrue();
        _plugin.FileExists($"conversations/{convId}/002.txt").Should().BeTrue(); // AI response
    }

    [Fact]
    public void WriteFile_ShouldCreateNewConversation_WhenConvIdIsNew()
    {
        // Act
        _plugin.WriteFile("conversations/new/001.txt", "Start new conversation");

        // Assert - A new conversation should be created (with GUID as ID)
        var conversations = _plugin.GetDirectories("conversations").ToList();
        conversations.Should().HaveCount(1);
    }

    [Fact]
    public void ReadFile_ShouldReturnMessageContent()
    {
        // Arrange
        var convId = "read-test";
        _plugin.CreateDirectory($"conversations/{convId}");
        _plugin.WriteFile($"conversations/{convId}/001.txt", "Test message");

        // Act
        var content = _plugin.ReadFile($"conversations/{convId}/001.txt");

        // Assert
        content.Should().Contain("user:");
        content.Should().Contain("Test message");
    }

    [Fact]
    public void ReadFile_ShouldThrowFileNotFoundException_WhenFileDoesNotExist()
    {
        // Act
        var act = () => _plugin.ReadFile("conversations/nonexistent/001.txt");

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadFileBytes_ShouldReturnUtf8Bytes()
    {
        // Arrange
        var convId = "bytes-test";
        _plugin.CreateDirectory($"conversations/{convId}");
        _plugin.WriteFile($"conversations/{convId}/001.txt", "Test content");

        // Act
        var bytes = _plugin.ReadFileBytes($"conversations/{convId}/001.txt");

        // Assert
        bytes.Should().NotBeEmpty();
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        text.Should().Contain("Test content");
    }

    [Fact]
    public void DeleteFile_ShouldRemoveMessage()
    {
        // Arrange
        var convId = "delete-test";
        _plugin.CreateDirectory($"conversations/{convId}");
        _plugin.WriteFile($"conversations/{convId}/001.txt", "Message to delete");

        // WriteFile creates 2 messages (user + assistant), so we have 001.txt and 002.txt
        var filesBefore = _plugin.GetFiles($"conversations/{convId}").ToList();

        // Act - Delete the first message
        _plugin.DeleteFile($"conversations/{convId}/001.txt");

        // Assert - Should have one less file
        var filesAfter = _plugin.GetFiles($"conversations/{convId}").ToList();
        filesAfter.Should().HaveCount(filesBefore.Count - 1);
    }

    [Fact]
    public void GetFileSize_ShouldReturnByteCount()
    {
        // Arrange
        var convId = "size-test";
        _plugin.CreateDirectory($"conversations/{convId}");
        _plugin.WriteFile($"conversations/{convId}/001.txt", "Test");

        // Act
        var size = _plugin.GetFileSize($"conversations/{convId}/001.txt");

        // Assert
        size.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetChildren_ShouldReturnBothDirectoriesAndFiles()
    {
        // Arrange
        _plugin.CreateDirectory("conversations/conv-1");
        _plugin.WriteFile("conversations/conv-1/001.txt", "Test");

        // Act
        var children = _plugin.GetChildren("").ToList();

        // Assert
        children.Should().Contain("conversations");
        children.Should().Contain("models");
    }

    [Fact]
    public async Task SendChatMessageAsync_ShouldSendMessageWithHistory()
    {
        // Arrange
        var history = new List<(string Role, string Content)>
        {
            ("user", "First message"),
            ("assistant", "First response")
        };

        // Act
        var response = await _plugin.SendChatMessageAsync(history, "Second message");

        // Assert
        response.Should().Contain("Mock response to: Second message");
        response.Should().Contain("history: 2 messages");
    }

    [Fact]
    public async Task InitializeAsync_ShouldComplete()
    {
        // Act & Assert
        await _plugin.InitializeAsync();
        // Should complete without throwing
    }

    [Fact]
    public void Dispose_ShouldDisposeHttpClient()
    {
        // Arrange
        var httpClient = new HttpClient();
        var plugin = new MockAIProviderPlugin("test-key", httpClient);

        // Act
        plugin.Dispose();

        // Assert - HttpClient should be disposed (can't directly test, but shouldn't throw)
        var act = () => plugin.Dispose();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        _plugin?.Dispose();
        _httpClient?.Dispose();
    }
}
