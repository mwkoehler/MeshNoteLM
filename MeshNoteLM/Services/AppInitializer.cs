using MeshNoteLM.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshNoteLM.Services
{
    // AppInitializer.cs
    public sealed class AppInitializer
    {
        private readonly IAppDatabase _db;

        public AppInitializer(IAppDatabase db)
        {
            System.Diagnostics.Debug.WriteLine("=== AppInitializer constructor START ===");
            try
            {
                _db = db;
                System.Diagnostics.Debug.WriteLine("=== AppInitializer constructor END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in AppInitializer constructor: {ex}");
                throw;
            }
        }

        public Task InitializeAsync()
        {
            System.Diagnostics.Debug.WriteLine("=== AppInitializer.InitializeAsync START ===");
            try
            {
                return _db.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in AppInitializer.InitializeAsync: {ex}");
                throw;
            }
        }
    }
}
