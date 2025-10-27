using MeshNoteLM.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshNoteLM.Plugins
{
    public abstract class PluginBase : IPlugin
    {
        public abstract string Name { get; }
        public abstract string Version { get; }
        public abstract string Description { get; }
        public virtual string Author => "Starglass";

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Override this method to implement authorization checks.
        /// Default implementation returns true (no authorization required).
        /// </summary>
        public virtual bool HasValidAuthorization()
        {
            return true; // Default: no authorization required
        }

        /// <summary>
        /// Override this method to test actual API connection.
        /// Default implementation returns success if HasValidAuthorization passes.
        /// </summary>
        public virtual Task<(bool Success, string Message)> TestConnectionAsync()
        {
            if (!HasValidAuthorization())
            {
                return Task.FromResult((false, "Invalid - Missing or invalid credentials"));
            }
            return Task.FromResult((true, "✓ Valid - Plugin enabled"));
        }

        public virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual void Dispose()
        {
            // Override if cleanup is needed
        }
    }
}
