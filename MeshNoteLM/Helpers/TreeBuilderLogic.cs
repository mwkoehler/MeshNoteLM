using System;
using System.Collections.Generic;
using System.Linq;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Helpers
{
    /// <summary>
    /// Pure logic class for tree building operations.
    /// Contains no UI or ViewModel dependencies - fully testable.
    /// </summary>
    public static class TreeBuilderLogic
    {
        /// <summary>
        /// Represents a tree node for building operations
        /// </summary>
        public interface IBuildableNode
        {
            string Name { get; }
            string Path { get; }
            bool IsDirectory { get; }
            IFileSystemPlugin? Plugin { get; }
        }

        /// <summary>
        /// Result of tree node creation
        /// </summary>
        public class NodeCreationResult
        {
            /// <summary>
            /// Whether the creation was successful
            /// </summary>
            public bool Success { get; init; }

            /// <summary>
            /// The node name (normalized)
            /// </summary>
            public string NodeName { get; init; } = string.Empty;

            /// <summary>
            /// Error message if creation failed
            /// </summary>
            public string? ErrorMessage { get; init; }

            /// <summary>
            /// Type of validation failure
            /// </summary>
            public ValidationFailureType FailureType { get; init; }
        }

        /// <summary>
        /// Types of validation failures
        /// </summary>
        public enum ValidationFailureType
        {
            None,
            EmptyPath,
            InvalidPath,
            NullPlugin,
            InvalidName
        }

        /// <summary>
        /// Validates and normalizes a tree node name
        /// </summary>
        /// <param name="path">The node path</param>
        /// <returns>Node creation result with normalized name</returns>
        public static NodeCreationResult ValidateAndNormalizeNode(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Path cannot be null or empty",
                    FailureType = ValidationFailureType.EmptyPath
                };
            }

            // Use TreeLogic for safe string handling
            var safePath = TreeLogic.SafeString(path);
            if (string.IsNullOrEmpty(safePath))
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Path contains invalid characters",
                    FailureType = ValidationFailureType.InvalidPath
                };
            }

            // Extract and normalize the node name
            var nodeName = TreeLogic.GetLeafName(safePath);
            var normalizedName = TreeLogic.SafeString(nodeName);

            if (string.IsNullOrEmpty(normalizedName))
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Node name could not be determined from path",
                    FailureType = ValidationFailureType.InvalidName
                };
            }

            return new NodeCreationResult
            {
                Success = true,
                NodeName = normalizedName
            };
        }

        /// <summary>
        /// Creates a directory node specification for building
        /// </summary>
        /// <param name="directoryPath">The directory path</param>
        /// <param name="plugin">The filesystem plugin</param>
        /// <returns>Node creation result</returns>
        public static NodeCreationResult CreateDirectoryNodeSpec(string directoryPath, IFileSystemPlugin? plugin)
        {
            if (plugin == null)
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Plugin cannot be null for directory nodes",
                    FailureType = ValidationFailureType.NullPlugin
                };
            }

            var validationResult = ValidateAndNormalizeNode(directoryPath);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            return new NodeCreationResult
            {
                Success = true,
                NodeName = validationResult.NodeName
            };
        }

        /// <summary>
        /// Creates a file node specification for building
        /// </summary>
        /// <param name="filePath">The file path</param>
        /// <param name="plugin">The filesystem plugin</param>
        /// <returns>Node creation result</returns>
        public static NodeCreationResult CreateFileNodeSpec(string filePath, IFileSystemPlugin? plugin)
        {
            if (plugin == null)
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Plugin cannot be null for file nodes",
                    FailureType = ValidationFailureType.NullPlugin
                };
            }

            var validationResult = ValidateAndNormalizeNode(filePath);
            if (!validationResult.Success)
            {
                return validationResult;
            }

            return new NodeCreationResult
            {
                Success = true,
                NodeName = validationResult.NodeName
            };
        }

        /// <summary>
        /// Determines if a path should be treated as a root path
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is a root path</returns>
        public static bool IsRootPath(string? path)
        {
            return TreeLogic.IsRootOrEmpty(path) || path == "/";
        }

        /// <summary>
        /// Validates plugin information for root node creation
        /// </summary>
        /// <param name="pluginName">The plugin name</param>
        /// <param name="plugin">The plugin instance</param>
        /// <returns>Node creation result</returns>
        public static NodeCreationResult ValidatePluginRoot(string pluginName, IFileSystemPlugin? plugin)
        {
            if (plugin == null)
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Plugin instance cannot be null",
                    FailureType = ValidationFailureType.NullPlugin
                };
            }

            var normalizedName = TreeLogic.SafeString(pluginName);
            if (string.IsNullOrEmpty(normalizedName))
            {
                return new NodeCreationResult
                {
                    Success = false,
                    ErrorMessage = "Plugin name cannot be null or empty",
                    FailureType = ValidationFailureType.InvalidName
                };
            }

            return new NodeCreationResult
            {
                Success = true,
                NodeName = normalizedName
            };
        }

        /// <summary>
        /// Generates a description for node creation operations
        /// </summary>
        /// <param name="nodeType">Type of node (directory/file)</param>
        /// <param name="nodeName">Name of the node</param>
        /// <param name="pluginName">Name of the plugin</param>
        /// <returns>Descriptive string for the operation</returns>
        public static string GenerateNodeDescription(string nodeType, string nodeName, string? pluginName = null)
        {
            var safeNodeType = TreeLogic.SafeString(nodeType);
            var safeNodeName = TreeLogic.SafeString(nodeName);
            var safePluginName = TreeLogic.SafeString(pluginName);

            return string.IsNullOrEmpty(safePluginName)
                ? $"{safeNodeType}: {safeNodeName}"
                : $"{safeNodeType}: {safeNodeName} ({safePluginName})";
        }

        /// <summary>
        /// Groups nodes by plugin for organized display
        /// </summary>
        /// <typeparam name="T">Type of nodes</typeparam>
        /// <param name="nodes">Collection of nodes</param>
        /// <returns>Dictionary grouped by plugin name</returns>
        public static Dictionary<string, List<T>> GroupNodesByPlugin<T>(IEnumerable<T> nodes) where T : IBuildableNode
        {
            return nodes
                .Where(n => n?.Plugin != null)
                .GroupBy(n => TreeLogic.SafeString(n.Plugin!.Name))
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        /// <summary>
        /// Filters nodes based on search criteria
        /// </summary>
        /// <typeparam name="T">Type of nodes</typeparam>
        /// <param name="nodes">Collection of nodes to filter</param>
        /// <param name="searchTerm">Search term (case-insensitive)</param>
        /// <param name="includeDirectories">Whether to include directories in results</param>
        /// <param name="includeFiles">Whether to include files in results</param>
        /// <returns>Filtered collection of nodes</returns>
        public static IEnumerable<T> FilterNodes<T>(
            IEnumerable<T> nodes,
            string? searchTerm,
            bool includeDirectories = true,
            bool includeFiles = true) where T : IBuildableNode
        {
            if (nodes == null)
                return Enumerable.Empty<T>();

            var filtered = nodes;

            // Filter by node type
            if (!includeDirectories)
                filtered = filtered.Where(n => !n.IsDirectory);
            if (!includeFiles)
                filtered = filtered.Where(n => n.IsDirectory);

            // Filter by search term
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var searchLower = searchTerm.Trim().ToLowerInvariant();
                filtered = filtered.Where(n =>
                    n.Name?.ToLowerInvariant().Contains(searchLower) == true);
            }

            return filtered;
        }

        /// <summary>
        /// Calculates tree statistics for a collection of nodes
        /// </summary>
        /// <typeparam name="T">Type of nodes</typeparam>
        /// <param name="nodes">Collection of nodes</param>
        /// <returns>Tree statistics</returns>
        public static TreeStatistics CalculateTreeStatistics<T>(IEnumerable<T> nodes) where T : IBuildableNode
        {
            if (nodes == null)
                return new TreeStatistics();

            var nodeList = nodes.ToList();
            var directories = nodeList.Count(n => n.IsDirectory);
            var files = nodeList.Count(n => !n.IsDirectory);

            var plugins = nodeList
                .Where(n => n?.Plugin != null)
                .Select(n => n.Plugin!.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            return new TreeStatistics
            {
                TotalNodes = nodeList.Count,
                DirectoryCount = directories,
                FileCount = files,
                PluginCount = plugins.Count,
                PluginNames = plugins
            };
        }

        /// <summary>
        /// Tree statistics information
        /// </summary>
        public class TreeStatistics
        {
            public int TotalNodes { get; init; }
            public int DirectoryCount { get; init; }
            public int FileCount { get; init; }
            public int PluginCount { get; init; }
            public List<string> PluginNames { get; init; } = [];
        }
    }
}
