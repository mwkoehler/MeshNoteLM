/*
================================================================================
Local FileSystem Plugin Base Class
Abstract base class for platform-specific local filesystem plugins

Common Features:
- Sandboxed filesystem access with security validation
- Path traversal prevention
- Standard IFileSystemPlugin operations
- Platform-specific root directory resolution

Derived classes only need to implement:
- Platform-specific root path resolution
- Plugin metadata (Name, Version, Description)

This eliminates ~200 lines of duplicated code per local filesystem plugin.
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Plugins;

public abstract class LocalFileSystemPluginBase : PluginBase, IFileSystemPlugin
{
    protected readonly string _rootFullPath;

    protected LocalFileSystemPluginBase(string rootPath)
    {
        _rootFullPath = rootPath;

        if (!string.IsNullOrEmpty(_rootFullPath))
        {
            Directory.CreateDirectory(_rootFullPath);
        }
    }

    // ============ IFileSystemPlugin: Files ============

    public bool FileExists(string path) => File.Exists(SafeCombine(path));

    public string ReadFile(string path)
    {
        var full = SafeCombine(path);
        return File.ReadAllText(full);
    }

    public byte[] ReadFileBytes(string path)
    {
        var full = SafeCombine(path);
        return File.ReadAllBytes(full);
    }

    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        var full = SafeCombine(path, ensureParentDir: true);
        if (!overwrite && File.Exists(full))
            throw new IOException($"File already exists: {path}");

        File.WriteAllText(full, contents);
    }

    public void AppendToFile(string path, string contents)
    {
        var full = SafeCombine(path, ensureParentDir: true);
        File.AppendAllText(full, contents);
    }

    public void DeleteFile(string path)
    {
        var full = SafeCombine(path);
        if (File.Exists(full)) File.Delete(full);
    }

    // ============ IFileSystemPlugin: Directories ============

    public bool DirectoryExists(string path) => Directory.Exists(SafeCombine(path));

    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(SafeCombine(path));
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        var full = SafeCombine(path);
        if (Directory.Exists(full))
            Directory.Delete(full, recursive);
    }

    // ============ IFileSystemPlugin: Info & Listing ============

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var full = SafeCombine(directoryPath);
        if (!Directory.Exists(full))
            return Enumerable.Empty<string>();

        return Directory.GetFiles(full, searchPattern, searchOption)
            .Select(ToRelativeFromRoot);
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var full = SafeCombine(directoryPath);
        if (!Directory.Exists(full))
            return Enumerable.Empty<string>();

        return Directory.GetDirectories(full, searchPattern, searchOption)
            .Select(ToRelativeFromRoot);
    }

    public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
    {
        var full = SafeCombine(directoryPath);
        if (!Directory.Exists(full))
            return Enumerable.Empty<string>();

        var dirs = Directory.GetDirectories(full, searchPattern, searchOption)
            .Select(ToRelativeFromRoot);
        var files = Directory.GetFiles(full, searchPattern, searchOption)
            .Select(ToRelativeFromRoot);

        return dirs.Concat(files);
    }

    public long GetFileSize(string path)
    {
        var full = SafeCombine(path);
        return new FileInfo(full).Length;
    }

    // ============ Helper Methods ============

    protected string ToRelativeFromRoot(string fullPath)
    {
        var rel = Path.GetRelativePath(_rootFullPath, fullPath);
        return rel.Replace('\\', '/');
    }

    protected string SafeCombine(string relativePath, bool ensureParentDir = false)
    {
        // Handle root directory (empty, null, "/", or whitespace)
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == "/" || relativePath == "\\")
        {
            return _rootFullPath;
        }

        // Normalize separators
        relativePath = relativePath.Replace('\\', '/').TrimStart('/');

        // Disallow absolute paths (after trimming leading slash)
        if (Path.IsPathRooted(relativePath))
            throw new UnauthorizedAccessException("Absolute paths are not allowed.");

        // Combine & canonicalize
        var combined = Path.GetFullPath(Path.Combine(_rootFullPath, relativePath));

        // Ensure stays under root (prevents ../ traversal)
        var rootWithSep = _rootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootFullPath
            : _rootFullPath + Path.DirectorySeparatorChar;

        if (!combined.StartsWith(rootWithSep, StringComparison.Ordinal) &&
            !combined.Equals(_rootFullPath, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Path escapes the allowed root.");

        if (ensureParentDir)
        {
            var parent = Path.GetDirectoryName(combined);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
        }

        return combined;
    }

    // ============ IPlugin Implementation ============

    public override bool HasValidAuthorization() => true; // No authorization required for local filesystem

    public override Task InitializeAsync() => Task.CompletedTask;

    public override void Dispose()
    {
        // No resources to dispose
    }
}
