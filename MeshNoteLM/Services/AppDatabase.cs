using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshNoteLM.Models;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Services
{
    public sealed class AppDatabase : IAppDatabase
    {
        private readonly Lazy<SQLiteAsyncConnection> _conn;
        private readonly IFileSystemService _fileSystem;

        public AppDatabase(IFileSystemService fileSystem)
        {
            _fileSystem = fileSystem;
            System.Diagnostics.Debug.WriteLine("=== AppDatabase constructor START ===");
            try
            {
                _conn = new Lazy<SQLiteAsyncConnection>(() =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Creating SQLiteAsyncConnection...");

                        string appDataDir = _fileSystem.AppDataDirectory;
                        System.Diagnostics.Debug.WriteLine($"AppDataDirectory: {appDataDir}");

                        string dbPath = Path.Combine(appDataDir, "MeshNoteLM.db");
                        System.Diagnostics.Debug.WriteLine($"Database path: {dbPath}");

                        System.Diagnostics.Debug.WriteLine("About to create SQLiteAsyncConnection instance...");
                        var connection = new SQLiteAsyncConnection(
                            dbPath,
                            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
                        System.Diagnostics.Debug.WriteLine("SQLiteAsyncConnection created successfully");
                        return connection;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FATAL ERROR creating SQLiteAsyncConnection: {ex}");
                        throw;
                    }
                });
                System.Diagnostics.Debug.WriteLine("=== AppDatabase constructor END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in AppDatabase constructor: {ex}");
                throw;
            }
        }

        public SQLiteAsyncConnection Connection => _conn.Value;

        public async Task InitializeAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Creating NoteModel table...");
                await Connection.CreateTableAsync<NoteModel>();
                System.Diagnostics.Debug.WriteLine("NoteModel table created");

                System.Diagnostics.Debug.WriteLine("=== AppDatabase.InitializeAsync completed successfully ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in AppDatabase.InitializeAsync: {ex}");
                throw;
            }
        }

        public async Task<int> ClearAllDataAsync()
        {
            await Connection.DeleteAllAsync<NoteModel>();
            return 0;
        }

        public void Dispose()
        {
            if (_conn.IsValueCreated)
            {
                _conn.Value?.CloseAsync().Wait();
            }
        }
    }
}
