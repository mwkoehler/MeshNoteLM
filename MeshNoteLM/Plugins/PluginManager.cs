using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MeshNoteLM.Interfaces;
using MeshNoteLM;
using MeshNoteLM.Services;
using MeshNoteLM.Plugins;


namespace MeshNoteLM.Plugins
{
    public partial class PluginManager : IDisposable
    {
        private readonly Dictionary<string, IPlugin> _plugins = [];
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PluginManager> _logger;

        public PluginManager(IServiceProvider serviceProvider, ILogger<PluginManager> logger)
        {
            System.Diagnostics.Debug.WriteLine("=== PluginManager constructor START ===");
            try
            {
                _serviceProvider = serviceProvider;
                _logger = logger;
                System.Diagnostics.Debug.WriteLine("=== PluginManager constructor END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in PluginManager constructor: {ex}");
                throw;
            }
        }

        public async Task LoadPluginsAsync()
        {
            _logger.LogInformation("=== Starting LoadPluginsAsync ===");

            // Get all plugin types from current assembly
            var pluginTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            _logger.LogInformation("Found {Count} plugin types to load", pluginTypes.Count);

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    _logger.LogInformation("Creating plugin: {PluginType}", pluginType.Name);
                    var plugin = (IPlugin)Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(_serviceProvider, pluginType);

                    _logger.LogInformation("Initializing plugin: {PluginType}", pluginType.Name);
                    await plugin.InitializeAsync();

                    // Check if plugin has valid authorization
                    if (!plugin.HasValidAuthorization())
                    {
                        _logger.LogWarning("⚠ Plugin {PluginName} disabled: missing or invalid authorization (API key, credentials, etc.)", plugin.Name);
                        plugin.IsEnabled = false;
                    }

                    _plugins[plugin.Name] = plugin;

                    if (plugin.IsEnabled)
                    {
                        _logger.LogInformation("✓ Loaded plugin: {PluginName} v{Version}", plugin.Name, plugin.Version);
                    }
                    else
                    {
                        _logger.LogInformation("○ Plugin loaded but disabled: {PluginName} v{Version}", plugin.Name, plugin.Version);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "✗ Failed to load plugin: {PluginType}", pluginType.Name);
                }
            }

            _logger.LogInformation("=== Completed LoadPluginsAsync - {Count} plugins loaded ===", _plugins.Count);
        }

        internal IPlugin? GetPlugin(string name) => _plugins.TryGetValue(name, out var plugin) ? plugin : null;

        internal IEnumerable<IPlugin> GetAllPlugins() => _plugins.Values;

        public void EnablePlugin(string name)
        {
            if (_plugins.TryGetValue(name, out var plugin))
                plugin.IsEnabled = true;
        }

        public void DisablePlugin(string name)
        {
            if (_plugins.TryGetValue(name, out var plugin))
                plugin.IsEnabled = false;
        }

        public async Task ReloadPluginAsync(string name)
        {
            _logger.LogInformation("Reloading plugin: {PluginName}", name);

            // Dispose existing plugin if it exists
            if (_plugins.TryGetValue(name, out var existingPlugin))
            {
                existingPlugin.Dispose();
                _plugins.Remove(name);
            }

            // Find the plugin type
            var pluginType = Assembly.GetExecutingAssembly()
                .GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) &&
                                     !t.IsInterface &&
                                     !t.IsAbstract &&
                                     t.Name.Contains(name, StringComparison.OrdinalIgnoreCase));

            if (pluginType == null)
            {
                _logger.LogWarning("Plugin type not found for: {PluginName}", name);
                return;
            }

            try
            {
                // Create new instance
                var plugin = (IPlugin)Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(_serviceProvider, pluginType);
                await plugin.InitializeAsync();

                // Check authorization
                if (!plugin.HasValidAuthorization())
                {
                    _logger.LogWarning("⚠ Plugin {PluginName} disabled: missing or invalid authorization", plugin.Name);
                    plugin.IsEnabled = false;
                }

                _plugins[plugin.Name] = plugin;

                if (plugin.IsEnabled)
                {
                    _logger.LogInformation("✓ Reloaded plugin: {PluginName} v{Version}", plugin.Name, plugin.Version);
                }
                else
                {
                    _logger.LogInformation("○ Plugin reloaded but disabled: {PluginName} v{Version}", plugin.Name, plugin.Version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "✗ Failed to reload plugin: {PluginName}", name);
            }
        }

        public void Dispose()
        {
            foreach (var plugin in _plugins.Values)
            {
                plugin.Dispose();
            }
            _plugins.Clear();
            GC.SuppressFinalize(this);
        }
    }

}
