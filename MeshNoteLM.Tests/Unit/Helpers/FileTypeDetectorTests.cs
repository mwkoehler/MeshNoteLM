using MeshNoteLM.Helpers;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class FileTypeDetectorTests
{
    [Theory]
    [InlineData(".doc", true)]
    [InlineData(".docx", true)]
    [InlineData(".xls", true)]
    [InlineData(".xlsx", true)]
    [InlineData(".ppt", true)]
    [InlineData(".pptx", true)]
    public void CanViewFile_ShouldReturnTrue_ForMSOfficeFiles(string extension, bool expected)
    {
        // Act
        var result = FileTypeDetector.CanViewFile($"document{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".odt", true)]
    [InlineData(".ods", true)]
    [InlineData(".odp", true)]
    public void CanViewFile_ShouldReturnTrue_ForOpenOfficeFiles(string extension, bool expected)
    {
        // Act
        var result = FileTypeDetector.CanViewFile($"document{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".pdf", true)]
    [InlineData(".md", true)]
    [InlineData(".markdown", true)]
    public void CanViewFile_ShouldReturnTrue_ForPdfAndMarkdown(string extension, bool expected)
    {
        // Act
        var result = FileTypeDetector.CanViewFile($"document{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".txt", true)]
    [InlineData(".json", true)]
    [InlineData(".xml", true)]
    [InlineData(".cs", true)]
    [InlineData(".xaml", true)]
    [InlineData(".html", true)]
    [InlineData(".css", true)]
    [InlineData(".js", true)]
    public void CanViewFile_ShouldReturnTrue_ForTextFiles(string extension, bool expected)
    {
        // Act
        var result = FileTypeDetector.CanViewFile($"file{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(".exe", false)]
    [InlineData(".dll", false)]
    [InlineData(".zip", false)]
    [InlineData(".png", false)]
    [InlineData(".jpg", false)]
    [InlineData(".mp3", false)]
    [InlineData(".mp4", false)]
    [InlineData("", false)]
    public void CanViewFile_ShouldReturnFalse_ForUnsupportedFiles(string extension, bool expected)
    {
        // Act
        var result = FileTypeDetector.CanViewFile($"file{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CanViewFile_ShouldBeCaseInsensitive()
    {
        // Act
        var lowercase = FileTypeDetector.CanViewFile("document.pdf");
        var uppercase = FileTypeDetector.CanViewFile("document.PDF");
        var mixedcase = FileTypeDetector.CanViewFile("document.Pdf");

        // Assert
        lowercase.Should().BeTrue();
        uppercase.Should().BeTrue();
        mixedcase.Should().BeTrue();
    }

    [Theory]
    [InlineData(".docx", FileTypeDetector.ViewerType.MSOffice)]
    [InlineData(".xlsx", FileTypeDetector.ViewerType.MSOffice)]
    [InlineData(".pptx", FileTypeDetector.ViewerType.MSOffice)]
    [InlineData(".odt", FileTypeDetector.ViewerType.OpenOffice)]
    [InlineData(".pdf", FileTypeDetector.ViewerType.Pdf)]
    [InlineData(".md", FileTypeDetector.ViewerType.Markdown)]
    [InlineData(".txt", FileTypeDetector.ViewerType.Text)]
    [InlineData(".exe", FileTypeDetector.ViewerType.None)]
    public void GetViewerType_ShouldReturnCorrectType(string extension, FileTypeDetector.ViewerType expected)
    {
        // Act
        var result = FileTypeDetector.GetViewerType($"file{extension}");

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://docs.google.com/document/d/123", true)]
    [InlineData("https://docs.google.com/spreadsheets/d/456", true)]
    [InlineData("https://docs.google.com/presentation/d/789", true)]
    [InlineData("https://docs.google.com/forms/d/abc", true)]
    [InlineData("https://docs.google.com/drawings/d/def", true)]
    public void IsGoogleDocsUrl_ShouldReturnTrue_ForGoogleDocsUrls(string url, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsGoogleDocsUrl(url);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("https://example.com/document", false)]
    [InlineData("https://drive.google.com/file/d/123", false)]
    [InlineData("/local/path/document.docx", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsGoogleDocsUrl_ShouldReturnFalse_ForNonGoogleDocsUrls(string? path, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsGoogleDocsUrl(path!);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.docx", true)]
    [InlineData("spreadsheet.xlsx", true)]
    [InlineData("presentation.pptx", true)]
    [InlineData("document.pdf", false)]
    [InlineData("readme.md", false)]
    public void IsMSOfficeFile_ShouldDetectMSOfficeFiles(string fileName, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsMSOfficeFile(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.odt", true)]
    [InlineData("spreadsheet.ods", true)]
    [InlineData("presentation.odp", true)]
    [InlineData("document.docx", false)]
    public void IsOpenOfficeFile_ShouldDetectOpenOfficeFiles(string fileName, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsOpenOfficeFile(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.pdf", true)]
    [InlineData("document.PDF", true)]
    [InlineData("document.docx", false)]
    public void IsPdfFile_ShouldDetectPdfFiles(string fileName, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsPdfFile(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("readme.md", true)]
    [InlineData("README.markdown", true)]
    [InlineData("document.txt", false)]
    public void IsMarkdownFile_ShouldDetectMarkdownFiles(string fileName, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsMarkdownFile(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("file.txt", true)]
    [InlineData("config.json", true)]
    [InlineData("data.xml", true)]
    [InlineData("Program.cs", true)]
    [InlineData("document.pdf", false)]
    public void IsTextFile_ShouldDetectTextFiles(string fileName, bool expected)
    {
        // Act
        var result = FileTypeDetector.IsTextFile(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.docx", "Microsoft Office Document")]
    [InlineData("document.odt", "OpenOffice Document")]
    [InlineData("document.pdf", "PDF Document")]
    [InlineData("readme.md", "Markdown Document")]
    [InlineData("file.txt", "Text File")]
    [InlineData("file.exe", "Unknown File Type")]
    public void GetFileTypeDescription_ShouldReturnCorrectDescription(string fileName, string expected)
    {
        // Act
        var result = FileTypeDetector.GetFileTypeDescription(fileName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CanViewFile_ShouldHandleFilenameWithoutExtension()
    {
        // Act
        var result = FileTypeDetector.CanViewFile("file_without_extension");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CanViewFile_ShouldHandleMultipleDots()
    {
        // Act
        var result = FileTypeDetector.CanViewFile("my.document.name.pdf");

        // Assert
        result.Should().BeTrue("should use last extension");
    }

    [Fact]
    public void CanViewFile_ShouldHandlePathSeparators()
    {
        // Act
        var result1 = FileTypeDetector.CanViewFile("/path/to/document.pdf");
        var result2 = FileTypeDetector.CanViewFile("C:\\path\\to\\document.pdf");

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void GetViewerType_ShouldHandleNullOrEmpty()
    {
        // Act
        var result1 = FileTypeDetector.GetViewerType("");
        var result2 = FileTypeDetector.GetViewerType("file");

        // Assert
        result1.Should().Be(FileTypeDetector.ViewerType.None);
        result2.Should().Be(FileTypeDetector.ViewerType.None);
    }

    [Fact]
    public void GetViewerType_ShouldBeCaseInsensitive()
    {
        // Act
        var lower = FileTypeDetector.GetViewerType("file.pdf");
        var upper = FileTypeDetector.GetViewerType("file.PDF");
        var mixed = FileTypeDetector.GetViewerType("file.Pdf");

        // Assert
        lower.Should().Be(FileTypeDetector.ViewerType.Pdf);
        upper.Should().Be(FileTypeDetector.ViewerType.Pdf);
        mixed.Should().Be(FileTypeDetector.ViewerType.Pdf);
    }
}
