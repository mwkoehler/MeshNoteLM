using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeshNoteLM.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeshNoteLM.ViewModels
{
    public sealed partial class TreeNodeViewModel(
        string name,
        string fullPath,
        bool isDirectory,
        Func<CancellationToken, Task<IReadOnlyList<TreeNodeViewModel>>>? childrenFactory,
        IFileSystemPlugin? plugin = null) : INotifyPropertyChanged
    {
        public string Name { get; } = name;
        public string FullPath { get; } = fullPath;
        public bool IsDirectory { get; } = isDirectory;
        public IFileSystemPlugin? Plugin { get; } = plugin;

        public bool IsExpanded { get; set; } = false;

        /// <summary>
        /// Null for files. For directories, call to lazily fetch children.
        /// </summary>
        public Func<CancellationToken, Task<IReadOnlyList<TreeNodeViewModel>>>? ChildrenFactory { get; } = childrenFactory;

        // Optional convenience cache if your UI wants to keep loaded children
        private IReadOnlyList<TreeNodeViewModel>? _childrenCache;
        public IReadOnlyList<TreeNodeViewModel>? Children
        {
            get => _childrenCache;
            private set { _childrenCache = value; OnPropertyChanged(nameof(Children)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>
        /// Loads children once (if a directory). Subsequent calls return cached results.
        /// </summary>
        public async Task<IReadOnlyList<TreeNodeViewModel>> EnsureChildrenLoadedAsync(CancellationToken ct = default)
        {
            if (!IsDirectory || ChildrenFactory is null)
                return [];

            if (Children is not null)
                return Children;

            var kids = await ChildrenFactory(ct).ConfigureAwait(false);
            Children = kids;
            return kids;
        }
    }

}
