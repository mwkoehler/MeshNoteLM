using MeshNoteLM.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MeshNoteLM.ViewModels;


namespace MeshNoteLM.Helpers
{
    internal static class TreeBuilder
    {
        /// <summary>
        /// Build top-level nodes: one per plugin (lazy), suitable to show under a single "Sources" root.
        /// </summary>
        internal static List<TreeNodeViewModel> BuildPluginRoots(IEnumerable<FileSystemSource> plugins)
        {
            return [.. plugins.Select(p =>
                new TreeNodeViewModel(
                    name: TreeLogic.SafeString(p?.Name) ?? "Plugin",
                    fullPath: "/",                                  // plugin root
                    isDirectory: true,
                    childrenFactory: ct => BuildDirectoryChildrenAsync(p?.Plugin!, "/", ct),
                    plugin: p?.Plugin
                )
            )];
        }

        /// <summary>
        /// Create a directory node (lazy).
        /// </summary>
        internal static TreeNodeViewModel CreateDirectoryNode(IFileSystemPlugin fs, string directoryPath)
            => new(
                name: TreeLogic.GetLeafName(directoryPath),
                fullPath: directoryPath,
                isDirectory: true,
                childrenFactory: ct => BuildDirectoryChildrenAsync(fs, directoryPath, ct),
                plugin: fs
            );

        /// <summary>
        /// Create a file node (no children).
        /// </summary>
        public static TreeNodeViewModel CreateFileNode(IFileSystemPlugin fs, string filePath)
            => new(
                name: TreeLogic.GetLeafName(filePath),
                fullPath: filePath,
                isDirectory: false,
                childrenFactory: null,
                plugin: fs
            );

        /// <summary>
        /// Actually enumerate a single directory (TopDirectoryOnly), and return nodes
        /// (directories first, then files), each with their own lazy factories.
        /// </summary>
        internal static Task<IReadOnlyList<TreeNodeViewModel>> BuildDirectoryChildrenAsync(
            IFileSystemPlugin fs,
            string directoryPath,
            CancellationToken ct = default)
        {
            System.Diagnostics.Debug.WriteLine($"[TreeBuilder] BuildDirectoryChildrenAsync called for plugin '{fs.Name}', path: '{directoryPath}'");

            // Your IFileSystemPlugin is synchronous; keep it quick and non-recursive.
            var dirs = fs
                .GetDirectories(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Select(d => CreateDirectoryNode(fs, d))
                .ToList();
            System.Diagnostics.Debug.WriteLine($"[TreeBuilder] Created {dirs.Count} directory nodes");

            var files = fs
                .GetFiles(directoryPath, "*", SearchOption.TopDirectoryOnly)
                .Select(f => CreateFileNode(fs, f))
                .ToList();
            System.Diagnostics.Debug.WriteLine($"[TreeBuilder] Created {files.Count} file nodes");

            // Use TreeLogic for sorting (adapter pattern)
            var adapter = new TreeNodeAdapter();
            var sorted = TreeLogic.SortTreeNodes(dirs.Concat(files).Select(n => adapter.Wrap(n)))
                .Select(wrapped => wrapped.Node)
                .ToList()
                .AsReadOnly();

            System.Diagnostics.Debug.WriteLine($"[TreeBuilder] Returning {sorted.Count} total nodes");
            return Task.FromResult<IReadOnlyList<TreeNodeViewModel>>(sorted);
        }

        /// <summary>
        /// Adapter to make TreeNodeViewModel compatible with TreeLogic.ITreeNode
        /// </summary>
        private class TreeNodeAdapter
        {
            public TreeNodeWrapper Wrap(TreeNodeViewModel node) => new(node);
        }

        private class TreeNodeWrapper(TreeNodeViewModel node) : TreeLogic.ITreeNode
        {
            public TreeNodeViewModel Node { get; } = node;
            public string Name => Node.Name;
            public bool IsDirectory => Node.IsDirectory;
        }
    }

}
