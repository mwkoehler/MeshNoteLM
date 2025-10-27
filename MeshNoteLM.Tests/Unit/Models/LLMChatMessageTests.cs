using MeshNoteLM.Models;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Models;

public class LLMChatMessageTests
{
    [Fact]
    public void Constructor_ShouldCreateUserMessage()
    {
        // Act
        var message = new LLMChatMessage
        {
            Role = "user",
            Content = "Hello, how are you?"
        };

        // Assert
        message.Role.Should().Be("user");
        message.Content.Should().Be("Hello, how are you?");
        message.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(2));
        message.LLMProvider.Should().BeNull();
        message.ErrorMessage.Should().BeNull();
        message.IsError.Should().BeFalse();
    }

    [Fact]
    public void Constructor_ShouldCreateAssistantMessage()
    {
        // Act
        var message = new LLMChatMessage
        {
            Role = "assistant",
            Content = "I'm doing well, thank you!",
            LLMProvider = "Claude"
        };

        // Assert
        message.Role.Should().Be("assistant");
        message.Content.Should().Be("I'm doing well, thank you!");
        message.LLMProvider.Should().Be("Claude");
        message.IsError.Should().BeFalse();
    }

    [Fact]
    public void ToMarkdown_ShouldFormatUserMessage()
    {
        // Arrange
        var timestamp = new DateTime(2025, 10, 23, 14, 30, 0);
        var message = new LLMChatMessage
        {
            Role = "user",
            Content = "What is the weather?",
            Timestamp = timestamp
        };

        // Act
        var markdown = message.ToMarkdown();

        // Assert
        markdown.Should().Contain("2025-10-23 14:30:00");
        markdown.Should().Contain("**User:**");
        markdown.Should().Contain("What is the weather?");
        markdown.Should().StartWith("##");
    }

    [Fact]
    public void ToMarkdown_ShouldFormatAssistantMessage()
    {
        // Arrange
        var timestamp = new DateTime(2025, 10, 23, 14, 30, 5);
        var message = new LLMChatMessage
        {
            Role = "assistant",
            Content = "It's sunny today!",
            LLMProvider = "OpenAI",
            Timestamp = timestamp
        };

        // Act
        var markdown = message.ToMarkdown();

        // Assert
        markdown.Should().Contain("2025-10-23 14:30:05");
        markdown.Should().Contain("**OpenAI:**");
        markdown.Should().Contain("It's sunny today!");
        markdown.Should().StartWith("##");
    }

    [Fact]
    public void ToMarkdown_ShouldFormatErrorMessage()
    {
        // Arrange
        var timestamp = new DateTime(2025, 10, 23, 14, 30, 10);
        var message = new LLMChatMessage
        {
            Role = "assistant",
            Content = "",
            LLMProvider = "Claude",
            Timestamp = timestamp,
            ErrorMessage = "API rate limit exceeded"
        };

        // Act
        var markdown = message.ToMarkdown();

        // Assert
        markdown.Should().Contain("2025-10-23 14:30:10");
        markdown.Should().Contain("**Claude (Error):**");
        markdown.Should().Contain("API rate limit exceeded");
        markdown.Should().NotContain("**Claude:**", "should use (Error) suffix");
    }

    [Fact]
    public void FromMarkdown_ShouldParseUserMessage()
    {
        // Arrange
        var markdown = @"## 2025-10-23 14:30:00
**User:** What is 2+2?
";

        // Act
        var message = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        message.Should().NotBeNull();
        message!.Role.Should().Be("user");
        message.Content.Should().Be("What is 2+2?");
        message.Timestamp.Should().Be(new DateTime(2025, 10, 23, 14, 30, 0));
        message.LLMProvider.Should().BeNull();
        message.IsError.Should().BeFalse();
    }

    [Fact]
    public void FromMarkdown_ShouldParseAssistantMessage()
    {
        // Arrange
        var markdown = @"## 2025-10-23 14:30:05
**Gemini:** 2+2 equals 4.
";

        // Act
        var message = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        message.Should().NotBeNull();
        message!.Role.Should().Be("assistant");
        message.Content.Should().Be("2+2 equals 4.");
        message.Timestamp.Should().Be(new DateTime(2025, 10, 23, 14, 30, 5));
        message.LLMProvider.Should().Be("Gemini");
        message.IsError.Should().BeFalse();
    }

    [Fact]
    public void FromMarkdown_ShouldParseErrorMessage()
    {
        // Arrange
        var markdown = @"## 2025-10-23 14:30:10
**Claude (Error):** Connection timeout
";

        // Act
        var message = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        message.Should().NotBeNull();
        message!.Role.Should().Be("assistant");
        message.IsError.Should().BeTrue();
        message.ErrorMessage.Should().Be("Connection timeout");
        message.LLMProvider.Should().Be("Claude");
        message.Timestamp.Should().Be(new DateTime(2025, 10, 23, 14, 30, 10));
    }

    [Fact]
    public void FromMarkdown_ShouldReturnNull_WhenInvalidFormat()
    {
        // Arrange
        var invalidMarkdown = "Just some random text without proper format";

        // Act
        var message = LLMChatMessage.FromMarkdown(invalidMarkdown);

        // Assert
        message.Should().BeNull();
    }

    [Fact]
    public void ToMarkdown_AndFromMarkdown_ShouldRoundTrip_UserMessage()
    {
        // Arrange
        var original = new LLMChatMessage
        {
            Role = "user",
            Content = "Test message",
            Timestamp = new DateTime(2025, 10, 23, 12, 0, 0)
        };

        // Act
        var markdown = original.ToMarkdown();
        var parsed = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        parsed.Should().NotBeNull();
        parsed!.Role.Should().Be(original.Role);
        parsed.Content.Should().Be(original.Content);
        parsed.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ToMarkdown_AndFromMarkdown_ShouldRoundTrip_AssistantMessage()
    {
        // Arrange
        var original = new LLMChatMessage
        {
            Role = "assistant",
            Content = "Test response",
            LLMProvider = "Grok",
            Timestamp = new DateTime(2025, 10, 23, 12, 0, 5)
        };

        // Act
        var markdown = original.ToMarkdown();
        var parsed = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        parsed.Should().NotBeNull();
        parsed!.Role.Should().Be(original.Role);
        parsed.Content.Should().Be(original.Content);
        parsed.LLMProvider.Should().Be(original.LLMProvider);
        parsed.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void ToMarkdown_AndFromMarkdown_ShouldRoundTrip_ErrorMessage()
    {
        // Arrange
        var original = new LLMChatMessage
        {
            Role = "assistant",
            Content = "",
            LLMProvider = "Mistral",
            Timestamp = new DateTime(2025, 10, 23, 12, 0, 10),
            ErrorMessage = "Network error"
        };

        // Act
        var markdown = original.ToMarkdown();
        var parsed = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        parsed.Should().NotBeNull();
        parsed!.Role.Should().Be(original.Role);
        parsed.IsError.Should().BeTrue();
        parsed.ErrorMessage.Should().Be(original.ErrorMessage);
        parsed.LLMProvider.Should().Be(original.LLMProvider);
        parsed.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void FromMarkdown_ShouldHandleMultilineContent()
    {
        // Arrange
        var markdown = @"## 2025-10-23 14:30:00
**Claude:** This is a response
with multiple lines
of content.
";

        // Act
        var message = LLMChatMessage.FromMarkdown(markdown);

        // Assert
        message.Should().NotBeNull();
        message!.Content.Should().Contain("multiple lines");
    }

    [Fact]
    public void Constructor_ShouldSetTimestampAutomatically()
    {
        // Arrange
        var before = DateTime.Now;

        // Act
        var message = new LLMChatMessage
        {
            Role = "user",
            Content = "Test"
        };
        var after = DateTime.Now;

        // Assert
        message.Timestamp.Should().BeOnOrAfter(before);
        message.Timestamp.Should().BeOnOrBefore(after);
    }
}
