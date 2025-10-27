using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Services;

/// <summary>
/// MAUI implementation of file system service
/// </summary>
public class MauiFileSystemService : IFileSystemService
{
    public string AppDataDirectory => FileSystem.AppDataDirectory;
    public string CacheDirectory => FileSystem.CacheDirectory;
}
