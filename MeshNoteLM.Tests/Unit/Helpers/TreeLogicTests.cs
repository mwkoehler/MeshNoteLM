using MeshNoteLM.Helpers;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;
using System.Linq;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class TreeLogicTests
{
    // Test implementation of ITreeNode for sorting tests
    private class TestTreeNode : TreeLogic.ITreeNode
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }

    #region GetLeafName Tests

    [Theory]
    [InlineData("/folder/file.txt", "file.txt")]
    [InlineData("/folder/subfolder/document.pdf", "document.pdf")]
    [InlineData("/root/data.json", "data.json")]
    [InlineData("file.txt", "file.txt")]
    public void GetLeafName_ShouldReturnFileName_ForFilePaths(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetLeafName(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder/subfolder", "subfolder")]
    [InlineData("/folder/subfolder/", "subfolder")]
    [InlineData("/root", "root")]
    [InlineData("folder", "folder")]
    public void GetLeafName_ShouldReturnDirectoryName_ForDirectoryPaths(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetLeafName(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("   ", "   ")]
    public void GetLeafName_ShouldHandleEdgeCases(string? path, string expected)
    {
        // Act
        var result = TreeLogic.GetLeafName(path!);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder/file.with.dots.txt", "file.with.dots.txt")]
    [InlineData("/my.folder/file.txt", "file.txt")]
    [InlineData("file.with.many.dots.json", "file.with.many.dots.json")]
    public void GetLeafName_ShouldHandleMultipleDots(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetLeafName(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder/file.txt", "file.txt")]
    [InlineData("/folder/file.txt/", "file.txt")]
    [InlineData("/folder/file.txt///", "file.txt")]
    public void GetLeafName_ShouldHandleTrailingSlashes(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetLeafName(path);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region SafeString Tests

    [Theory]
    [InlineData("valid string", "valid string")]
    [InlineData("Plugin Name", "Plugin Name")]
    [InlineData("123", "123")]
    public void SafeString_ShouldReturnOriginal_ForValidStrings(string input, string expected)
    {
        // Act
        var result = TreeLogic.SafeString(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("\t\n", "")]
    public void SafeString_ShouldReturnEmpty_ForInvalidStrings(string? input, string expected)
    {
        // Act
        var result = TreeLogic.SafeString(input);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region SortTreeNodes Tests

    [Fact]
    public void SortTreeNodes_ShouldPlaceDirectoriesFirst()
    {
        // Arrange
        var nodes = new List<TestTreeNode>
        {
            new() { Name = "file1.txt", IsDirectory = false },
            new() { Name = "directory1", IsDirectory = true },
            new() { Name = "file2.txt", IsDirectory = false },
            new() { Name = "directory2", IsDirectory = true }
        };

        // Act
        var sorted = TreeLogic.SortTreeNodes(nodes).ToList();

        // Assert
        sorted[0].IsDirectory.Should().BeTrue();
        sorted[1].IsDirectory.Should().BeTrue();
        sorted[2].IsDirectory.Should().BeFalse();
        sorted[3].IsDirectory.Should().BeFalse();
    }

    [Fact]
    public void SortTreeNodes_ShouldSortAlphabetically_WithinGroups()
    {
        // Arrange
        var nodes = new List<TestTreeNode>
        {
            new() { Name = "zebra", IsDirectory = true },
            new() { Name = "apple", IsDirectory = true },
            new() { Name = "zoo.txt", IsDirectory = false },
            new() { Name = "aardvark.txt", IsDirectory = false }
        };

        // Act
        var sorted = TreeLogic.SortTreeNodes(nodes).ToList();

        // Assert
        sorted[0].Name.Should().Be("apple");      // Directory
        sorted[1].Name.Should().Be("zebra");      // Directory
        sorted[2].Name.Should().Be("aardvark.txt"); // File
        sorted[3].Name.Should().Be("zoo.txt");    // File
    }

    [Fact]
    public void SortTreeNodes_ShouldBeCaseInsensitive()
    {
        // Arrange
        var nodes = new List<TestTreeNode>
        {
            new() { Name = "Zebra", IsDirectory = true },
            new() { Name = "apple", IsDirectory = true },
            new() { Name = "BANANA", IsDirectory = true }
        };

        // Act
        var sorted = TreeLogic.SortTreeNodes(nodes).ToList();

        // Assert
        sorted[0].Name.Should().Be("apple");
        sorted[1].Name.Should().Be("BANANA");
        sorted[2].Name.Should().Be("Zebra");
    }

    [Fact]
    public void SortTreeNodes_ShouldHandleEmptyCollection()
    {
        // Arrange
        var nodes = new List<TestTreeNode>();

        // Act
        var sorted = TreeLogic.SortTreeNodes(nodes).ToList();

        // Assert
        sorted.Should().BeEmpty();
    }

    [Fact]
    public void SortTreeNodes_ShouldHandleSingleNode()
    {
        // Arrange
        var nodes = new List<TestTreeNode>
        {
            new() { Name = "single", IsDirectory = true }
        };

        // Act
        var sorted = TreeLogic.SortTreeNodes(nodes).ToList();

        // Assert
        sorted.Should().HaveCount(1);
        sorted[0].Name.Should().Be("single");
    }

    #endregion

    #region GetNodeSortPriority Tests

    [Fact]
    public void GetNodeSortPriority_ShouldReturn0_ForDirectories()
    {
        // Act
        var result = TreeLogic.GetNodeSortPriority(isDirectory: true);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void GetNodeSortPriority_ShouldReturn1_ForFiles()
    {
        // Act
        var result = TreeLogic.GetNodeSortPriority(isDirectory: false);

        // Assert
        result.Should().Be(1);
    }

    #endregion

    #region IsRootOrEmpty Tests

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("/", true)]
    [InlineData(" / ", true)]
    public void IsRootOrEmpty_ShouldReturnTrue_ForRootOrEmptyPaths(string? path, bool expected)
    {
        // Act
        var result = TreeLogic.IsRootOrEmpty(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder", false)]
    [InlineData("/folder/file.txt", false)]
    [InlineData("file.txt", false)]
    public void IsRootOrEmpty_ShouldReturnFalse_ForValidPaths(string path, bool expected)
    {
        // Act
        var result = TreeLogic.IsRootOrEmpty(path);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region NormalizePath Tests

    [Theory]
    [InlineData("/folder/file.txt", "/folder/file.txt")]
    [InlineData("/folder/file.txt/", "/folder/file.txt")]
    [InlineData("/folder/file.txt///", "/folder/file.txt")]
    [InlineData("folder", "folder")]
    public void NormalizePath_ShouldRemoveTrailingSlashes(string path, string expected)
    {
        // Act
        var result = TreeLogic.NormalizePath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizePath_ShouldReturnEmpty_ForInvalidPaths(string? path, string expected)
    {
        // Act
        var result = TreeLogic.NormalizePath(path!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizePath_ShouldHandleRootPath()
    {
        // Act
        var result = TreeLogic.NormalizePath("/");

        // Assert
        result.Should().Be("");
    }

    #endregion

    #region CombinePath Tests

    [Theory]
    [InlineData("/folder", "file.txt", "/folder/file.txt")]
    [InlineData("/folder/subfolder", "document.pdf", "/folder/subfolder/document.pdf")]
    [InlineData("/root", "data.json", "/root/data.json")]
    public void CombinePath_ShouldCombineParentAndChild(string parent, string child, string expected)
    {
        // Act
        var result = TreeLogic.CombinePath(parent, child);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/", "file.txt", "/file.txt")]
    [InlineData("", "file.txt", "/file.txt")]
    [InlineData("   ", "file.txt", "/file.txt")]
    public void CombinePath_ShouldHandleRootParent(string? parent, string child, string expected)
    {
        // Act
        var result = TreeLogic.CombinePath(parent!, child);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder/", "file.txt", "/folder/file.txt")]
    [InlineData("/folder///", "file.txt", "/folder/file.txt")]
    public void CombinePath_ShouldHandleTrailingSlashes(string parent, string child, string expected)
    {
        // Act
        var result = TreeLogic.CombinePath(parent, child);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder", null, "/folder")]
    [InlineData("/folder", "", "/folder")]
    [InlineData("/folder", "   ", "/folder")]
    public void CombinePath_ShouldHandleEmptyChild(string parent, string? child, string expected)
    {
        // Act
        var result = TreeLogic.CombinePath(parent, child!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CombinePath_ShouldHandleBothEmpty()
    {
        // Act
        var result = TreeLogic.CombinePath("", "");

        // Assert
        result.Should().Be("");
    }

    #endregion

    #region GetParentPath Tests

    [Theory]
    [InlineData("/folder/file.txt", "/folder")]
    [InlineData("/folder/subfolder/document.pdf", "/folder/subfolder")]
    [InlineData("/root/data.json", "/root")]
    public void GetParentPath_ShouldReturnParentDirectory(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetParentPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/file.txt", "/")]
    [InlineData("/folder", "/")]
    [InlineData("file.txt", "/")]
    public void GetParentPath_ShouldReturnRoot_ForTopLevelPaths(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetParentPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("   ", "/")]
    public void GetParentPath_ShouldReturnRoot_ForInvalidPaths(string? path, string expected)
    {
        // Act
        var result = TreeLogic.GetParentPath(path!);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("/folder/file.txt/", "/folder")]
    [InlineData("/folder/subfolder///", "/folder")]
    public void GetParentPath_ShouldHandleTrailingSlashes(string path, string expected)
    {
        // Act
        var result = TreeLogic.GetParentPath(path);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
