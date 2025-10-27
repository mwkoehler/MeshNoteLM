using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// File: ViewModels/FileTree.cs
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MeshNoteLM.Interfaces; // IFileSystemPlugin
using Microsoft.Maui.ApplicationModel;

namespace MeshNoteLM.ViewModels;

internal class FileSystemSource(string name, IFileSystemPlugin plugin, string rootPath = "")
{
    public string Name { get; } = name;
    internal IFileSystemPlugin Plugin { get; } = plugin;
    public string RootPath { get; } = rootPath ?? "";
}

/// <summary>Node for TreeView, with lazy-loading support.</summary>
internal partial class FsNode(FileSystemSource source, string pathKey, bool isDirectory, string? displayName = null) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!Equals(field, value)) { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }

    internal FileSystemSource Source { get; } = source;
    public string PathKey { get; } = pathKey;
    public bool IsDirectory { get; } = isDirectory;
    public string Name { get; } = string.IsNullOrWhiteSpace(displayName) ? LastSegment(pathKey) : displayName;

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    private bool _isLoaded;
    public bool IsLoaded { get => _isLoaded; private set => Set(ref _isLoaded, value); }

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; private set => Set(ref _isLoading, value); }

    public ObservableCollection<FsNode> Children { get; } = [];

    static string LastSegment(string path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        var p = path.Replace('\\', '/');
        if (p.EndsWith('/')) p = p.TrimEnd('/');
        var i = p.LastIndexOf('/');
        return i >= 0 ? p[(i + 1)..] : p;
    }

    /// <summary>Lazy-load children when expanding a directory.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (IsLoaded || !IsDirectory) return;
        IsLoading = true;

        try
        {
            // Offload sync plugin I/O to a worker thread to keep UI responsive
            var (dirs, files) = await Task.Run(() =>
            {
                var d = Source.Plugin.GetDirectories(PathKey).ToList();
                var f = Source.Plugin.GetFiles(PathKey).ToList();
                return (d, f);
            }).ConfigureAwait(false);

            // marshal back to UI thread
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Children.Clear();

                foreach (var d in dirs)
                {
                    // d is a plugin-native path; pass through unchanged for subsequent calls
                    Children.Add(new FsNode(Source, d, isDirectory: true));
                }
                foreach (var f in files)
                {
                    Children.Add(new FsNode(Source, f, isDirectory: false));
                }

                IsLoaded = true;
            });
        }
        catch (Exception ex)
        {
            // If you want, surface an error node
            await Microsoft.Maui.ApplicationModel.MainThread.InvokeOnMainThreadAsync(() =>
            {
                Children.Clear();
                Children.Add(new FsNode(Source, PathKey + "::<error>", isDirectory: false, displayName: $"⚠ {ex.Message}"));
                IsLoaded = true;
            });
        }
        finally
        {
            IsLoading = false;
        }
    }
}

