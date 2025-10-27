using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshNoteLM.Interfaces
{
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        bool IsEnabled { get; set; }
        string Author { get; }

        /// <summary>
        /// Checks if the plugin has valid authorization (API keys, credentials, etc.)
        /// Returns true if plugin can operate without authorization, or if authorization is valid.
        /// Returns false if authorization is required but missing/invalid.
        /// </summary>
        bool HasValidAuthorization();

        /// <summary>
        /// Tests the connection/credentials by making an actual API call.
        /// Returns (success, message) tuple.
        /// </summary>
        Task<(bool Success, string Message)> TestConnectionAsync();

        Task InitializeAsync();
        void Dispose();
    }
}
