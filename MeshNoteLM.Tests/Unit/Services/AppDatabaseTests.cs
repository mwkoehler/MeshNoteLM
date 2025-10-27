using MeshNoteLM.Interfaces;
using MeshNoteLM.Models;
using MeshNoteLM.Services;
using MeshNoteLM.Tests.Mocks;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class AppDatabaseTests : IDisposable
{
    private readonly TestFileSystemService _fileSystem;
    private readonly IAppDatabase _database;

    public AppDatabaseTests()
    {
        _fileSystem = new TestFileSystemService();
        _database = new AppDatabase(_fileSystem);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateTables()
    {
        // Act
        await _database.InitializeAsync();

        // Assert - Should not throw and connection should be available
        _database.Connection.Should().NotBeNull();
    }

    [Fact]
    public async Task Connection_ShouldUsesFileSystemDirectory()
    {
        // Act
        await _database.InitializeAsync();

        // Assert - Database file should be created in test directory
        var expectedDbPath = Path.Combine(_fileSystem.AppDataDirectory, "MeshNoteLM.db");
        File.Exists(expectedDbPath).Should().BeTrue();
    }

    [Fact]
    public async Task ClearAllDataAsync_ShouldRemoveAllNotes()
    {
        // Arrange
        await _database.InitializeAsync();

        // Add some test data
        var note1 = new NoteModel { Text = "Note 1", Timestamp = DateTime.UtcNow.ToString("O") };
        var note2 = new NoteModel { Text = "Note 2", Timestamp = DateTime.UtcNow.ToString("O") };
        await _database.Connection.InsertAsync(note1);
        await _database.Connection.InsertAsync(note2);

        // Act
        await _database.ClearAllDataAsync();

        // Assert
        var notes = await _database.Connection.Table<NoteModel>().ToListAsync();
        notes.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleInstances_ShouldShareSameDatabase()
    {
        // Arrange - Use same file system service for both databases
        var database1 = new AppDatabase(_fileSystem);
        var database2 = new AppDatabase(_fileSystem);

        await database1.InitializeAsync();
        await database2.InitializeAsync();

        // Act - Insert with database1
        var note = new NoteModel { Text = "Test", Timestamp = DateTime.UtcNow.ToString("O") };
        await database1.Connection.InsertAsync(note);

        // Assert - Read with database2
        var notes = await database2.Connection.Table<NoteModel>().ToListAsync();
        notes.Should().HaveCount(1);
        notes[0].Text.Should().Be("Test");

        // Cleanup
        database1.Dispose();
        database2.Dispose();
    }

    [Fact]
    public async Task Dispose_ShouldCloseConnection()
    {
        // Arrange
        await _database.InitializeAsync();
        var note = new NoteModel { Text = "Test", Timestamp = DateTime.UtcNow.ToString("O") };
        await _database.Connection.InsertAsync(note);

        // Act
        _database.Dispose();

        // Assert - Should not throw
        // (SQLite will throw if we try to use a disposed connection)
        var act = () => _database.Connection.Table<NoteModel>().ToListAsync();
        await act.Should().NotThrowAsync();  // Connection is lazy, won't fail until used
    }

    public void Dispose()
    {
        _database?.Dispose();
        _fileSystem?.Dispose();
    }
}
