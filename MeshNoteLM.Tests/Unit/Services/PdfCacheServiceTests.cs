using MeshNoteLM.Services;
using MeshNoteLM.Tests.Mocks;
using FluentAssertions;
using System.Text;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class PdfCacheServiceTests : IDisposable
{
    private readonly TestFileSystemService _fileSystem;
    private readonly PdfCacheService _cacheService;

    public PdfCacheServiceTests()
    {
        _fileSystem = new TestFileSystemService();
        _cacheService = new PdfCacheService(_fileSystem);
    }

    [Fact]
    public void Constructor_ShouldCreateCacheDirectory()
    {
        // Assert
        var expectedCacheDir = Path.Combine(_fileSystem.CacheDirectory, "pdf_cache");
        Directory.Exists(expectedCacheDir).Should().BeTrue();
    }

    [Fact]
    public void GetCachedPdf_ShouldReturnNull_WhenNoCacheExists()
    {
        // Arrange
        var fileName = "test.docx";

        // Act
        var result = _cacheService.GetCachedPdf(fileName);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void CachePdf_AndRetrieve_ShouldReturnSamePdfData()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content here");

        // Act
        _cacheService.CachePdf(fileName, pdfData);
        var retrieved = _cacheService.GetCachedPdf(fileName);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(pdfData);
    }

    [Fact]
    public void GetCachedPdf_ShouldReturnCachedData_WhenSameFileNameIsUsed()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content");

        // Act
        _cacheService.CachePdf(fileName, pdfData);
        var retrieved = _cacheService.GetCachedPdf(fileName);

        // Assert - Should return cached data since cache key is based on filename only
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(pdfData);
    }

    [Fact]
    public void GetCachedPdf_ShouldReturnNull_WhenFileNameChanges()
    {
        // Arrange
        var fileName1 = "test1.docx";
        var fileName2 = "test2.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content");

        // Act
        _cacheService.CachePdf(fileName1, pdfData);
        var retrieved = _cacheService.GetCachedPdf(fileName2);

        // Assert
        retrieved.Should().BeNull("file name changed");
    }

    [Fact]
    public void CachePdf_ShouldHandleMultipleFiles()
    {
        // Arrange
        var pdf1Data = Encoding.UTF8.GetBytes("PDF 1");
        var pdf2Data = Encoding.UTF8.GetBytes("PDF 2");
        var pdf3Data = Encoding.UTF8.GetBytes("PDF 3");

        // Act
        _cacheService.CachePdf("file1.docx", pdf1Data);
        _cacheService.CachePdf("file2.docx", pdf2Data);
        _cacheService.CachePdf("file3.docx", pdf3Data);

        // Assert
        var retrieved1 = _cacheService.GetCachedPdf("file1.docx");
        var retrieved2 = _cacheService.GetCachedPdf("file2.docx");
        var retrieved3 = _cacheService.GetCachedPdf("file3.docx");

        retrieved1.Should().BeEquivalentTo(pdf1Data);
        retrieved2.Should().BeEquivalentTo(pdf2Data);
        retrieved3.Should().BeEquivalentTo(pdf3Data);
    }

    [Fact]
    public void ClearCache_ShouldRemoveAllCachedItems()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content");
        _cacheService.CachePdf(fileName, pdfData);

        // Act
        _cacheService.ClearCache();
        var retrieved = _cacheService.GetCachedPdf(fileName);

        // Assert
        retrieved.Should().BeNull("cache was cleared");
    }

    [Fact]
    public void CachePdf_ShouldOverwriteExisting_WhenSameFileIsCachedTwice()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData1 = Encoding.UTF8.GetBytes("PDF version 1");
        var pdfData2 = Encoding.UTF8.GetBytes("PDF version 2");

        // Act
        _cacheService.CachePdf(fileName, pdfData1);
        _cacheService.CachePdf(fileName, pdfData2);
        var retrieved = _cacheService.GetCachedPdf(fileName);

        // Assert
        retrieved.Should().BeEquivalentTo(pdfData2, "latest version should be cached");
    }

    [Fact]
    public void CachePdf_ShouldHandleLargeFiles()
    {
        // Arrange
        var largeFileData = new byte[10 * 1024 * 1024]; // 10 MB
        new Random(42).NextBytes(largeFileData);
        var largePdfData = new byte[5 * 1024 * 1024]; // 5 MB
        new Random(123).NextBytes(largePdfData);
        var fileName = "large.docx";

        // Act
        _cacheService.CachePdf(fileName, largePdfData);
        var retrieved = _cacheService.GetCachedPdf(fileName);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Should().BeEquivalentTo(largePdfData);
    }

    [Fact]
    public void Cache_ShouldPersistToDisk()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content");

        // Act
        _cacheService.CachePdf(fileName, pdfData);

        // Assert - Check disk cache
        var cacheDir = Path.Combine(_fileSystem.CacheDirectory, "pdf_cache");
        var cacheFiles = Directory.GetFiles(cacheDir, "*.pdf");
        cacheFiles.Should().HaveCountGreaterThan(0, "PDF should be cached to disk");
    }

    [Fact]
    public void GetCachedPdf_ShouldLoadFromDisk_WhenNotInMemory()
    {
        // Arrange
        var fileName = "test.docx";
        var pdfData = Encoding.UTF8.GetBytes("PDF content");

        // Cache with first service instance
        _cacheService.CachePdf(fileName, pdfData);

        // Create new service instance (different memory cache, same disk)
        var newCacheService = new PdfCacheService(_fileSystem);

        // Act - Should load from disk
        var retrieved = newCacheService.GetCachedPdf(fileName);

        // Assert
        retrieved.Should().NotBeNull("should load from disk cache");
        retrieved.Should().BeEquivalentTo(pdfData);
    }

    public void Dispose()
    {
        _fileSystem?.Dispose();
        GC.SuppressFinalize(this);
    }
}
