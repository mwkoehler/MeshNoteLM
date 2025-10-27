using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// File: ViewModels/FileTreeViewModel.cs
using System.Collections.ObjectModel;

namespace MeshNoteLM.ViewModels;

public sealed class FileTreeViewModel
{
    ObservableCollection<FsNode> Roots { get; } = [];

    FileTreeViewModel(IEnumerable<FileSystemSource> sources)
    {
        // Each plugin shows as a top-level directory node (labelled with source Name)
        foreach (var s in sources)
        {
            // The root node uses the plugin's RootPath as the key but displays the friendly name
            Roots.Add(new FsNode(s, s.RootPath, isDirectory: true, displayName: s.Name));
        }
    }
}
