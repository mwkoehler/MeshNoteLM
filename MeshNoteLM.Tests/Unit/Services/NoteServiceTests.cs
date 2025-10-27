using MeshNoteLM.Interfaces;
using MeshNoteLM.Models;
using MeshNoteLM.Services;
using MeshNoteLM.Tests.Mocks;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class NoteServiceTests : IDisposable
{
    private readonly IAppDatabase _mockDatabase;
    private readonly INoteService _noteService;

    public NoteServiceTests()
    {
        _mockDatabase = new MockAppDatabase();
        _noteService = new NoteService(_mockDatabase);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNull_WhenNoteDoesNotExist()
    {
        // Act
        var result = await _noteService.GetAsync(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldInsertNewNote_WhenIdIsZero()
    {
        // Arrange
        var newNote = new NoteModel
        {
            Id = 0,
            Text = "Test note",
            Timestamp = DateTime.UtcNow.ToString("O")
        };

        // Act
        var rowsAffected = await _noteService.UpdateAsync(newNote);

        // Assert
        rowsAffected.Should().Be(1);
        newNote.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExistingNote_WhenIdIsNonZero()
    {
        // Arrange - Insert a note first
        var note = new NoteModel
        {
            Id = 0,
            Text = "Original text",
            Timestamp = DateTime.UtcNow.ToString("O")
        };
        await _noteService.UpdateAsync(note);

        // Act - Update the note
        note.Text = "Updated text";
        var rowsAffected = await _noteService.UpdateAsync(note);

        // Assert
        rowsAffected.Should().Be(1);
        var retrievedNote = await _noteService.GetAsync(note.Id);
        retrievedNote.Should().NotBeNull();
        retrievedNote!.Text.Should().Be("Updated text");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnEmptyList_WhenNoNotesExist()
    {
        // Act
        var notes = await _noteService.GetAllAsync();

        // Assert
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnAllNotes_OrderedByTimestamp()
    {
        // Arrange
        var note1 = new NoteModel { Text = "Note 1", Timestamp = DateTime.UtcNow.AddMinutes(-10).ToString("O") };
        var note2 = new NoteModel { Text = "Note 2", Timestamp = DateTime.UtcNow.AddMinutes(-5).ToString("O") };
        var note3 = new NoteModel { Text = "Note 3", Timestamp = DateTime.UtcNow.ToString("O") };

        await _noteService.UpdateAsync(note1);
        await _noteService.UpdateAsync(note2);
        await _noteService.UpdateAsync(note3);

        // Act
        var notes = await _noteService.GetAllAsync();

        // Assert
        notes.Should().HaveCount(3);
        notes[0].Text.Should().Be("Note 1");
        notes[1].Text.Should().Be("Note 2");
        notes[2].Text.Should().Be("Note 3");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveNote_WhenNoteExists()
    {
        // Arrange
        var note = new NoteModel { Text = "Test note", Timestamp = DateTime.UtcNow.ToString("O") };
        await _noteService.UpdateAsync(note);

        // Act
        var rowsAffected = await _noteService.DeleteAsync(note);

        // Assert
        rowsAffected.Should().Be(1);
        var retrievedNote = await _noteService.GetAsync(note.Id);
        retrievedNote.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnCorrectNote_WhenMultipleNotesExist()
    {
        // Arrange
        var note1 = new NoteModel { Text = "Note 1", Timestamp = DateTime.UtcNow.ToString("O") };
        var note2 = new NoteModel { Text = "Note 2", Timestamp = DateTime.UtcNow.ToString("O") };

        await _noteService.UpdateAsync(note1);
        await _noteService.UpdateAsync(note2);

        // Act
        var retrieved = await _noteService.GetAsync(note2.Id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(note2.Id);
        retrieved.Text.Should().Be("Note 2");
    }

    public void Dispose()
    {
        (_mockDatabase as MockAppDatabase)?.Dispose();
    }
}
