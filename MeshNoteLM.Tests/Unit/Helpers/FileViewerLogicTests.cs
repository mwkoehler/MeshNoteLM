using System;
using System.IO;
using MeshNoteLM.Helpers;
using MeshNoteLM.Plugins;
using FluentAssertions;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class FileViewerLogicTests
{
    [Theory]
    [InlineData("document.docx", FileTypeDetector.ViewerType.MSOffice, true, "Convert Microsoft Office Document to PDF for viewing")]
    [InlineData("spreadsheet.xlsx", FileTypeDetector.ViewerType.MSOffice, true, "Convert Microsoft Office Document to PDF for viewing")]
    [InlineData("presentation.pptx", FileTypeDetector.ViewerType.MSOffice, true, "Convert Microsoft Office Document to PDF for viewing")]
    [InlineData("document.odt", FileTypeDetector.ViewerType.OpenOffice, false, "View OpenOffice Document directly")]
    [InlineData("spreadsheet.ods", FileTypeDetector.ViewerType.OpenOffice, false, "View OpenOffice Document directly")]
    [InlineData("document.pdf", FileTypeDetector.ViewerType.Pdf, false, "View PDF document")]
    [InlineData("readme.md", FileTypeDetector.ViewerType.Markdown, false, "Render Markdown as HTML")]
    [InlineData("notes.txt", FileTypeDetector.ViewerType.Text, false, "Open in text editor")]
    [InlineData("data.json", FileTypeDetector.ViewerType.Text, false, "Open in text editor")]
    [InlineData("script.cs", FileTypeDetector.ViewerType.Text, false, "Open in text editor")]
    [InlineData("archive.zip", FileTypeDetector.ViewerType.None, false, "Unsupported file type")]
    [InlineData("image.jpg", FileTypeDetector.ViewerType.None, false, "Unsupported file type")]
    public void GetViewerDecision_ShouldReturnCorrectDecision(string fileName, FileTypeDetector.ViewerType expectedType, bool requiresConversion, string expectedDescription)
    {
        // Act
        var decision = FileViewerLogic.GetViewerDecision(fileName);

        // Assert
        decision.ViewerType.Should().Be(expectedType);
        decision.IsSupported.Should().Be(expectedType != FileTypeDetector.ViewerType.None);
        decision.RequiresConversion.Should().Be(requiresConversion);
        decision.ActionDescription.Should().Be(expectedDescription);

        if (expectedType == FileTypeDetector.ViewerType.None)
        {
            decision.ErrorMessage.Should().Contain("No viewer available for");
            decision.ErrorMessage.Should().Contain(Path.GetExtension(fileName));
        }
        else
        {
            decision.ErrorMessage.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetViewerDecision_ShouldReturnError_ForEmptyFileName(string fileName)
    {
        // Act
        var decision = FileViewerLogic.GetViewerDecision(fileName);

        // Assert
        decision.IsSupported.Should().BeFalse();
        decision.ViewerType.Should().Be(FileTypeDetector.ViewerType.None);
        decision.ErrorMessage.Should().Be("File name cannot be empty");
        decision.ActionDescription.Should().Be("Invalid file");
    }

    [Fact]
    public void GetViewerDecision_ShouldReturnError_ForNullFileName()
    {
        // Act
        var decision = FileViewerLogic.GetViewerDecision(null!);

        // Assert
        decision.IsSupported.Should().BeFalse();
        decision.ViewerType.Should().Be(FileTypeDetector.ViewerType.None);
        decision.ErrorMessage.Should().Be("File name cannot be empty");
        decision.ActionDescription.Should().Be("Invalid file");
    }

    [Theory]
    [InlineData("https://docs.google.com/document/d/123", null, true)]
    [InlineData("https://docs.google.com/spreadsheets/d/456", null, true)]
    [InlineData("https://docs.google.com/presentation/d/789", null, true)]
    [InlineData("https://example.com/document", null, false)]
    [InlineData("/local/path/file.txt", null, false)]
    public void RequiresGoogleWorkspaceHandling_ShouldDetectGoogleDocsUrls(string path, FileViewerLogic.IGoogleWorkspaceDetector? detector, bool expected)
    {
        // Act
        var result = FileViewerLogic.RequiresGoogleWorkspaceHandling(path, detector);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 bytes")]
    [InlineData(512, "512 bytes")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1048576, "1 MB")]
    [InlineData(1572864, "1.5 MB")]
    [InlineData(1073741824, "1 GB")]
    [InlineData(1610612736, "1.5 GB")]
    [InlineData(1099511627776, "1 TB")]
    public void FormatFileSize_ShouldReturnCorrectFormat(long bytes, string expected)
    {
        // Act
        var result = FileViewerLogic.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-100, "0 bytes")]
    public void FormatFileSize_ShouldHandleNegativeBytes(long bytes, string expected)
    {
        // Act
        var result = FileViewerLogic.FormatFileSize(bytes);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("document.docx", "Conversion failed", FileTypeDetector.ViewerType.MSOffice)]
    [InlineData("spreadsheet.xlsx", "Error during conversion", FileTypeDetector.ViewerType.MSOffice)]
    [InlineData("document.pdf", "File not found", FileTypeDetector.ViewerType.Pdf)]
    [InlineData("readme.md", "Rendering error", FileTypeDetector.ViewerType.Markdown)]
    public void GenerateErrorMessage_ShouldIncludeConversionContext_WhenConversionException(string fileName, string exceptionMessage, FileTypeDetector.ViewerType viewerType)
    {
        // Arrange
        var exception = new Exception(exceptionMessage);

        // Act
        var result = FileViewerLogic.GenerateErrorMessage(fileName, exception, viewerType);

        // Assert
        if (exceptionMessage.Contains("conversion", StringComparison.OrdinalIgnoreCase))
        {
            result.Should().Contain("Failed to convert");
            result.Should().Contain(Path.GetExtension(fileName));
            result.Should().Contain(exceptionMessage);
        }
        else
        {
            result.Should().Contain("Error loading");
            result.Should().Contain(viewerType.ToString());
            result.Should().Contain(fileName);
            result.Should().Contain(exceptionMessage);
        }
    }

    [Fact]
    public void ValidateFileData_ShouldReturnError_ForNullData()
    {
        // Act
        var result = FileViewerLogic.ValidateFileData(null!, "test.txt");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("File data is null");
        result.Severity.Should().Be(FileViewerLogic.ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateFileData_ShouldReturnError_ForEmptyData()
    {
        // Act
        var result = FileViewerLogic.ValidateFileData(Array.Empty<byte>(), "test.txt");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Be("File is empty");
        result.Severity.Should().Be(FileViewerLogic.ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateFileData_ShouldReturnWarning_ForLargeFile()
    {
        // Arrange - Create a 101MB file
        var largeFile = new byte[101 * 1024 * 1024];

        // Act
        var result = FileViewerLogic.ValidateFileData(largeFile, "large.txt");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("too large");
        result.ErrorMessage.Should().Contain("100 MB");
        result.Severity.Should().Be(FileViewerLogic.ValidationSeverity.Warning);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3, 4 }, "document.pdf", FileTypeDetector.ViewerType.Pdf, 4)]
    [InlineData(new byte[] { 65, 66, 67 }, "notes.txt", FileTypeDetector.ViewerType.Text, 3)]
    [InlineData(new byte[] { 35, 36, 37 }, "data.json", FileTypeDetector.ViewerType.Text, 3)]
    public void ValidateFileData_ShouldReturnSuccess_ForValidData(byte[] fileData, string fileName, FileTypeDetector.ViewerType expectedType, long expectedSize)
    {
        // Act
        var result = FileViewerLogic.ValidateFileData(fileData, fileName);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.FileSize.Should().Be(expectedSize);
        result.ViewerType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData(new byte[] { 1, 2, 3 }, "archive.zip")]
    [InlineData(new byte[] { 4, 5, 6 }, "image.jpg")]
    [InlineData(new byte[] { 7, 8, 9 }, "music.mp3")]
    public void ValidateFileData_ShouldReturnError_ForUnsupportedFileTypes(byte[] fileData, string fileName)
    {
        // Act
        var result = FileViewerLogic.ValidateFileData(fileData, fileName);

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unsupported file type");
        result.ErrorMessage.Should().Contain(Path.GetExtension(fileName));
        result.Severity.Should().Be(FileViewerLogic.ValidationSeverity.Error);
    }

    [Fact]
    public void GetViewerDecision_ShouldHandleAllFileTypes()
    {
        // Test all supported file types to ensure no unexpected failures
        var testFiles = new[]
        {
            "document.doc", "document.docx", "spreadsheet.xls", "spreadsheet.xlsx",
            "presentation.ppt", "presentation.pptx", "document.odt", "spreadsheet.ods",
            "presentation.odp", "document.pdf", "readme.md", "notes.markdown",
            "file.txt", "data.json", "config.xml", "script.cs", "style.css",
            "script.js", "page.html"
        };

        foreach (var fileName in testFiles)
        {
            // Act
            var decision = FileViewerLogic.GetViewerDecision(fileName);

            // Assert
            decision.Should().NotBeNull();
            decision.ActionDescription.Should().NotBeNullOrEmpty();
        }
    }
}
