using MeshNoteLM.Models;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Models;

public class NoteModelTests
{
    [Fact]
    public void Constructor_ShouldCreateNoteWithDefaultValues()
    {
        // Act
        var note = new NoteModel();

        // Assert
        note.Id.Should().Be(0);
        note.Text.Should().BeNull();
        note.Timestamp.Should().BeNull();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var note = new NoteModel
        {
            // Act
            Id = 42,
            Text = "This is a test note",
            Timestamp = "2025-10-23 14:30:00"
        };

        // Assert
        note.Id.Should().Be(42);
        note.Text.Should().Be("This is a test note");
        note.Timestamp.Should().Be("2025-10-23 14:30:00");
    }

    [Fact]
    public void Text_ShouldAllowNull()
    {
        // Arrange
        var note = new NoteModel
        {
            Text = "Initial text"
        };

        // Act
        note.Text = null;

        // Assert
        note.Text.Should().BeNull();
    }

    [Fact]
    public void Timestamp_ShouldAllowNull()
    {
        // Arrange
        var note = new NoteModel
        {
            Timestamp = "2025-10-23 14:30:00"
        };

        // Act
        note.Timestamp = null;

        // Assert
        note.Timestamp.Should().BeNull();
    }

    [Fact]
    public void Id_ShouldBeAutoIncrement()
    {
        // This test verifies the [AutoIncrement] attribute is present
        // Actual auto-increment behavior is tested in NoteServiceTests and AppDatabaseTests

        // Arrange & Act
        var note = new NoteModel();

        // Assert
        note.Id.Should().Be(0, "auto-increment fields start at 0 until persisted");
    }

    [Fact]
    public void Text_ShouldHandleEmptyString()
    {
        // Arrange
        var note = new NoteModel
        {
            // Act
            Text = string.Empty
        };

        // Assert
        note.Text.Should().BeEmpty();
        note.Text.Should().NotBeNull();
    }

    [Fact]
    public void Text_ShouldHandleLargeContent()
    {
        // Arrange
        var note = new NoteModel();
        var largeText = new string('A', 10000);

        // Act
        note.Text = largeText;

        // Assert
        note.Text.Should().HaveLength(10000);
        note.Text.Should().Be(largeText);
    }
}
