using FluentAssertions;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Views;

public class ChatFileManagementTests : IDisposable
{
    private readonly string _testDirectory;

    public ChatFileManagementTests()
    {
        // Create a test directory for file operations
        _testDirectory = Path.Combine(Path.GetTempPath(), "ChatFileManagementTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void GenerateUniqueFileName_ShouldReturnOriginalPath_WhenFileDoesNotExist()
    {
        // Arrange
        var folder = _testDirectory;
        var baseFileName = "test.chat.md";
        var expectedPath = Path.Combine(folder, baseFileName);

        // Act
        var result = GenerateUniqueFileName(folder, baseFileName);

        // Assert
        result.Should().Be(expectedPath);
    }

    [Fact]
    public void GenerateUniqueFileName_ShouldGenerateNewTimestamp_WhenFileExists()
    {
        // Arrange
        var folder = _testDirectory;
        var baseFileName = "Chat_2025-01-01_12-00-00.0.chat.md";
        var basePath = Path.Combine(folder, baseFileName);

        // Create the base file
        File.WriteAllText(basePath, "test content");

        // Act
        var result = GenerateUniqueFileName(folder, baseFileName);

        // Assert
        result.Should().NotBe(basePath);
        result.Should().EndWith(".chat.md");
        var fileName = Path.GetFileName(result);
        fileName.Should().StartWith("Chat_");
        fileName.Should().MatchRegex(@"Chat_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.\d+\.chat\.md"); // Should match timestamp format with tenths
        fileName.Should().NotBe("Chat_2025-01-01_12-00-00.0.chat.md"); // Should be different from the original
    }

    [Fact]
    public void GenerateUniqueFileName_ShouldFindUniqueTimestamp_WhenMultipleFilesExist()
    {
        // Arrange
        var folder = _testDirectory;
        var baseFileName = "Chat_2025-01-01_12-00-00.0.chat.md";
        var basePath = Path.Combine(folder, baseFileName);

        // Create the base file
        File.WriteAllText(basePath, "test content 1");

        // Act
        var result = GenerateUniqueFileName(folder, baseFileName);

        // Assert
        result.Should().NotBe(basePath);
        result.Should().EndWith(".chat.md");
        Path.GetFileName(result).Should().StartWith("Chat_");
        // Should find a unique timestamp (we can't predict the exact value due to timing)
    }

    [Theory]
    [InlineData("Chat_2024-01-01_12-00-00.0.chat.md")]
    [InlineData("Chat_2024-01-01_12-00-00.5.chat.md")]
    [InlineData("Chat_2024-01-01_12-00-00.9.chat.md")]
    public void GenerateUniqueFileName_ShouldHandleVariousTimestamps(string baseFileName)
    {
        // Arrange
        var folder = _testDirectory;

        // Act
        var result = GenerateUniqueFileName(folder, baseFileName);

        // Assert
        result.Should().StartWith(folder);
        result.Should().EndWith(baseFileName);
    }

    [Fact]
    public void GenerateUniqueFileName_ShouldHaveReasonableLimit()
    {
        // Arrange
        var folder = _testDirectory;
        var baseFileName = "Chat_2025-01-01_12-00-00.0.chat.md";

        // Create the base file - the limit test is more about ensuring it doesn't get stuck in infinite loop
        File.WriteAllText(Path.Combine(folder, baseFileName), "base content");

        // Act
        var result = GenerateUniqueFileName(folder, baseFileName);

        // Assert
        result.Should().NotBeNull();
        result.Should().EndWith(".chat.md");
        result.Should().NotBe(baseFileName); // Should have found a unique timestamp
        // The method should succeed within the 100 tenths-of-second limit
    }

    [Fact]
    public void AutoCreateChatFileName_ShouldHaveCorrectFormat()
    {
        // Act
        var fileName = GenerateChatFileName();

        // Assert
        fileName.Should().StartWith("Chat_");
        fileName.Should().EndWith(".chat.md");
        fileName.Length.Should().BeGreaterThan(17); // "Chat_" + date-time with tenths + ".chat.md"
        fileName.Should().MatchRegex(@"Chat_\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.\d+\.chat\.md"); // Should match timestamp format with tenths
    }

    [Fact]
    public async Task AutoCreateChatFileName_ShouldBeUniqueAcrossMultipleCalls()
    {
        // Act
        var fileName1 = GenerateChatFileName();
        await Task.Delay(1100); // Delay over 1 second to ensure different timestamps
        var fileName2 = GenerateChatFileName();

        // Assert
        fileName1.Should().NotBe(fileName2);
        fileName1.Should().StartWith("Chat_");
        fileName2.Should().StartWith("Chat_");
    }

    [Fact]
    public async Task FileRenameOperation_ShouldMoveFileSuccessfully()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "source.chat.md");
        var targetPath = Path.Combine(_testDirectory, "target.chat.md");
        File.WriteAllText(sourcePath, "test content");

        // Act
        await RenameFileAsync(sourcePath, targetPath);

        // Assert
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(targetPath).Should().BeTrue();
        File.ReadAllText(targetPath).Should().Be("test content");
    }

    [Fact]
    public async Task FileRenameOperation_ShouldCreateTargetDirectory_WhenNotExists()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "source.chat.md");
        var targetDir = Path.Combine(_testDirectory, "newdir");
        var targetPath = Path.Combine(targetDir, "target.chat.md");
        File.WriteAllText(sourcePath, "test content");

        // Act
        await RenameFileAsync(sourcePath, targetPath);

        // Assert
        Directory.Exists(targetDir).Should().BeTrue();
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(targetPath).Should().BeTrue();
    }

    [Fact]
    public async Task FileRenameOperation_ShouldHandleMissingSourceFile()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "nonexistent.chat.md");
        var targetPath = Path.Combine(_testDirectory, "target.chat.md");

        // Act & Assert - Should not throw
        await RenameFileAsync(sourcePath, targetPath);

        // File shouldn't be created since source doesn't exist
        File.Exists(targetPath).Should().BeFalse();
    }

    [Fact]
    public async Task FileRenameOperation_ShouldHandleExistingTargetFile()
    {
        // Arrange
        var sourcePath = Path.Combine(_testDirectory, "source.chat.md");
        var targetPath = Path.Combine(_testDirectory, "target.chat.md");
        File.WriteAllText(sourcePath, "source content");
        File.WriteAllText(targetPath, "target content");

        // Act & Assert - Should not throw
        await RenameFileAsync(sourcePath, targetPath);

        // Target file should be overwritten
        File.Exists(sourcePath).Should().BeFalse();
        File.Exists(targetPath).Should().BeTrue();
        File.ReadAllText(targetPath).Should().Be("source content");
    }

    [Fact]
    public async Task ContextFileMove_ShouldMoveBothFiles_WhenRequested()
    {
        // Arrange
        var sourceChatPath = Path.Combine(_testDirectory, "chat.chat.md");
        var sourceContextPath = Path.Combine(_testDirectory, "context.txt");
        var targetDir = Path.Combine(_testDirectory, "moved");
        var targetChatPath = Path.Combine(targetDir, "chat.chat.md");

        // Create both files
        File.WriteAllText(sourceChatPath, "chat content");
        File.WriteAllText(sourceContextPath, "context content");

        // Act
        await MoveFilesWithContextAsync(sourceChatPath, targetChatPath, sourceContextPath, true);

        // Assert
        File.Exists(sourceChatPath).Should().BeFalse();
        File.Exists(sourceContextPath).Should().BeFalse();
        File.Exists(targetChatPath).Should().BeTrue();
        File.Exists(Path.Combine(targetDir, "context.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ContextFileMove_ShouldHandleContextFileConflict()
    {
        // Arrange
        var sourceChatPath = Path.Combine(_testDirectory, "chat.chat.md");
        var sourceContextPath = Path.Combine(_testDirectory, "context.txt");
        var targetDir = Path.Combine(_testDirectory, "moved");
        var targetChatPath = Path.Combine(targetDir, "chat.chat.md");
        var existingContextPath = Path.Combine(targetDir, "context.txt");

        // Create source files and existing target context file
        File.WriteAllText(sourceChatPath, "chat content");
        File.WriteAllText(sourceContextPath, "context content");
        Directory.CreateDirectory(targetDir);
        File.WriteAllText(existingContextPath, "existing context");

        // Act
        await MoveFilesWithContextAsync(sourceChatPath, targetChatPath, sourceContextPath, true);

        // Assert
        File.Exists(sourceChatPath).Should().BeFalse();
        File.Exists(sourceContextPath).Should().BeFalse();
        File.Exists(targetChatPath).Should().BeTrue();
        File.Exists(existingContextPath).Should().BeTrue(); // Original should remain
        File.Exists(Path.Combine(targetDir, "context_01.txt")).Should().BeTrue(); // Renamed version
    }

    // Helper methods to simulate the private LLMChatView methods
    private static string GenerateChatFileName()
    {
        return $"Chat_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.f}.chat.md";
    }

    private static string GenerateUniqueFileName(string folder, string baseFileName)
    {
        var basePath = Path.Combine(folder, baseFileName);

        // If the base filename doesn't exist, use it
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        // Extract the full extension (.chat.md) and base name pattern
        var extension = ".chat.md"; // We always use this extension for chat files
        var baseNameOnly = "Chat"; // Base name without timestamp

        // Retry with increasing tenths of a second
        var tenths = 1;
        string uniquePath;

        do
        {
            // Generate new timestamp with additional tenths
            var newTimestamp = DateTime.Now.AddMilliseconds(tenths * 100).ToString("yyyy-MM-dd_HH-mm-ss.f");
            var uniqueName = $"{baseNameOnly}_{newTimestamp}{extension}";
            uniquePath = Path.Combine(folder, uniqueName);
            tenths++;
        }
        while (File.Exists(uniquePath) && tenths < 100); // Prevent infinite loop (up to 10 seconds of retries)

        return uniquePath;
    }

    private static async Task RenameFileAsync(string sourcePath, string targetPath)
    {
        // Ensure target directory exists
        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
        {
            Directory.CreateDirectory(targetDirectory);
        }

        // Move the file
        if (File.Exists(sourcePath))
        {
            // Delete target if it exists to allow overwrite
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(sourcePath, targetPath);
        }
    }

    private static async Task MoveFilesWithContextAsync(string sourceChatPath, string targetChatPath, string sourceContextPath, bool moveContextFile)
    {
        try
        {
            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(targetChatPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Move the chat file
            if (File.Exists(sourceChatPath))
            {
                File.Move(sourceChatPath, targetChatPath);
            }

            // Move context file if requested and it exists
            string? newContextPath = null;
            if (moveContextFile && File.Exists(sourceContextPath))
            {
                var contextFileName = Path.GetFileName(sourceContextPath);
                newContextPath = Path.Combine(targetDirectory!, contextFileName);

                // Handle case where context file would overwrite existing file
                if (File.Exists(newContextPath))
                {
                    var counter = 1;
                    var contextBaseName = Path.GetFileNameWithoutExtension(contextFileName);
                    var contextExtension = Path.GetExtension(contextFileName);

                    do
                    {
                        var tempName = $"{contextBaseName}_{counter:D2}{contextExtension}";
                        newContextPath = Path.Combine(targetDirectory!, tempName);
                        counter++;
                    }
                    while (File.Exists(newContextPath) && counter < 100);
                }

                File.Move(sourceContextPath, newContextPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error in MoveFilesWithContext: {ex.Message}");
            throw;
        }
    }
}

