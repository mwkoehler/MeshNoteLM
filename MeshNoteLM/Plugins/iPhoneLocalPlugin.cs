/*
================================================================================
iOS Local File System Plugin (C# / .NET MAUI or Xamarin.iOS)
Implements: IFileSystemPlugin via LocalFileSystemPluginBase

What this does
- Provides safe read/write/delete and directory ops inside your iOS app sandbox.
- Defaults root to the app's Documents folder but can be pointed at Library,
  Caches, or a custom subfolder within the sandbox.
- Optionally applies iOS data protection attributes (NSFileProtection*).

iOS sandbox paths (typical)
- Documents:      .../Containers/Data/Application/<GUID>/Documents
- Library:        .../Library
- Library/Caches: .../Library/Caches
- tmp:            .../tmp

Usage
------------------------------------------------------------------------------
var plugin = new IOSLocalFileSystemPlugin(
    root: IOSLocalFileSystemPlugin.SpecialRoot.Documents,
    applyFileProtection: true,
    protection: IOSLocalFileSystemPlugin.ProtectionLevel.Complete
);

plugin.CreateDirectory("notes");
plugin.WriteFile("notes/session1.txt", "Hello iPhone!", overwrite: true);
================================================================================
*/

#nullable enable

using System;
using System.IO;
using System.Linq;

#if IOS
using Foundation;
#endif

namespace MeshNoteLM.Plugins;

public class IOSLocalFileSystemPlugin : LocalFileSystemPluginBase
{
    public enum SpecialRoot { Documents, Library, Caches, Temporary, Custom }
    public enum ProtectionLevel
    {
        None,
#if IOS
        Complete,
        CompleteUnlessOpen,
        CompleteUntilFirstUserAuthentication
#endif
    }

    private readonly bool _applyFileProtection;
    private readonly ProtectionLevel _protection;

    public override string Name => "iPhone Local Files";
    public override string Version => "0.1";
    public override string Description => "Access for local iPhone files";
    public override string Author => "Starglass Technology";

    /// <summary>
    /// Create an iOS-local filesystem plugin rooted inside the app sandbox.
    /// </summary>
    /// <param name="root">Which sandbox base directory to use.</param>
    /// <param name="customAbsoluteRoot">
    /// If <see cref="SpecialRoot.Custom"/> is used, provide an absolute path
    /// under the sandbox (e.g., Path.Combine(DocumentsPath, "MyArea")).
    /// </param>
    /// <param name="applyFileProtection">Apply iOS file-protection attributes when writing.</param>
    /// <param name="protection">The protection level to apply (ignored if not iOS).</param>
    public IOSLocalFileSystemPlugin(
        SpecialRoot root = SpecialRoot.Documents,
        string? customAbsoluteRoot = null,
        bool applyFileProtection = false,
        ProtectionLevel protection = ProtectionLevel.None)
        : base(ResolveRoot(root, customAbsoluteRoot))
    {
        IsEnabled = DeviceInfo.Current.Platform == DevicePlatform.iOS;
        _applyFileProtection = applyFileProtection;
        _protection = protection;
        System.Diagnostics.Debug.WriteLine($"IOSLocalFileSystemPlugin initialized with root: {_rootFullPath}, IsEnabled: {IsEnabled}");
    }

    // ============ Override Write Methods for iOS File Protection ============

    public new void WriteFile(string path, string contents, bool overwrite = true)
    {
        base.WriteFile(path, contents, overwrite);
        ApplyProtectionIfRequested(SafeCombine(path));
    }

    public new void AppendToFile(string path, string contents)
    {
        base.AppendToFile(path, contents);
        ApplyProtectionIfRequested(SafeCombine(path));
    }

    // ============ Platform-Specific Root Resolution ============

    private static string ResolveRoot(SpecialRoot root, string? customAbsoluteRoot)
    {
        return root switch
        {
            SpecialRoot.Documents => GetDocumentsPath(),
            SpecialRoot.Library => GetLibraryPath(),
            SpecialRoot.Caches => GetCachesPath(),
            SpecialRoot.Temporary => GetTempPath(),
            SpecialRoot.Custom when !string.IsNullOrWhiteSpace(customAbsoluteRoot) =>
                Path.GetFullPath(customAbsoluteRoot),
            SpecialRoot.Custom => throw new ArgumentException("Custom root requires a valid absolute path.", nameof(customAbsoluteRoot)),
            _ => GetDocumentsPath()
        };
    }

    private static string GetDocumentsPath()
    {
#if IOS
        var urls = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.DocumentDirectory, NSSearchPathDomain.User);
        var path = urls.LastOrDefault()?.Path ?? throw new InvalidOperationException("Documents path could not be resolved.");
        return path;
#else
        return Path.Combine(AppContext.BaseDirectory, "Documents");
#endif
    }

    private static string GetLibraryPath()
    {
#if IOS
        var urls = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.LibraryDirectory, NSSearchPathDomain.User);
        var path = urls.LastOrDefault()?.Path ?? throw new InvalidOperationException("Library path could not be resolved.");
        return path;
#else
        return Path.Combine(AppContext.BaseDirectory, "Library");
#endif
    }

    private static string GetCachesPath()
    {
#if IOS
        var urls = NSFileManager.DefaultManager.GetUrls(NSSearchPathDirectory.CachesDirectory, NSSearchPathDomain.User);
        var path = urls.LastOrDefault()?.Path ?? throw new InvalidOperationException("Caches path could not be resolved.");
        return path;
#else
        return Path.Combine(AppContext.BaseDirectory, "Caches");
#endif
    }

    private static string GetTempPath()
    {
        return Path.GetTempPath(); // Maps to sandboxed tmp on iOS
    }

    // ============ iOS File Protection ============

#if IOS
    private static NSString MapProtection(ProtectionLevel level)
    {
        return level switch
        {
            ProtectionLevel.Complete =>
                new NSString("NSFileProtectionComplete"),
            ProtectionLevel.CompleteUnlessOpen =>
                new NSString("NSFileProtectionCompleteUnlessOpen"),
            ProtectionLevel.CompleteUntilFirstUserAuthentication =>
                new NSString("NSFileProtectionCompleteUntilFirstUserAuthentication"),
            _ => new NSString("NSFileProtectionNone")
        };
    }
#endif

    private void ApplyProtectionIfRequested(string fullPath)
    {
#if IOS
        if (!_applyFileProtection || _protection == ProtectionLevel.None) return;

        var protection = MapProtection(_protection);
        using var attributes = NSDictionary.FromObjectAndKey(protection, NSFileManager.FileProtectionKey);
        NSFileManager.DefaultManager.SetAttributes(attributes, fullPath, out var error);

        if (error is not null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[IOSLocalFileSystemPlugin] Failed to set protection: {error.LocalizedDescription}");
        }
#endif
    }
}
