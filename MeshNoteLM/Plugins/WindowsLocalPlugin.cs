/*
================================================================================
Windows Local File System Plugin (C# / .NET MAUI)
Implements: IFileSystemPlugin via LocalFileSystemPluginBase

What this does
- Safe read/write/delete + directory ops in Windows user profile folder.
- Defaults to user profile root (C:\Users\YourName).
- Custom root: Optionally specify any directory path

Usage
------------------------------------------------------------------------------
// Use default user profile folder
var plugin = new WindowsLocalFileSystemPlugin();

// Or specify custom root
var plugin = new WindowsLocalFileSystemPlugin(@"D:\MyFiles");

plugin.CreateDirectory("notes");
plugin.WriteFile("notes/todo.txt", "Hello Windows!", overwrite: true);
================================================================================
*/

#nullable enable

using System;
using System.IO;

namespace MeshNoteLM.Plugins;

public class WindowsLocalFileSystemPlugin : LocalFileSystemPluginBase
{
    public override string Name => "Windows Local";
    public override string Version => "0.1";
    public override string Description => "Access for local Windows files";
    public override string Author => "Starglass Technology";

    /// <summary>
    /// Create a Windows-local filesystem plugin rooted in user profile directory.
    /// </summary>
    /// <param name="customRoot">
    /// Optional custom root path. If not provided, uses user profile folder (C:\Users\YourName).
    /// </param>
    public WindowsLocalFileSystemPlugin(string? customRoot = null)
        : base(customRoot ?? GetUserProfilePath())
    {
        try
        {
            IsEnabled = DeviceInfo.Current.Platform == DevicePlatform.WinUI;
            System.Diagnostics.Debug.WriteLine($"WindowsLocalFileSystemPlugin initialized with root: {_rootFullPath}, IsEnabled: {IsEnabled}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WARNING: Could not check platform: {ex.Message}");
            IsEnabled = true; // Assume Windows if we can't check
        }
    }

    // ============ Platform-Specific Root Resolution ============

    private static string GetUserProfilePath()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                System.Diagnostics.Debug.WriteLine($"User profile path: {userProfile}");
                return userProfile;
            }

            var fallback = Path.Combine(AppContext.BaseDirectory, "UserProfile");
            System.Diagnostics.Debug.WriteLine($"Using fallback UserProfile: {fallback}");
            return fallback;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR getting UserProfile: {ex}");
            return Path.Combine(AppContext.BaseDirectory, "UserProfile");
        }
    }
}
