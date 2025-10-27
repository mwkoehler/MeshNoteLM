using System;
using System.IO;
using System.Threading.Tasks;
using MeshNoteLM.Interfaces;
using MeshNoteLM.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Services;

public class MicrosoftGraphTests
{
    private readonly Mock<IMicrosoftAuthService> _mockAuthService;
    private readonly Mock<PdfCacheService> _mockCacheService;
    private readonly MicrosoftGraphOfficeConverter _converter = null!;

    public MicrosoftGraphTests()
    {
        _mockAuthService = new Mock<IMicrosoftAuthService>();
        _mockCacheService = new Mock<PdfCacheService>();
        _converter = new MicrosoftGraphOfficeConverter(_mockAuthService.Object, _mockCacheService.Object);
    }

    // MicrosoftGraphOfficeConverter doesn't implement IDisposable, so no dispose needed

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act
        var converter = new MicrosoftGraphOfficeConverter(_mockAuthService.Object, _mockCacheService.Object);

        // Assert
        converter.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenAuthServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MicrosoftGraphOfficeConverter(null!, _mockCacheService.Object));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenCacheServiceIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MicrosoftGraphOfficeConverter(_mockAuthService.Object, null!));
    }

    [Fact]
    public void IsAvailable_ShouldReturnTrue_WhenUserIsAuthenticated()
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);

        // Act
        var result = _converter.IsAvailable;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsAvailable_ShouldReturnFalse_WhenUserIsNotAuthenticated()
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(false);

        // Act
        var result = _converter.IsAvailable;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void UnavailableMessage_ShouldReturnCorrectMessage()
    {
        // Act
        var result = _converter.UnavailableMessage;

        // Assert
        result.Should().Be("Sign in with Microsoft 365 to view Office documents, or open in external app");
    }

    [Theory]
    [InlineData("test.docx")]
    [InlineData("document.xlsx")]
    [InlineData("presentation.pptx")]
    [InlineData("empty.doc")]
    public async Task ConvertToPdfAsync_ShouldReturnNull_WhenNotAuthenticated(string fileName)
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(false);

        // Act
        var result = await _converter.ConvertToPdfAsync([0], fileName);

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("test.docx")]
    [InlineData("document.xlsx")]
    [InlineData("presentation.pptx")]
    public async Task ConvertToPdfAsync_ShouldCheckCache_WhenAvailable(string fileName)
    {
        // Arrange
        var expectedPdf = new byte[] { 10, 11, 12 };
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync("valid_token");
        _mockCacheService.Setup(c => c.GetCachedPdf(fileName)).Returns(expectedPdf);

        // Act
        var result = await _converter.ConvertToPdfAsync([0], fileName);

        // Assert
        result.Should().BeEquivalentTo(expectedPdf);
        _mockCacheService.Verify(c => c.GetCachedPdf(fileName), Times.Once);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, "test.docx")]
    [InlineData(new byte[] { 4, 5, 6 }, "document.xlsx")]
    public async Task ConvertToPdfAsync_ShouldBypassCache_WhenNotAvailable(byte[] officeData, string fileName)
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync("valid_token");
        _mockCacheService.Setup(c => c.GetCachedPdf(fileName)).Returns((byte[]?)null);

        // Mock conversion success (this would normally call Microsoft Graph API)
        // Since we can't easily mock the actual Graph API call in this test,
        // we'll verify the method completes without throwing and cache is checked

        // Act
        var result = await _converter.ConvertToPdfAsync(officeData, fileName);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCountGreaterThan(0); // Some PDF data should be returned
        _mockCacheService.Verify(c => c.GetCachedPdf(fileName), Times.Once);
    }

    [Theory]
    [InlineData("test.docx")]
    [InlineData("document.xlsx")]
    [InlineData("presentation.pptx")]
    [InlineData("document.doc")]
    public async Task ConvertToPdfAsync_ShouldHandleEmptyData(string fileName)
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync("valid_token");
        var emptyData = Array.Empty<byte>();

        // Act
        var result = await _converter.ConvertToPdfAsync(emptyData, fileName);

        // Assert
        result.Should().NotBeNull();
    }

    [Theory]
    [InlineData("test.docx")]
    [InlineData("document.xlsx")]
    [InlineData("presentation.pptx")]
    public void GetCachedPdf_ShouldReturnCorrectCacheKey(string fileName)
    {
        // Act
        var cacheKey = PdfCacheService.GenerateCacheKey(fileName);

        // Assert
        cacheKey.Should().Contain(fileName);
        // Should be deterministic
        var sameDataKey = PdfCacheService.GenerateCacheKey(fileName);
        sameDataKey.Should().Be(cacheKey);
    }

    [Fact]
    public void GetCachedPdf_ShouldGenerateDifferentKeys_ForDifferentFiles()
    {
        // Arrange
        const string fileName = "test.docx";

        // Act
        var key1 = PdfCacheService.GenerateCacheKey(fileName);
        var key2 = PdfCacheService.GenerateCacheKey(fileName);

        // Assert
        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GetCachedPdf_ShouldGenerateSameKey_ForSameData()
    {
        // Arrange
        const string fileName = "test.docx";

        // Act
        var key1 = PdfCacheService.GenerateCacheKey(fileName);
        var key2 = PdfCacheService.GenerateCacheKey(fileName);

        // Assert
        key1.Should().Be(key2);
    }

    [Fact]
    public void GetCachedPdf_ShouldGenerateDifferentKeys_ForDifferentFileNames()
    {
        // Arrange
        const string fileName1 = "test.docx";
        const string fileName2 = "document.docx";

        // Act
        var key1 = PdfCacheService.GenerateCacheKey(fileName1);
        var key2 = PdfCacheService.GenerateCacheKey(fileName2);

        // Assert
        key1.Should().NotBe(key2);
        key1.Should().Contain(fileName1);
        key2.Should().Contain(fileName2);
    }

    [Theory]
    [InlineData("test.docx", true)]
    [InlineData("document.xlsx", true)]
    [InlineData("presentation.pptx", true)]
    [InlineData("unknown.xyz", false)]
    // [InlineData(Array.Empty<byte>(), "empty.docx", false)] // TODO: Fix constant expression issue
    public void IsOfficeDocument_ShouldIdentifyOfficeFiles_OriginalTest(string fileName, bool expected)
    {
        // Act
        var result = MicrosoftGraphOfficeConverter.IsOfficeDocument(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".docx", true)]
    [InlineData(".doc", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".xls", true)]
    [InlineData(".pptx", true)]
    [InlineData(".ppt", true)]
    [InlineData(".pdf", false)]
    [InlineData(".txt", false)]
    [InlineData(".jpg", false)]
    [InlineData("", false)]
    public void HasOfficeExtension_ShouldIdentifyOfficeExtensions(string extension, bool expected)
    {
        // Act
        var result = MicrosoftGraphOfficeConverter.HasOfficeExtension(extension);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetFileExtension_ShouldExtractCorrectExtension()
    {
        // Arrange & Act
        var docxExt = MicrosoftGraphOfficeConverter.GetFileExtension("document.docx");
        var xlsxExt = MicrosoftGraphOfficeConverter.GetFileExtension("spreadsheet.xlsx");
        var noExt = MicrosoftGraphOfficeConverter.GetFileExtension("filename");

        // Assert
        docxExt.Should().Be(".docx");
        xlsxExt.Should().Be(".xlsx");
        noExt.Should().BeEmpty();
    }

    [Fact]
    public void GetFileSize_ShouldReturnCorrectSize()
    {
        // Arrange
        var smallData = new byte[] { 1, 2, 3 };
        var largeData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
        var emptyData = Array.Empty<byte>();

        // Act
        var smallSize = MicrosoftGraphOfficeConverter.GetFileSize(smallData);
        var largeSize = MicrosoftGraphOfficeConverter.GetFileSize(largeData);
        var emptySize = MicrosoftGraphOfficeConverter.GetFileSize(emptyData);

        // Assert
        smallSize.Should().Be(3);
        largeSize.Should().Be(10);
        emptySize.Should().Be(0);
    }

    [Fact]
    public void FormatFileSize_ShouldFormatCorrectly()
    {
        // Arrange
        var bytes1 = 1024L; // 1 KB
        var bytes2 = 1048576L; // 1 MB
        var bytes3 = 1073741824L; // 1 GB
        var bytes4 = 1099511627776L; // 1 TB

        // Act
        var size1 = MicrosoftGraphOfficeConverter.FormatFileSize(bytes1);
        var size2 = MicrosoftGraphOfficeConverter.FormatFileSize(bytes2);
        var size3 = MicrosoftGraphOfficeConverter.FormatFileSize(bytes3);
        var size4 = MicrosoftGraphOfficeConverter.FormatFileSize(bytes4);

        // Assert
        size1.Should().Be("1 KB");
        size2.Should().Be("1 MB");
        size3.Should().Be("1 GB");
        size4.Should().Be("1 TB");
    }

    [Fact]
    public async Task ConvertToPdfAsync_ShouldReturnNull_WhenTokenFails()
    {
        // Arrange
        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync((string?)null);

        // Act
        var result = await _converter.ConvertToPdfAsync([1, 2, 3], "test.docx");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ConvertToPdfAsync_ShouldCacheResult_WhenConversionSucceeds()
    {
        // Arrange
        var officeData = new byte[] { 1, 2, 3 };
        var fileName = "test.docx";
        var expectedPdf = new byte[] { 10, 11, 12 };

        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync("valid_token");

        // Act
        var result = await _converter.ConvertToPdfAsync(officeData, fileName);

        // Assert
        result.Should().NotBeNull();
        _mockCacheService.Verify(c => c.CachePdf(fileName, It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task ConvertToPdfAsync_ShouldNotCache_WhenConversionFails()
    {
        // Arrange
        var officeData = new byte[] { 1, 2, 3 };
        var fileName = "test.docx";

        _mockAuthService.Setup(a => a.IsAuthenticated).Returns(true);
        _mockAuthService.Setup(a => a.GetAccessTokenAsync()).ReturnsAsync((string?)null);

        // Act
        var result = await _converter.ConvertToPdfAsync(officeData, fileName);

        // Assert
        result.Should().BeNull();
        _mockCacheService.Verify(c => c.CachePdf(fileName, It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public void CacheKeyGeneration_ShouldBeDeterministic()
    {
        // Arrange
        const string fileName = "test.docx";

        // Act
        var key1 = PdfCacheService.GenerateCacheKey(fileName);
        var key2 = PdfCacheService.GenerateCacheKey(fileName);

        // Assert
        key1.Should().Be(key2);
    }

    [Theory]
    [InlineData(0L, "0 bytes")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1048576L, "1 MB")]
    [InlineData(1073741824L, "1 GB")]
    public void FormatFileSize_ShouldHandleEdgeCases(long bytes, string expected)
    {
        // Act
        var result = MicrosoftGraphOfficeConverter.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.docx", true)]
    [InlineData("DOCUMENT.DOCX", true)]
    [InlineData("test.PPTX", true)]
    [InlineData("image.JPG", false)]
    [InlineData("unknown", false)]
    public void IsOfficeDocument_ShouldBeCaseInsensitive(string fileName, bool expected)
    {
        // Act
        var result = MicrosoftGraphOfficeConverter.IsOfficeDocument(fileName);

        // Assert
        result.Should().Be(expected);
    }

    // MicrosoftGraphOfficeConverter doesn't implement IDisposable, so no dispose tests needed
}
