using MeshNoteLM.Interfaces;
using MeshNoteLM.Models;
using SQLite;

namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Mock implementation of IAppDatabase using in-memory SQLite for testing
/// </summary>
public class MockAppDatabase : IAppDatabase
{
    private readonly SQLiteAsyncConnection _connection;

    public MockAppDatabase()
    {
        // Create in-memory database for testing
        _connection = new SQLiteAsyncConnection(":memory:");
        InitializeAsync().Wait();
    }

    public SQLiteAsyncConnection Connection => _connection;

    public async Task InitializeAsync()
    {
        await _connection.CreateTableAsync<NoteModel>();
    }

    public async Task<int> ClearAllDataAsync()
    {
        await _connection.DeleteAllAsync<NoteModel>();
        return 0;
    }

    public void Dispose()
    {
        _connection?.CloseAsync().Wait();
    }
}
