/*
================================================================================
Obsidian Vault Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin

What this does
- Adapts a local Obsidian vault directory to the IFileSystemPlugin interface
- Vault root folder maps to "/" (virtual root)
- Any file/dir inside the vault is reachable by relative POSIX-like path
- Optional ignore of ".obsidian/" internal settings folder
- Safe path resolution to prevent traversal outside the vault

Virtual filesystem structure
/                              - Vault root directory
/Daily/2025-09-11.md          - Example daily note
/Projects/Widget/README.md    - Example project note
/Assets/img/logo.png          - Example asset file

File operations
- ReadFile("/Daily/2025-09-11.md") → Returns note content
- WriteFile("/Inbox/Idea.md", "# New Idea") → Creates new note
- AppendToFile("/Daily/log.md", "\n- New entry") → Appends to note
- GetFiles("/Projects", "*.md") → Lists markdown notes

Usage
------------------------------------------------------------------------------
var vaultPath = @"C:\Users\you\Obsidian\MainVault";
var plugin = new ObsidianPlugin(vaultPath, ignoreObsidianSystemDir: true);

plugin.WriteFile("/Inbox/Idea.md", "# New Idea\n\n- point 1\n");
plugin.AppendToFile("/Daily/2025-09-11.md", "\n- Energy: steady ✅");

foreach (var p in plugin.GetFiles("/Projects", "*.md"))
    Console.WriteLine(p);

Security
------------------------------------------------------------------------------
- Path resolution prevents directory traversal (.. safety)
- All operations stay within vault boundaries
- UTF-8 encoding for reads/writes
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Plugins;

public class ObsidianPlugin : PluginBase, IFileSystemPlugin
{
    private readonly string _vaultRootPath;
    private readonly bool _ignoreObsidianSystemDir;
    private readonly bool _hasValidVault;

    public override string Name => "Obsidian";
    public override string Version => "0.1";
    public override string Description => "Obsidian vault filesystem access";
    public override string Author => "Starglass Technology";

    public ObsidianPlugin(string? vaultPath = null, bool ignoreObsidianSystemDir = true)
    {
        System.Diagnostics.Debug.WriteLine("=== ObsidianPlugin constructor START ===");

        // Check for configured vault path from: 1) parameter, 2) settings service, 3) environment variable
        string? resolvedPath = vaultPath;

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            try
            {
                var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
                resolvedPath = settingsService?.ObsidianVaultPath;
                System.Diagnostics.Debug.WriteLine($"[Obsidian] Checked settings service, got: {resolvedPath ?? "(null)"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Obsidian] Could not access settings service: {ex.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            resolvedPath = Environment.GetEnvironmentVariable("OBSIDIAN_VAULT_PATH");
            System.Diagnostics.Debug.WriteLine($"[Obsidian] Checked environment variable, got: {resolvedPath ?? "(null)"}");
        }

        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            System.Diagnostics.Debug.WriteLine("[Obsidian] No vault path configured (configure in Settings or set OBSIDIAN_VAULT_PATH environment variable)");
            _vaultRootPath = string.Empty;
            _ignoreObsidianSystemDir = ignoreObsidianSystemDir;
            _hasValidVault = false;
            System.Diagnostics.Debug.WriteLine("=== ObsidianPlugin constructor END (no vault configured) ===");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[Obsidian] Resolved vault path: {resolvedPath}");

        _vaultRootPath = Path.GetFullPath(resolvedPath);
        _ignoreObsidianSystemDir = ignoreObsidianSystemDir;

        System.Diagnostics.Debug.WriteLine($"[Obsidian] Full vault path: {_vaultRootPath}");

        if (!Directory.Exists(_vaultRootPath))
        {
            System.Diagnostics.Debug.WriteLine($"[Obsidian] Vault directory does not exist: {_vaultRootPath}");
            _hasValidVault = false;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[Obsidian] Vault directory exists: {_vaultRootPath}");
            _hasValidVault = true;
        }

        System.Diagnostics.Debug.WriteLine("=== ObsidianPlugin constructor END ===");
    }

    public override bool HasValidAuthorization()
    {
        return _hasValidVault;
    }

    // ---------------- IFileSystemPlugin: Files ----------------

    public bool FileExists(string path)
    {
        var full = SafeCombine(path);
        return File.Exists(full);
    }

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
        if (File.Exists(full))
            File.Delete(full);
    }

    // ------------- IFileSystemPlugin: Directories -------------

    public bool DirectoryExists(string path)
    {
        var full = SafeCombine(path);
        return Directory.Exists(full);
    }

    public void CreateDirectory(string path)
    {
        var full = SafeCombine(path);
        Directory.CreateDirectory(full);
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        var full = SafeCombine(path);
        if (Directory.Exists(full))
            Directory.Delete(full, recursive);
    }

    // ------------- IFileSystemPlugin: Info & Listing ----------

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        System.Diagnostics.Debug.WriteLine($"[Obsidian] GetFiles called: directoryPath='{directoryPath}', pattern='{searchPattern}'");
        var dir = SafeCombine(directoryPath);
        System.Diagnostics.Debug.WriteLine($"[Obsidian] SafeCombine result: '{dir}'");

        if (!Directory.Exists(dir))
        {
            System.Diagnostics.Debug.WriteLine($"[Obsidian] Directory does not exist: '{dir}'");
            return [];
        }

        var files = Directory.EnumerateFiles(dir, searchPattern, searchOption)
                        .Where(f => !ShouldIgnore(f))
                        .Select(ToRelativeFromRoot)
                        .ToList();
        System.Diagnostics.Debug.WriteLine($"[Obsidian] Found {files.Count} files");
        return files;
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        System.Diagnostics.Debug.WriteLine($"[Obsidian] GetDirectories called: directoryPath='{directoryPath}', pattern='{searchPattern}'");
        var dir = SafeCombine(directoryPath);
        System.Diagnostics.Debug.WriteLine($"[Obsidian] SafeCombine result: '{dir}'");

        if (!Directory.Exists(dir))
        {
            System.Diagnostics.Debug.WriteLine($"[Obsidian] Directory does not exist: '{dir}'");
            return [];
        }

        var directories = Directory.EnumerateDirectories(dir, searchPattern, searchOption)
                        .Where(d => !ShouldIgnore(d))
                        .Select(ToRelativeFromRoot)
                        .ToList();
        System.Diagnostics.Debug.WriteLine($"[Obsidian] Found {directories.Count} directories");
        return directories;
    }

    public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in GetDirectories(directoryPath, searchPattern, searchOption))
            if (seen.Add(d))
                yield return d;

        foreach (var f in GetFiles(directoryPath, searchPattern, searchOption))
            if (seen.Add(f))
                yield return f;
    }

    public long GetFileSize(string path)
    {
        var full = SafeCombine(path);
        return new FileInfo(full).Length;
    }

    // ------------------------ Helpers -------------------------

    private string ToRelativeFromRoot(string fullPath)
    {
        var rel = Path.GetRelativePath(_vaultRootPath, fullPath);
        return "/" + rel.Replace('\\', '/');
    }

    private string SafeCombine(string relativePath, bool ensureParentDir = false)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            relativePath = "/";

        relativePath = relativePath.Replace('\\', '/').TrimStart('/');

        var combined = Path.GetFullPath(Path.Combine(_vaultRootPath, relativePath));

        // Normalize both paths for comparison
        var normalizedRoot = Path.GetFullPath(_vaultRootPath);
        var normalizedCombined = Path.GetFullPath(combined);

        // Check if combined path is the root itself or under the root
        if (!normalizedCombined.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
            !normalizedCombined.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Path escapes the vault root.");
        }

        if (ensureParentDir)
        {
            var parent = Path.GetDirectoryName(combined);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
        }

        return combined;
    }

    private bool ShouldIgnore(string fullPath)
    {
        if (!_ignoreObsidianSystemDir)
            return false;

        var name = Path.GetFileName(fullPath);
        return name == ".obsidian" || name == ".trash";
    }

    public override Task InitializeAsync() => Task.CompletedTask;

    public override void Dispose()
    {
        // No resources to dispose
    }
}
