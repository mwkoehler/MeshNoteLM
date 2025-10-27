using System;
using System.Collections.Generic;
using System.Linq;
using MeshNoteLM.Helpers;
using MeshNoteLM.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace MeshNoteLM.Tests.Unit.Helpers;

public class TreeBuilderLogicTests
{
    private readonly Mock<IFileSystemPlugin> _mockPlugin = new();

    public TreeBuilderLogicTests()
    {
        _mockPlugin.Setup(p => p.Name).Returns("TestPlugin");
    }

    [Theory]
    [InlineData("/folder/file.txt", "file.txt", true)]
    [InlineData("/documents/report.pdf", "report.pdf", true)]
    [InlineData("folder/subfolder", "subfolder", true)]
    [InlineData("file.txt", "file.txt", true)]
    [InlineData("folder-with-dashes", "folder-with-dashes", true)]
    [InlineData("123 numbers", "123 numbers", true)]
    [InlineData("", "", false)]
    [InlineData("   ", "", false)]
    [InlineData("/", "/", true)] // Root path is valid - TreeLogic.GetLeafName returns "/"
    [InlineData("///", "/", true)]  /// becomes empty string but contains '/', so returns "/"
    public void ValidateAndNormalizeNode_ShouldHandleVariousPaths(string path, string expectedName, bool shouldSucceed)
    {
        // Act
        var result = TreeBuilderLogic.ValidateAndNormalizeNode(path);

        // Assert
        result.Success.Should().Be(shouldSucceed);
        result.NodeName.Should().Be(expectedName);

        if (!shouldSucceed)
        {
            result.ErrorMessage.Should().NotBeNullOrEmpty();
            result.FailureType.Should().NotBe(TreeBuilderLogic.ValidationFailureType.None);
        }
    }

    [Theory]
    [InlineData("/folder", false)]  // These are not root paths
    [InlineData("/path/to/directory", false)]  // These are not root paths
    [InlineData("relative/path", false)]  // These are not root paths
    [InlineData("file.txt", false)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("/", true)]
    [InlineData("\\", false)]  // Backslash is not considered root by TreeLogic
    public void IsRootPath_ShouldIdentifyRootPaths(string? path, bool expected)
    {
        // Act
        var result = TreeBuilderLogic.IsRootPath(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void CreateDirectoryNodeSpec_ShouldReturnError_ForNullPlugin()
    {
        // Act
        var result = TreeBuilderLogic.CreateDirectoryNodeSpec("/folder", null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Plugin cannot be null for directory nodes");
        result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.NullPlugin);
    }

    [Fact]
    public void CreateDirectoryNodeSpec_ShouldReturnSuccess_ForValidInput()
    {
        // Act
        var result = TreeBuilderLogic.CreateDirectoryNodeSpec("/valid/directory", _mockPlugin.Object);

        // Assert
        result.Success.Should().BeTrue();
        result.NodeName.Should().Be("directory");
        result.ErrorMessage.Should().BeNull();
        result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.None);
    }

    [Fact]
    public void CreateFileNodeSpec_ShouldReturnError_ForNullPlugin()
    {
        // Act
        var result = TreeBuilderLogic.CreateFileNodeSpec("/folder/file.txt", null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Plugin cannot be null for file nodes");
        result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.NullPlugin);
    }

    [Fact]
    public void CreateFileNodeSpec_ShouldReturnSuccess_ForValidInput()
    {
        // Act
        var result = TreeBuilderLogic.CreateFileNodeSpec("/valid/file.txt", _mockPlugin.Object);

        // Assert
        result.Success.Should().BeTrue();
        result.NodeName.Should().Be("file.txt");
        result.ErrorMessage.Should().BeNull();
        result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.None);
    }

    [Fact]
    public void ValidatePluginRoot_ShouldReturnError_ForNullPlugin()
    {
        // Act
        var result = TreeBuilderLogic.ValidatePluginRoot("TestPlugin", null);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Plugin instance cannot be null");
        result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.NullPlugin);
    }

    [Theory]
    [InlineData("ValidPlugin", true)]
    [InlineData("Plugin With Spaces", true)]
    [InlineData("plugin-with-dashes", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ValidatePluginRoot_ShouldHandlePluginNames(string? pluginName, bool expectedSuccess)
    {
        // Act
        var result = TreeBuilderLogic.ValidatePluginRoot(pluginName ?? "", _mockPlugin.Object);

        // Assert
        result.Success.Should().Be(expectedSuccess);

        if (expectedSuccess)
        {
            result.NodeName.Should().Be(TreeLogic.SafeString(pluginName));
        }
        else
        {
            result.FailureType.Should().Be(TreeBuilderLogic.ValidationFailureType.InvalidName);
        }
    }

    [Theory]
    [InlineData("directory", "MyFolder", "MyFolder", "directory: MyFolder (MyFolder)")]
    [InlineData("file", "document.txt", "TestPlugin", "file: document.txt (TestPlugin)")]
    [InlineData("root", "/", "CloudStorage", "root: / (CloudStorage)")]
    public void GenerateNodeDescription_ShouldCreateCorrectDescription(string nodeType, string nodeName, string? pluginName, string expected)
    {
        // Act
        var result = TreeBuilderLogic.GenerateNodeDescription(nodeType, nodeName, pluginName);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GenerateNodeDescription_ShouldHandleMissingPluginName()
    {
        // Act
        var result = TreeBuilderLogic.GenerateNodeDescription("file", "test.txt", null);

        // Assert
        result.Should().Be("file: test.txt");
    }

    private class TestNode(string name, string path, bool isDirectory, IFileSystemPlugin? plugin) : TreeBuilderLogic.IBuildableNode
    {
        public string Name { get; set; } = name;
        public string Path { get; set; } = path;
        public bool IsDirectory { get; set; } = isDirectory;
        public IFileSystemPlugin? Plugin { get; set; } = plugin;
    }

    [Fact]
    public void GroupNodesByPlugin_ShouldGroupCorrectly()
    {
        // Arrange
        var plugin1 = new Mock<IFileSystemPlugin>();
        plugin1.Setup(p => p.Name).Returns("Plugin1");

        var plugin2 = new Mock<IFileSystemPlugin>();
        plugin2.Setup(p => p.Name).Returns("Plugin2");

        var nodes = new List<TestNode>
        {
            new("file1.txt", "/file1.txt", false, plugin1.Object),
            new("dir1", "/dir1", true, plugin1.Object),
            new("file2.txt", "/file2.txt", false, plugin2.Object),
            new("file3.txt", "/file3.txt", false, plugin1.Object),
            new("dir2", "/dir2", true, plugin2.Object),
            new("null", "/null", false, null!) // Should be excluded
        };

        // Act
        var result = TreeBuilderLogic.GroupNodesByPlugin(nodes);

        // Assert
        result.Should().ContainKey("Plugin1");
        result.Should().ContainKey("Plugin2");
        result.Should().NotContainKey("null");

        result["Plugin1"].Should().HaveCount(3);
        result["Plugin2"].Should().HaveCount(2);

        result["Plugin1"].All(n => n.Plugin?.Name == "Plugin1").Should().BeTrue();
        result["Plugin2"].All(n => n.Plugin?.Name == "Plugin2").Should().BeTrue();
    }

    [Fact]
    public void FilterNodes_ShouldFilterBySearchTerm()
    {
        // Arrange
        var nodes = new List<TestNode>
        {
            new("document.txt", "/document.txt", false, _mockPlugin.Object),
            new("readme.md", "/readme.md", false, _mockPlugin.Object),
            new("data.json", "/data.json", false, _mockPlugin.Object),
            new("config.xml", "/config.xml", false, _mockPlugin.Object)
        };

        // Act
        var result = TreeBuilderLogic.FilterNodes(nodes, "doc");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("document.txt");
    }

    [Fact]
    public void FilterNodes_ShouldBeCaseInsensitive()
    {
        // Arrange
        var nodes = new List<TestNode>
        {
            new("Document.txt", "/Document.txt", false, _mockPlugin.Object),
            new("document.TXT", "/document.TXT", false, _mockPlugin.Object)
        };

        // Act
        var result = TreeBuilderLogic.FilterNodes(nodes, "DOC");

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public void FilterNodes_ShouldFilterByNodeType()
    {
        // Arrange
        var nodes = new List<TestNode>
        {
            new("file1.txt", "/file1.txt", false, _mockPlugin.Object),
            new("dir1", "/dir1", true, _mockPlugin.Object),
            new("file2.txt", "/file2.txt", false, _mockPlugin.Object),
            new("dir2", "/dir2", true, _mockPlugin.Object)
        };

        // Act
        var directories = TreeBuilderLogic.FilterNodes(nodes, null, includeDirectories: true, includeFiles: false);
        var files = TreeBuilderLogic.FilterNodes(nodes, null, includeDirectories: false, includeFiles: true);

        // Assert
        directories.Should().HaveCount(2);
        directories.All(n => n.IsDirectory).Should().BeTrue();

        files.Should().HaveCount(2);
        files.All(n => !n.IsDirectory).Should().BeTrue();
    }

    [Fact]
    public void FilterNodes_ShouldHandleNullCollection()
    {
        // Act
        var result = TreeBuilderLogic.FilterNodes<TestNode>(null!, "search");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CalculateTreeStatistics_ShouldCalculateCorrectly()
    {
        // Arrange
        var plugin1 = new Mock<IFileSystemPlugin>();
        plugin1.Setup(p => p.Name).Returns("Plugin1");

        var plugin2 = new Mock<IFileSystemPlugin>();
        plugin2.Setup(p => p.Name).Returns("Plugin2");

        var nodes = new List<TestNode>
        {
            new("file1.txt", "/file1.txt", false, plugin1.Object),
            new("dir1", "/dir1", true, plugin1.Object),
            new("file2.txt", "/file2.txt", false, plugin2.Object),
            new("dir2", "/dir2", true, plugin1.Object),
            new("file3.txt", "/file3.txt", false, plugin1.Object),
            new("null", "/null", false, null)
        };

        // Act
        var stats = TreeBuilderLogic.CalculateTreeStatistics(nodes);

        // Assert
        stats.TotalNodes.Should().Be(6);
        stats.DirectoryCount.Should().Be(2);
        stats.FileCount.Should().Be(4);
        stats.PluginCount.Should().Be(2);
        stats.PluginNames.Should().Contain("Plugin1");
        stats.PluginNames.Should().Contain("Plugin2");
    }

    [Fact]
    public void CalculateTreeStatistics_ShouldHandleNullCollection()
    {
        // Act
        var stats = TreeBuilderLogic.CalculateTreeStatistics<TestNode>(null!);

        // Assert
        stats.TotalNodes.Should().Be(0);
        stats.DirectoryCount.Should().Be(0);
        stats.FileCount.Should().Be(0);
        stats.PluginCount.Should().Be(0);
        stats.PluginNames.Should().BeEmpty();
    }

    [Fact]
    public void FilterNodes_ShouldTrimSearchTerm()
    {
        // Arrange
        var nodes = new List<TestNode>
        {
            new("document.txt", "/document.txt", false, _mockPlugin.Object),
            new("readme.md", "/readme.md", false, _mockPlugin.Object)
        };

        // Act
        var result = TreeBuilderLogic.FilterNodes(nodes, "  document  ");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("document.txt");
    }

    [Fact]
    public void GroupNodesByPlugin_ShouldHandleEmptyCollection()
    {
        // Act
        var result = TreeBuilderLogic.GroupNodesByPlugin(Enumerable.Empty<TestNode>());

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("folder/..", true)]  // These are valid strings, just not validated for traversal
    [InlineData("folder/../file.txt", true)]
    [InlineData("../folder", true)]
    [InlineData("folder/../..", true)]
    [InlineData("normal_folder", true)]
    [InlineData("folder/../normal", true)]
    public void ValidateAndNormalizeNode_ShouldHandlePathTraversal(string? path, bool expectedSuccess)
    {
        // Act
        var result = TreeBuilderLogic.ValidateAndNormalizeNode(path!);

        // Assert
        result.Success.Should().Be(expectedSuccess);
    }
}
