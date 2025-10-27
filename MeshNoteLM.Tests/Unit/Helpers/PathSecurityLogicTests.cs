using MeshNoteLM.Helpers;
using FluentAssertions;
using Xunit;
using System.IO;
using System.Runtime.InteropServices;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class PathSecurityLogicTests
{
    private readonly string _testRoot;

    public PathSecurityLogicTests()
    {
        // Use platform-specific test root
        _testRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\TestRoot"
            : "/TestRoot";
    }

    #region IsRootPath Tests

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("/", true)]
    [InlineData("\\", true)]
    public void IsRootPath_ShouldReturnTrue_ForRootPaths(string? path, bool expected)
    {
        // Act
        var result = PathSecurityLogic.IsRootPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("folder", false)]
    [InlineData("/folder", false)]
    [InlineData("folder/file.txt", false)]
    public void IsRootPath_ShouldReturnFalse_ForNonRootPaths(string path, bool expected)
    {
        // Act
        var result = PathSecurityLogic.IsRootPath(path);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region NormalizePathSeparators Tests

    [Theory]
    [InlineData(@"folder\file.txt", "folder/file.txt")]
    [InlineData(@"folder\subfolder\file.txt", "folder/subfolder/file.txt")]
    [InlineData("folder/file.txt", "folder/file.txt")]
    [InlineData(@"C:\folder\file.txt", "C:/folder/file.txt")]
    public void NormalizePathSeparators_ShouldConvertBackslashesToForwardSlashes(string input, string expected)
    {
        // Act
        var result = PathSecurityLogic.NormalizePathSeparators(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void NormalizePathSeparators_ShouldHandleNullOrEmpty(string? input, string expected)
    {
        // Act
        var result = PathSecurityLogic.NormalizePathSeparators(input!);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region TrimLeadingSlashes Tests

    [Theory]
    [InlineData("/folder/file.txt", "folder/file.txt")]
    [InlineData("///folder/file.txt", "folder/file.txt")]
    [InlineData(@"\folder\file.txt", "folder\\file.txt")]
    [InlineData("folder/file.txt", "folder/file.txt")]
    public void TrimLeadingSlashes_ShouldRemoveLeadingSlashes(string input, string expected)
    {
        // Act
        var result = PathSecurityLogic.TrimLeadingSlashes(input);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void TrimLeadingSlashes_ShouldHandleNullOrEmpty(string? input, string expected)
    {
        // Act
        var result = PathSecurityLogic.TrimLeadingSlashes(input!);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region IsRelativePath Tests

    [Theory]
    [InlineData("folder/file.txt", true)]
    [InlineData("subfolder/file.txt", true)]
    [InlineData("file.txt", true)]
    [InlineData("", true)]
    public void IsRelativePath_ShouldReturnTrue_ForRelativePaths(string path, bool expected)
    {
        // Act
        var result = PathSecurityLogic.IsRelativePath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(@"C:\folder\file.txt")]
    [InlineData("/absolute/path")]
    public void IsRelativePath_ShouldReturnFalse_ForAbsolutePaths(string path)
    {
        // Only test if this is an absolute path on the current platform
        if (Path.IsPathRooted(path))
        {
            // Act
            var result = PathSecurityLogic.IsRelativePath(path);

            // Assert
            result.Should().BeFalse();
        }
    }

    #endregion

    #region IsWithinRoot Tests

    [Fact]
    public void IsWithinRoot_ShouldReturnTrue_ForPathEqualToRoot()
    {
        // Act
        var result = PathSecurityLogic.IsWithinRoot(_testRoot, _testRoot);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinRoot_ShouldReturnTrue_ForPathWithinRoot()
    {
        // Arrange
        var childPath = Path.Combine(_testRoot, "subfolder", "file.txt");

        // Act
        var result = PathSecurityLogic.IsWithinRoot(_testRoot, childPath);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsWithinRoot_ShouldReturnFalse_ForPathOutsideRoot()
    {
        // Arrange
        var outsidePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\OtherFolder\file.txt"
            : "/OtherFolder/file.txt";

        // Act
        var result = PathSecurityLogic.IsWithinRoot(_testRoot, outsidePath);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "somepath")]
    [InlineData("somepath", null)]
    [InlineData("", "somepath")]
    [InlineData("somepath", "")]
    public void IsWithinRoot_ShouldReturnFalse_ForNullOrEmpty(string? root, string? combined)
    {
        // Act
        var result = PathSecurityLogic.IsWithinRoot(root!, combined!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region SecureCombine Tests

    [Fact]
    public void SecureCombine_ShouldReturnRoot_ForRootPath()
    {
        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "/");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(_testRoot);
        result.ViolationType.Should().Be(PathSecurityLogic.SecurityViolationType.None);
    }

    [Fact]
    public void SecureCombine_ShouldReturnRoot_ForEmptyPath()
    {
        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(_testRoot);
    }

    [Fact]
    public void SecureCombine_ShouldCombinePaths_ForValidRelativePath()
    {
        // Arrange
        var expectedPath = Path.Combine(_testRoot, "folder", "file.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "folder/file.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
        result.ViolationType.Should().Be(PathSecurityLogic.SecurityViolationType.None);
    }

    [Fact]
    public void SecureCombine_ShouldHandleBackslashes()
    {
        // Arrange
        var expectedPath = Path.Combine(_testRoot, "folder", "file.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, @"folder\file.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    [Fact]
    public void SecureCombine_ShouldHandleLeadingSlash()
    {
        // Arrange
        var expectedPath = Path.Combine(_testRoot, "folder", "file.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "/folder/file.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    [Fact]
    public void SecureCombine_ShouldRejectAbsolutePath_Windows()
    {
        // Only run on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, @"C:\absolute\path");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Absolute paths are not allowed");
        result.ViolationType.Should().Be(PathSecurityLogic.SecurityViolationType.AbsolutePathNotAllowed);
    }

    [Fact]
    public void SecureCombine_ShouldRejectPathTraversal()
    {
        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "../outside");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ErrorMessage.Should().Contain("escapes the allowed root");
        result.ViolationType.Should().Be(PathSecurityLogic.SecurityViolationType.PathEscapesRoot);
    }

    [Fact]
    public void SecureCombine_ShouldRejectMultiplePathTraversal()
    {
        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "folder/../../outside");

        // Assert
        result.IsValid.Should().BeFalse();
        result.ViolationType.Should().Be(PathSecurityLogic.SecurityViolationType.PathEscapesRoot);
    }

    [Fact]
    public void SecureCombine_ShouldAllowDotDotWithinRoot()
    {
        // Arrange - path that uses .. but stays within root
        var expectedPath = Path.Combine(_testRoot, "file.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "folder/../file.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    #endregion

    #region ToRelativePath Tests

    [Fact]
    public void ToRelativePath_ShouldConvertToRelative()
    {
        // Arrange
        var fullPath = Path.Combine(_testRoot, "folder", "file.txt");

        // Act
        var result = PathSecurityLogic.ToRelativePath(_testRoot, fullPath);

        // Assert
        result.Should().Be("folder/file.txt");
    }

    [Fact]
    public void ToRelativePath_ShouldHandleRootPath()
    {
        // Act
        var result = PathSecurityLogic.ToRelativePath(_testRoot, _testRoot);

        // Assert
        result.Should().Be(".");
    }

    [Fact]
    public void ToRelativePath_ShouldNormalizeBackslashes()
    {
        // Arrange
        var fullPath = Path.Combine(_testRoot, "folder", "subfolder", "file.txt");

        // Act
        var result = PathSecurityLogic.ToRelativePath(_testRoot, fullPath);

        // Assert
        result.Should().Contain("/");
        result.Should().NotContain("\\");
    }

    [Theory]
    [InlineData(null, "somepath")]
    [InlineData("somepath", null)]
    [InlineData("", "somepath")]
    [InlineData("somepath", "")]
    public void ToRelativePath_ShouldReturnEmpty_ForNullOrEmpty(string? root, string? full)
    {
        // Act
        var result = PathSecurityLogic.ToRelativePath(root!, full!);

        // Assert
        result.Should().Be("");
    }

    #endregion

    #region IsSecurePath Tests

    [Theory]
    [InlineData("folder/file.txt", true)]
    [InlineData("subfolder/file.txt", true)]
    [InlineData("file.txt", true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    public void IsSecurePath_ShouldReturnTrue_ForSecurePaths(string path, bool expected)
    {
        // Act
        var result = PathSecurityLogic.IsSecurePath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("../outside", false)]
    [InlineData("folder/../outside", false)]
    [InlineData("folder/../../outside", false)]
    public void IsSecurePath_ShouldReturnFalse_ForPathTraversal(string path, bool expected)
    {
        // Act
        var result = PathSecurityLogic.IsSecurePath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsSecurePath_ShouldReturnFalse_ForNullBytes()
    {
        // Arrange
        var pathWithNull = "folder\0file.txt";

        // Act
        var result = PathSecurityLogic.IsSecurePath(pathWithNull);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetParentRelativePath Tests

    [Theory]
    [InlineData("folder/subfolder/file.txt", "folder/subfolder")]
    [InlineData("folder/file.txt", "folder")]
    [InlineData("folder/subfolder", "folder")]
    public void GetParentRelativePath_ShouldReturnParent(string path, string expected)
    {
        // Act
        var result = PathSecurityLogic.GetParentRelativePath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("file.txt", "")]
    [InlineData("folder", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void GetParentRelativePath_ShouldReturnEmpty_ForTopLevelPaths(string? path, string expected)
    {
        // Act
        var result = PathSecurityLogic.GetParentRelativePath(path!);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetParentRelativePath_ShouldHandleBackslashes()
    {
        // Act
        var result = PathSecurityLogic.GetParentRelativePath(@"folder\subfolder\file.txt");

        // Assert
        result.Should().Be("folder/subfolder");
    }

    [Fact]
    public void GetParentRelativePath_ShouldHandleTrailingSlash()
    {
        // Act
        var result = PathSecurityLogic.GetParentRelativePath("folder/subfolder/");

        // Assert
        result.Should().Be("folder");
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void SecureCombine_ShouldHandleComplexPath()
    {
        // Arrange - complex but valid path
        var expectedPath = Path.Combine(_testRoot, "folder", "subfolder", "file.with.dots.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "folder/subfolder/file.with.dots.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    [Fact]
    public void SecureCombine_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var unicodePath = "folder/文件/файл.txt";
        var expectedPath = Path.Combine(_testRoot, "folder", "文件", "файл.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, unicodePath);

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    [Fact]
    public void SecureCombine_ShouldHandleSpacesInPath()
    {
        // Arrange
        var expectedPath = Path.Combine(_testRoot, "My Folder", "My File.txt");

        // Act
        var result = PathSecurityLogic.SecureCombine(_testRoot, "My Folder/My File.txt");

        // Assert
        result.IsValid.Should().BeTrue();
        result.FullPath.Should().Be(expectedPath);
    }

    #endregion
}
