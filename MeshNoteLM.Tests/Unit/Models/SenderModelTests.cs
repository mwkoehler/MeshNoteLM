using MeshNoteLM.Models;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Models;

public class SenderModelTests
{
    [Fact]
    public void Constructor_ShouldCreateSenderWithDefaultValues()
    {
        // Act
        var sender = new SenderModel();

        // Assert
        sender.Name.Should().BeNull();
        sender.Color.Should().BeNull();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var sender = new SenderModel
        {
            // Act
            Name = "Claude",
            Color = "#4A90E2"
        };

        // Assert
        sender.Name.Should().Be("Claude");
        sender.Color.Should().Be("#4A90E2");
    }

    [Fact]
    public void Name_ShouldAllowNull()
    {
        // Arrange
        var sender = new SenderModel
        {
            Name = "Initial Name"
        };

        // Act
        sender.Name = null;

        // Assert
        sender.Name.Should().BeNull();
    }

    [Fact]
    public void Color_ShouldAllowNull()
    {
        // Arrange
        var sender = new SenderModel
        {
            Color = "#FF0000"
        };

        // Act
        sender.Color = null;

        // Assert
        sender.Color.Should().BeNull();
    }

    [Fact]
    public void SenderModel_ShouldSupportObjectInitializer()
    {
        // Act
        var sender = new SenderModel
        {
            Name = "OpenAI",
            Color = "#00A67E"
        };

        // Assert
        sender.Name.Should().Be("OpenAI");
        sender.Color.Should().Be("#00A67E");
    }

    [Fact]
    public void Name_ShouldHandleEmptyString()
    {
        // Arrange
        var sender = new SenderModel
        {
            // Act
            Name = string.Empty
        };

        // Assert
        sender.Name.Should().BeEmpty();
        sender.Name.Should().NotBeNull();
    }

    [Fact]
    public void Color_ShouldHandleDifferentColorFormats()
    {
        // Arrange & Act
        var hexSender = new SenderModel { Color = "#FF5733" };
        var rgbSender = new SenderModel { Color = "rgb(255, 87, 51)" };
        var namedSender = new SenderModel { Color = "red" };

        // Assert
        hexSender.Color.Should().Be("#FF5733");
        rgbSender.Color.Should().Be("rgb(255, 87, 51)");
        namedSender.Color.Should().Be("red");
    }
}
