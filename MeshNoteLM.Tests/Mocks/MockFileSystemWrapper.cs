namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Mock file system abstraction for testing file operations
/// </summary>
public interface IFileSystemWrapper
{
    string GetAppDataDirectory();
    string GetCacheDirectory();
    bool FileExists(string path);
    byte[] ReadAllBytes(string path);
    void WriteAllBytes(string path, byte[] data);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void DeleteFile(string path);
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    void DeleteDirectory(string path, bool recursive);
}

/// <summary>
/// In-memory implementation for testing
/// </summary>
public class MockFileSystemWrapper : IFileSystemWrapper
{
    private readonly Dictionary<string, byte[]> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly string _appDataDirectory;
    private readonly string _cacheDirectory;

    public MockFileSystemWrapper()
    {
        _appDataDirectory = "/test/appdata";
        _cacheDirectory = "/test/cache";
        _directories.Add(_appDataDirectory);
        _directories.Add(_cacheDirectory);
    }

    public string GetAppDataDirectory() => _appDataDirectory;
    public string GetCacheDirectory() => _cacheDirectory;

    public bool FileExists(string path) => _files.ContainsKey(path);

    public byte[] ReadAllBytes(string path)
    {
        if (!_files.TryGetValue(path, out var data))
            throw new FileNotFoundException($"File not found: {path}");
        return data;
    }

    public void WriteAllBytes(string path, byte[] data)
    {
        _files[path] = data;
    }

    public string ReadAllText(string path)
    {
        var bytes = ReadAllBytes(path);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public void WriteAllText(string path, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        WriteAllBytes(path, bytes);
    }

    public void DeleteFile(string path)
    {
        _files.Remove(path);
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(path);
    }

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public void DeleteDirectory(string path, bool recursive)
    {
        if (recursive)
        {
            // Remove all files in directory
            var filesToRemove = _files.Keys.Where(k => k.StartsWith(path)).ToList();
            foreach (var file in filesToRemove)
            {
                _files.Remove(file);
            }
            // Remove subdirectories
            var dirsToRemove = _directories.Where(d => d.StartsWith(path)).ToList();
            foreach (var dir in dirsToRemove)
            {
                _directories.Remove(dir);
            }
        }
        _directories.Remove(path);
    }
}
