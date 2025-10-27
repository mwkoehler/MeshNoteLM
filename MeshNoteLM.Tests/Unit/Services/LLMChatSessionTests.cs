using MeshNoteLM.Models;
using MeshNoteLM.Services;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class LLMChatSessionTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LLMChatSession _session;

    public LLMChatSessionTests()
    {
        // Create unique temp directory for each test
        _testDirectory = Path.Combine(Path.GetTempPath(), "LLMChatSessionTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_testDirectory);
        _session = new LLMChatSession();
    }

    [Fact]
    public void AddUserMessage_ShouldAddToMessages()
    {
        // Act
        _session.AddUserMessage("Hello, how are you?");

        // Assert
        _session.Messages.Should().HaveCount(1);
        _session.Messages[0].Role.Should().Be("user");
        _session.Messages[0].Content.Should().Be("Hello, how are you?");
        _session.Messages[0].Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void AddAssistantMessage_ShouldAddToMessagesWithProvider()
    {
        // Act
        _session.AddAssistantMessage("I'm doing well, thank you!", "Claude");

        // Assert
        _session.Messages.Should().HaveCount(1);
        _session.Messages[0].Role.Should().Be("assistant");
        _session.Messages[0].Content.Should().Be("I'm doing well, thank you!");
        _session.Messages[0].LLMProvider.Should().Be("Claude");
        _session.Messages[0].IsError.Should().BeFalse();
    }

    [Fact]
    public void AddErrorMessage_ShouldAddToMessagesWithError()
    {
        // Act
        _session.AddErrorMessage("API rate limit exceeded", "OpenAI");

        // Assert
        _session.Messages.Should().HaveCount(1);
        _session.Messages[0].Role.Should().Be("assistant");
        _session.Messages[0].LLMProvider.Should().Be("OpenAI");
        _session.Messages[0].ErrorMessage.Should().Be("API rate limit exceeded");
        _session.Messages[0].IsError.Should().BeTrue();
    }

    [Fact]
    public void GetConversationContext_ShouldReturnEmpty_WhenNoMessagesAndNoFile()
    {
        // Act
        var context = _session.GetConversationContext();

        // Assert
        context.Should().BeEmpty("no file path or messages means no context");
    }

    [Fact]
    public void GetConversationContext_ShouldIncludeFileContent()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.md");
        _session.CurrentFilePath = testFilePath;
        _session.CurrentFileContent = "# My Document\nThis is the file content.";

        // Act
        var context = _session.GetConversationContext();

        // Assert
        context.Should().Contain("test.md");
        context.Should().Contain("# My Document");
        context.Should().Contain("This is the file content.");
    }

    [Fact]
    public void GetConversationContext_ShouldIncludeMessageHistory()
    {
        // Arrange
        _session.AddUserMessage("What is 2+2?");
        _session.AddAssistantMessage("2+2 equals 4.", "Claude");

        // Act
        var context = _session.GetConversationContext();

        // Assert
        context.Should().Contain("User: What is 2+2?");
        context.Should().Contain("Claude: 2+2 equals 4.");
    }

    [Fact]
    public void GetConversationContext_ShouldExcludeErrors()
    {
        // Arrange
        _session.AddUserMessage("What is 2+2?");
        _session.AddErrorMessage("Connection failed", "Claude");
        _session.AddAssistantMessage("2+2 equals 4.", "Claude");

        // Act
        var context = _session.GetConversationContext();

        // Assert
        context.Should().Contain("User: What is 2+2?");
        context.Should().Contain("Claude: 2+2 equals 4.");
        context.Should().NotContain("Connection failed");
    }

    [Fact]
    public void GetMessageHistory_ShouldReturnRoleContentPairs()
    {
        // Arrange
        _session.AddUserMessage("Hello");
        _session.AddAssistantMessage("Hi there!", "Claude");

        // Act
        var history = _session.GetMessageHistory();

        // Assert
        history.Should().HaveCount(2);
        history[0].Role.Should().Be("user");
        history[0].Content.Should().Be("Hello");
        history[1].Role.Should().Be("assistant");
        history[1].Content.Should().Be("Hi there!");
    }

    [Fact]
    public void GetMessageHistory_ShouldExcludeErrors()
    {
        // Arrange
        _session.AddUserMessage("Hello");
        _session.AddErrorMessage("Failed", "Claude");
        _session.AddAssistantMessage("Hi!", "Claude");

        // Act
        var history = _session.GetMessageHistory();

        // Assert
        history.Should().HaveCount(2);
        history.Should().NotContain(m => m.Content == "Failed");
    }

    [Fact]
    public void SaveCurrentSession_ShouldCreateChatFile()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.md");
        _session.CurrentFilePath = testFilePath;
        _session.AddUserMessage("Test message");

        // Act
        _session.SaveCurrentSession();

        // Assert
        var chatFilePath = testFilePath + ".chat.md";
        File.Exists(chatFilePath).Should().BeTrue();
        var content = File.ReadAllText(chatFilePath);
        content.Should().Contain("test.md");
        content.Should().Contain("Test message");
    }

    [Fact]
    public void CurrentFilePath_ShouldLoadExistingSession()
    {
        // Arrange - Create a chat file
        var testFilePath = Path.Combine(_testDirectory, "existing.md");
        var chatFilePath = testFilePath + ".chat.md";
        Directory.CreateDirectory(Path.GetDirectoryName(chatFilePath)!);

        var chatContent = @"# Chat for existing.md

## 2025-10-23 10:30:00
**User:** Previous message

## 2025-10-23 10:30:05
**Claude:** Previous response
";
        File.WriteAllText(chatFilePath, chatContent);

        // Act
        _session.CurrentFilePath = testFilePath;

        // Assert
        _session.Messages.Should().HaveCount(2);
        _session.Messages[0].Content.Trim().Should().Be("Previous message");
        _session.Messages[1].Content.Trim().Should().Be("Previous response");
    }

    [Fact]
    public void CurrentFilePath_ShouldSavePreviousSession_BeforeLoadingNew()
    {
        // Arrange
        var file1 = Path.Combine(_testDirectory, "file1.md");
        var file2 = Path.Combine(_testDirectory, "file2.md");

        _session.CurrentFilePath = file1;
        _session.AddUserMessage("Message for file1");

        // Act - Switch to file2
        _session.CurrentFilePath = file2;
        _session.AddUserMessage("Message for file2");

        // Assert - file1 chat should be saved
        var chat1Path = file1 + ".chat.md";
        File.Exists(chat1Path).Should().BeTrue();
        var chat1Content = File.ReadAllText(chat1Path);
        chat1Content.Should().Contain("Message for file1");
        chat1Content.Should().NotContain("Message for file2");

        // And file2 chat should have its message
        _session.Messages.Should().HaveCount(1);
        _session.Messages[0].Content.Should().Be("Message for file2");
    }

    [Fact]
    public void ClearConversation_ShouldRemoveAllMessages()
    {
        // Arrange
        _session.AddUserMessage("Message 1");
        _session.AddAssistantMessage("Response 1", "Claude");

        // Act
        _session.ClearConversation();

        // Assert
        _session.Messages.Should().BeEmpty();
    }

    [Fact]
    public void ClearConversation_ShouldDeleteChatFile()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "test.md");
        _session.CurrentFilePath = testFilePath;
        _session.AddUserMessage("Test message");
        _session.SaveCurrentSession();

        var chatFilePath = testFilePath + ".chat.md";
        File.Exists(chatFilePath).Should().BeTrue();

        // Act
        _session.ClearConversation();

        // Assert
        File.Exists(chatFilePath).Should().BeFalse();
    }

    [Fact]
    public void SaveCurrentSession_ShouldHandleMultipleMessages()
    {
        // Arrange
        var testFilePath = Path.Combine(_testDirectory, "multi.md");
        _session.CurrentFilePath = testFilePath;
        _session.AddUserMessage("First question");
        _session.AddAssistantMessage("First answer", "Claude");
        _session.AddUserMessage("Second question");
        _session.AddAssistantMessage("Second answer", "OpenAI");

        // Act
        _session.SaveCurrentSession();

        // Assert
        var chatFilePath = testFilePath + ".chat.md";
        var content = File.ReadAllText(chatFilePath);
        content.Should().Contain("First question");
        content.Should().Contain("First answer");
        content.Should().Contain("Second question");
        content.Should().Contain("Second answer");
        content.Should().Contain("Claude");
        content.Should().Contain("OpenAI");
    }

    [Fact]
    public void SaveCurrentSession_ShouldNotFail_WhenNoFilePathSet()
    {
        // Arrange - No CurrentFilePath set
        _session.AddUserMessage("Test message");

        // Act & Assert - Should not throw
        var act = () => _session.SaveCurrentSession();
        act.Should().NotThrow();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
