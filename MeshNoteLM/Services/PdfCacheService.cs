using MeshNoteLM.Interfaces;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace MeshNoteLM.Services;

/// <summary>
/// Service for caching converted PDFs to avoid re-conversion
/// </summary>
public class PdfCacheService
{
    private readonly ConcurrentDictionary<string, byte[]> _memoryCache = new();
    private readonly string _cacheDirectory;
    private const int MaxMemoryCacheSize = 10; // Keep last 10 PDFs in memory
    private readonly Queue<string> _cacheKeys = new();

    public PdfCacheService(IFileSystemService fileSystem)
    {
        // Use app data directory for disk cache
        _cacheDirectory = Path.Combine(fileSystem.CacheDirectory, "pdf_cache");
        Directory.CreateDirectory(_cacheDirectory);

        Debug.WriteLine($"[PdfCacheService] Cache directory: {_cacheDirectory}");
    }

    /// <summary>
    /// Gets a cached PDF if available
    /// </summary>
    public byte[]? GetCachedPdf(string fileName)
    {
        var cacheKey = GenerateCacheKey(fileName);

        // Try memory cache first
        if (_memoryCache.TryGetValue(cacheKey, out var cachedPdf))
        {
            Debug.WriteLine($"[PdfCacheService] Memory cache HIT for {fileName}");
            return cachedPdf;
        }

        // Try disk cache
        var diskCachePath = GetDiskCachePath(cacheKey);
        if (File.Exists(diskCachePath))
        {
            try
            {
                var pdfData = File.ReadAllBytes(diskCachePath);
                Debug.WriteLine($"[PdfCacheService] Disk cache HIT for {fileName} ({pdfData.Length} bytes)");

                // Promote to memory cache
                AddToMemoryCache(cacheKey, pdfData);

                return pdfData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PdfCacheService] Error reading disk cache: {ex.Message}");
            }
        }

        Debug.WriteLine($"[PdfCacheService] Cache MISS for {fileName}");
        return null;
    }

    /// <summary>
    /// Caches a converted PDF
    /// </summary>
    public void CachePdf(string fileName, byte[] pdfData)
    {
        var cacheKey = GenerateCacheKey(fileName);

        // Add to memory cache
        AddToMemoryCache(cacheKey, pdfData);

        // Save to disk cache
        try
        {
            var diskCachePath = GetDiskCachePath(cacheKey);
            File.WriteAllBytes(diskCachePath, pdfData);
            Debug.WriteLine($"[PdfCacheService] Cached PDF to disk: {fileName} ({pdfData.Length} bytes)");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PdfCacheService] Error writing disk cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all caches
    /// </summary>
    public void ClearCache()
    {
        _memoryCache.Clear();
        _cacheKeys.Clear();

        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                Directory.Delete(_cacheDirectory, recursive: true);
                Directory.CreateDirectory(_cacheDirectory);
            }
            Debug.WriteLine("[PdfCacheService] Cache cleared");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PdfCacheService] Error clearing cache: {ex.Message}");
        }
    }

    private void AddToMemoryCache(string cacheKey, byte[] pdfData)
    {
        // Add to memory cache
        _memoryCache[cacheKey] = pdfData;
        _cacheKeys.Enqueue(cacheKey);

        // Evict oldest if cache is full
        if (_cacheKeys.Count > MaxMemoryCacheSize)
        {
            var oldestKey = _cacheKeys.Dequeue();
            _memoryCache.TryRemove(oldestKey, out _);
            Debug.WriteLine($"[PdfCacheService] Evicted oldest entry from memory cache");
        }
    }

    public static string GenerateCacheKey(string fileName)
    {
        // Generate a hash of the file content + file name
        using var sha256 = SHA256.Create();
        // var fileHash = sha256.ComputeHash(fileData);
        var nameHash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(fileName));

        // Combine hashes
        // var combinedHash = new byte[fileHash.Length + nameHash.Length];
        // Buffer.BlockCopy(nameHash, 0, nameHash, 0, fileHash.Length);
        // Buffer.BlockCopy(nameHash, 0, nameHash, fileHash.Length, nameHash.Length);

        return Convert.ToHexString(nameHash);
    }

    private string GetDiskCachePath(string cacheKey)
    {
        return Path.Combine(_cacheDirectory, $"{cacheKey}.pdf");
    }
}
