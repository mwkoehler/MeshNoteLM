namespace MeshNoteLM.Interfaces;

/// <summary>
/// Abstraction for file system operations to enable testing
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Gets the application's data directory path
    /// </summary>
    string AppDataDirectory { get; }

    /// <summary>
    /// Gets the application's cache directory path
    /// </summary>
    string CacheDirectory { get; }
}
