using System;
using System.Collections.Generic;
using System.Linq;

namespace MeshNoteLM.Helpers
{
    /// <summary>
    /// Pure logic class for tree building operations.
    /// Contains no UI or ViewModel dependencies - fully testable.
    /// </summary>
    public static class TreeLogic
    {
        /// <summary>
        /// Represents a node in the tree for sorting purposes
        /// </summary>
        public interface ITreeNode
        {
            string Name { get; }
            bool IsDirectory { get; }
        }

        /// <summary>
        /// Extracts the leaf name (final segment) from a path.
        /// Handles various path formats including trailing slashes.
        /// </summary>
        /// <param name="path">The path to parse (e.g., "/folder/file.txt" or "folder/")</param>
        /// <returns>The leaf name (e.g., "file.txt" or "folder")</returns>
        public static string GetLeafName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path ?? "";

            var trimmedPath = path.TrimEnd('/');

            // Special case: root path "/"
            if (trimmedPath == string.Empty && path.Contains('/'))
                return "/";

            var lastSlashIndex = trimmedPath.LastIndexOf('/');
            var leaf = lastSlashIndex >= 0 ? trimmedPath[(lastSlashIndex + 1)..] : trimmedPath;

            return string.IsNullOrEmpty(leaf) ? trimmedPath : leaf;
        }

        /// <summary>
        /// Ensures a string is non-null and non-whitespace, returning empty string if invalid.
        /// </summary>
        /// <param name="value">The string to validate</param>
        /// <returns>The original string or empty string if null/whitespace</returns>
        public static string SafeString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value!;
        }

        /// <summary>
        /// Sorts tree nodes with directories first, then files, alphabetically by name.
        /// </summary>
        /// <typeparam name="T">The type of tree node</typeparam>
        /// <param name="nodes">The collection of nodes to sort</param>
        /// <returns>Sorted collection with directories first, then files, each group alphabetically sorted</returns>
        public static IEnumerable<T> SortTreeNodes<T>(IEnumerable<T> nodes) where T : ITreeNode
        {
            return nodes
                .OrderBy(n => n.IsDirectory ? 0 : 1)  // Directories (0) before files (1)
                .ThenBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase);
        }

        /// <summary>
        /// Determines the sort priority for a node (0 for directories, 1 for files).
        /// Used for custom sorting scenarios.
        /// </summary>
        /// <param name="isDirectory">Whether the node is a directory</param>
        /// <returns>0 for directories, 1 for files</returns>
        public static int GetNodeSortPriority(bool isDirectory)
        {
            return isDirectory ? 0 : 1;
        }

        /// <summary>
        /// Validates if a path is empty or represents a root path.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is null, empty, whitespace, or just "/"</returns>
        public static bool IsRootOrEmpty(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true;

            var trimmed = path.Trim();
            return trimmed == "/" || trimmed == string.Empty;
        }

        /// <summary>
        /// Normalizes a path by removing trailing slashes.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>The normalized path without trailing slashes</returns>
        public static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            return path.TrimEnd('/');
        }

        /// <summary>
        /// Combines a parent path with a child name to create a full path.
        /// </summary>
        /// <param name="parentPath">The parent directory path</param>
        /// <param name="childName">The child file or directory name</param>
        /// <returns>The combined path (e.g., "/parent/child")</returns>
        public static string CombinePath(string parentPath, string childName)
        {
            if (string.IsNullOrWhiteSpace(childName))
                return NormalizePath(parentPath ?? string.Empty);

            if (string.IsNullOrWhiteSpace(parentPath))
                return $"/{childName}";

            var normalizedParent = NormalizePath(parentPath);

            // If parent is root or empty, don't double-slash
            if (normalizedParent == string.Empty || normalizedParent == "/")
                return $"/{childName}";

            return $"{normalizedParent}/{childName}";
        }

        /// <summary>
        /// Gets the parent path from a full path.
        /// </summary>
        /// <param name="path">The full path</param>
        /// <returns>The parent path, or "/" for top-level paths</returns>
        public static string GetParentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            var normalized = NormalizePath(path);
            var lastSlashIndex = normalized.LastIndexOf('/');

            if (lastSlashIndex <= 0)
                return "/";

            return normalized[..lastSlashIndex];
        }
    }
}
