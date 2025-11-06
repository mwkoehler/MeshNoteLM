using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Mock interface for PdfCacheService to enable testing
/// </summary>
public interface IMockPdfCacheService
{
    byte[]? GetCachedPdf(string fileName);
    void CachePdf(string fileName, byte[] pdfData);
    void ClearCache();
    string? GetCachedPdfPath(string fileName);
    bool IsCached(string fileName);
}

/// <summary>
/// Mock implementation of PdfCacheService for testing
/// </summary>
public class MockPdfCacheService : IMockPdfCacheService
{
    private readonly Dictionary<string, byte[]> _cache = new();
    private readonly Dictionary<string, string> _cachePaths = new();

    public byte[]? GetCachedPdf(string fileName)
    {
        var cacheKey = GenerateCacheKey(fileName);
        _cache.TryGetValue(cacheKey, out var result);
        return result;
    }

    public void CachePdf(string fileName, byte[] pdfData)
    {
        var cacheKey = GenerateCacheKey(fileName);
        _cache[cacheKey] = pdfData;
        _cachePaths[cacheKey] = $"/mock/cache/{cacheKey}.pdf";
    }

    public void ClearCache()
    {
        _cache.Clear();
        _cachePaths.Clear();
    }

    public string? GetCachedPdfPath(string fileName)
    {
        var cacheKey = GenerateCacheKey(fileName);
        _cachePaths.TryGetValue(cacheKey, out var result);
        return result;
    }

    public bool IsCached(string fileName)
    {
        var cacheKey = GenerateCacheKey(fileName);
        return _cache.ContainsKey(cacheKey);
    }

    private string GenerateCacheKey(string fileName)
    {
        // Simple deterministic cache key generation for testing
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileName));
    }
}