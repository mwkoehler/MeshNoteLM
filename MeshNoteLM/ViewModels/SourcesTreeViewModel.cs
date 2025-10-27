using MeshNoteLM.Helpers;
using MeshNoteLM.Interfaces;
using MeshNoteLM.Plugins;
using CommunityToolkit.Maui.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Versioning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm;

namespace MeshNoteLM.ViewModels
{
    public partial class SourcesTreeViewModel : ObservableObject
    {
        private readonly PluginManager _pluginManager = null!;

        // Use a partial property for AOT compatibility and analyzer support
        private ObservableCollection<TreeNodeViewModel> _nodes = [];

        public ObservableCollection<TreeNodeViewModel> Nodes
        {
            get => _nodes;
            set
            {
                if (_nodes != value)
                {
                    _nodes = value;
                    OnPropertyChanged(nameof(Nodes));
                }
            }
        }

        // [SupportedOSPlatform("windows10.0.17763.0")]
        public SourcesTreeViewModel(PluginManager pm)
        {
            System.Diagnostics.Debug.WriteLine("=== SourcesTreeViewModel constructor START ===");
            System.Diagnostics.Debug.WriteLine($"pm: {pm != null}");

            _pluginManager = pm!;
            // Initialize with empty collection - will be populated after plugins load via RefreshTree()
            Nodes = new ObservableCollection<TreeNodeViewModel>();
            // Don't call RefreshTree() here - it will be called after plugins are loaded in App.OnStart()

            System.Diagnostics.Debug.WriteLine("=== SourcesTreeViewModel constructor END ===");
        }

        public void RefreshTree()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== RefreshTree START ===");

                var allPlugins = _pluginManager.GetAllPlugins().ToList();
                System.Diagnostics.Debug.WriteLine($"Total plugins: {allPlugins.Count}");

                var fileSystemPlugins = allPlugins.OfType<IFileSystemPlugin>().ToList();
                System.Diagnostics.Debug.WriteLine($"FileSystem plugins: {fileSystemPlugins.Count}");

                var plugins = fileSystemPlugins.Where(p => p.IsEnabled).ToList();
                System.Diagnostics.Debug.WriteLine($"Enabled FileSystem plugins: {plugins.Count}");

                var sources = new List<FileSystemSource>();

                foreach (var plugin in plugins)
                {
                    System.Diagnostics.Debug.WriteLine($"Adding source: {plugin.Name}");
                    sources.Add(new FileSystemSource(plugin.Name, plugin, "/"));
                }

                System.Diagnostics.Debug.WriteLine($"Total sources: {sources.Count}");

                // Replace ToObservableCollection() with manual conversion for cross-platform compatibility
                if (sources.Any())
                {
                    var nodesList = TreeBuilder.BuildPluginRoots(sources);
                    System.Diagnostics.Debug.WriteLine($"Tree nodes built: {nodesList.Count}");
                    Nodes = new ObservableCollection<TreeNodeViewModel>(nodesList);
                    System.Diagnostics.Debug.WriteLine($"Nodes collection updated");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No sources found");
                }

                System.Diagnostics.Debug.WriteLine("=== RefreshTree END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshTree ERROR: {ex}");
                // If plugin loading fails, just start with empty nodes
                // This can happen if plugins haven't been loaded yet
            }
        }
    }
}
