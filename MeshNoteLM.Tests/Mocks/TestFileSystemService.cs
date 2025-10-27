using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Test implementation of file system service that uses temporary directories
/// </summary>
public class TestFileSystemService : IFileSystemService, IDisposable
{
    private readonly string _testAppDataDir;
    private readonly string _testCacheDir;

    public TestFileSystemService()
    {
        // Create unique temp directories for each test instance
        var testId = Guid.NewGuid().ToString("N");
        _testAppDataDir = Path.Combine(Path.GetTempPath(), "MeshNoteLM_Test_AppData_" + testId);
        _testCacheDir = Path.Combine(Path.GetTempPath(), "MeshNoteLM_Test_Cache_" + testId);

        Directory.CreateDirectory(_testAppDataDir);
        Directory.CreateDirectory(_testCacheDir);
    }

    public string AppDataDirectory => _testAppDataDir;
    public string CacheDirectory => _testCacheDir;

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testAppDataDir))
                Directory.Delete(_testAppDataDir, recursive: true);
            if (Directory.Exists(_testCacheDir))
                Directory.Delete(_testCacheDir, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
