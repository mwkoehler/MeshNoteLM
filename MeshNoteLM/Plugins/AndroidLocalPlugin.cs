/*
================================================================================
Android Local File System Plugin (C# / .NET MAUI or Xamarin.Android)
Implements: IFileSystemPlugin via LocalFileSystemPluginBase

What this does
- Safe read/write/delete + directory ops inside your Android app sandbox.
- Choose root: InternalFiles (Context.FilesDir), Cache (Context.CacheDir),
  or ExternalFiles (Context.GetExternalFilesDir(null), still app-scoped).

Android storage notes (API 29+)
- InternalFiles / Cache are always private to your app; no permissions needed.
- ExternalFiles is also app-scoped (no READ/WRITE_EXTERNAL_STORAGE required).
- If you need user-visible arbitrary locations (Downloads, SD cards, etc.),
  use the Storage Access Framework (SAF) — not covered here.

Usage
------------------------------------------------------------------------------
var plugin = new AndroidLocalFileSystemPlugin(
    root: AndroidLocalFileSystemPlugin.SpecialRoot.InternalFiles
);

plugin.CreateDirectory("notes");
plugin.WriteFile("notes/todo.txt", "Hello Android!", overwrite: true);
var exists = plugin.FileExists("notes/todo.txt");
================================================================================
*/

#nullable enable

using System;
using System.IO;

#if ANDROID
using Android.App;
#endif

namespace MeshNoteLM.Plugins;

public partial class AndroidLocalFileSystemPlugin : LocalFileSystemPluginBase
{
    public enum SpecialRoot { InternalFiles, Cache, ExternalFiles, Custom }

    public override string Name => "Android";
    public override string Version => "0.1";
    public override string Description => "Access for local Android files";
    public override string Author => "Starglass Technology";

    /// <summary>
    /// Create an Android-local filesystem plugin rooted inside the app sandbox.
    /// </summary>
    /// <param name="root">Which sandbox base directory to use.</param>
    /// <param name="customAbsoluteRoot">
    /// If <see cref="SpecialRoot.Custom"/> is used, provide an absolute path
    /// under the app sandbox (e.g., Path.Combine(GetInternalFilesPath(), "MyArea")).
    /// </param>
    public AndroidLocalFileSystemPlugin(
        SpecialRoot root = SpecialRoot.InternalFiles,
        string? customAbsoluteRoot = null)
        : base(ResolveRoot(root, customAbsoluteRoot))
    {
        IsEnabled = DeviceInfo.Current.Platform == DevicePlatform.Android;
        System.Diagnostics.Debug.WriteLine($"AndroidLocalFileSystemPlugin initialized with root: {_rootFullPath}, IsEnabled: {IsEnabled}");
    }

    // ============ Platform-Specific Root Resolution ============

    private static string ResolveRoot(SpecialRoot root, string? customAbsoluteRoot)
    {
        return root switch
        {
            SpecialRoot.InternalFiles => GetInternalFilesPath(),
            SpecialRoot.Cache => GetCachePath(),
            SpecialRoot.ExternalFiles => GetExternalFilesPath(),
            SpecialRoot.Custom when !string.IsNullOrWhiteSpace(customAbsoluteRoot) =>
                Path.GetFullPath(customAbsoluteRoot),
            SpecialRoot.Custom => throw new ArgumentException("Custom root requires a valid absolute path.", nameof(customAbsoluteRoot)),
            _ => GetInternalFilesPath()
        };
    }

    private static string GetInternalFilesPath()
    {
#if ANDROID
        try
        {
            var ctx = Android.App.Application.Context;
            if (ctx?.FilesDir?.AbsolutePath is string path)
            {
                System.Diagnostics.Debug.WriteLine($"Android FilesDir: {path}");
                return path;
            }

            var fallback = Path.Combine(AppContext.BaseDirectory, "FilesDir");
            System.Diagnostics.Debug.WriteLine($"Using fallback FilesDir: {fallback}");
            return fallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR getting InternalFilesPath: {ex}");
            return Path.Combine(AppContext.BaseDirectory, "FilesDir");
        }
#else
        return Path.Combine(AppContext.BaseDirectory, "FilesDir");
#endif
    }

    private static string GetCachePath()
    {
#if ANDROID
        var ctx = Android.App.Application.Context;
        return ctx?.CacheDir?.AbsolutePath
               ?? Path.Combine(AppContext.BaseDirectory, "CacheDir");
#else
        return Path.Combine(AppContext.BaseDirectory, "CacheDir");
#endif
    }

    private static string GetExternalFilesPath()
    {
#if ANDROID
        var ctx = Android.App.Application.Context;
        var dir = ctx?.GetExternalFilesDir(null);
        if (dir?.AbsolutePath is string path)
            return path;

        // Fallback to internal files if external is unavailable
        return GetInternalFilesPath();
#else
        return Path.Combine(AppContext.BaseDirectory, "ExternalFiles");
#endif
    }
}
