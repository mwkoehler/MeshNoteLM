using System;
using System.IO;

namespace MeshNoteLM.Helpers
{
    /// <summary>
    /// Pure logic class for secure path operations and validation.
    /// Contains no filesystem I/O dependencies - fully testable.
    /// Provides path traversal prevention and security validation.
    /// </summary>
    public static class PathSecurityLogic
    {
        /// <summary>
        /// Result of secure path combination operation
        /// </summary>
        public class SecurePathResult
        {
            /// <summary>
            /// Whether the operation succeeded
            /// </summary>
            public bool IsValid { get; init; }

            /// <summary>
            /// The resulting full path if valid
            /// </summary>
            public string? FullPath { get; init; }

            /// <summary>
            /// Error message if invalid
            /// </summary>
            public string? ErrorMessage { get; init; }

            /// <summary>
            /// Type of security violation if invalid
            /// </summary>
            public SecurityViolationType ViolationType { get; init; }
        }

        /// <summary>
        /// Types of security violations
        /// </summary>
        public enum SecurityViolationType
        {
            None,
            AbsolutePathNotAllowed,
            PathEscapesRoot,
            InvalidPath
        }

        /// <summary>
        /// Checks if a path represents a root directory.
        /// </summary>
        /// <param name="path">The path to check</param>
        /// <returns>True if the path is root (null, empty, "/", or "\")</returns>
        public static bool IsRootPath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ||
                   path == "/" ||
                   path == "\\";
        }

        /// <summary>
        /// Normalizes path separators to forward slashes.
        /// </summary>
        /// <param name="path">The path to normalize</param>
        /// <returns>Path with forward slashes</returns>
        public static string NormalizePathSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.Replace('\\', '/');
        }

        /// <summary>
        /// Trims leading slashes from a path.
        /// </summary>
        /// <param name="path">The path to trim</param>
        /// <returns>Path without leading slashes</returns>
        public static string TrimLeadingSlashes(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            return path.TrimStart('/', '\\');
        }

        /// <summary>
        /// Validates that a path is not rooted (not absolute).
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <returns>True if the path is relative (not rooted)</returns>
        public static bool IsRelativePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            return !Path.IsPathRooted(path);
        }

        /// <summary>
        /// Validates that a combined path stays within the root directory.
        /// Prevents path traversal attacks using ../ sequences.
        /// </summary>
        /// <param name="rootPath">The root directory path</param>
        /// <param name="combinedPath">The combined full path to validate</param>
        /// <returns>True if the path is within the root</returns>
        public static bool IsWithinRoot(string rootPath, string combinedPath)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(combinedPath))
                return false;

            var rootWithSep = rootPath.EndsWith(Path.DirectorySeparatorChar)
                ? rootPath
                : rootPath + Path.DirectorySeparatorChar;

            return combinedPath.StartsWith(rootWithSep, StringComparison.Ordinal) ||
                   combinedPath.Equals(rootPath, StringComparison.Ordinal);
        }

        /// <summary>
        /// Securely combines a root path with a relative path, with full validation.
        /// Prevents absolute paths, path traversal attacks, and paths escaping the root.
        /// </summary>
        /// <param name="rootPath">The root directory path</param>
        /// <param name="relativePath">The relative path to combine</param>
        /// <returns>Result containing the full path or error information</returns>
        public static SecurePathResult SecureCombine(string rootPath, string relativePath)
        {
            try
            {
                // Handle root directory
                if (IsRootPath(relativePath))
                {
                    return new SecurePathResult
                    {
                        IsValid = true,
                        FullPath = rootPath,
                        ViolationType = SecurityViolationType.None
                    };
                }

                // Normalize separators and trim leading slashes
                var normalized = TrimLeadingSlashes(NormalizePathSeparators(relativePath));

                // Disallow absolute paths (after trimming leading slash)
                if (!IsRelativePath(normalized))
                {
                    return new SecurePathResult
                    {
                        IsValid = false,
                        ErrorMessage = "Absolute paths are not allowed.",
                        ViolationType = SecurityViolationType.AbsolutePathNotAllowed
                    };
                }

                // Combine & canonicalize
                var combined = Path.GetFullPath(Path.Combine(rootPath, normalized));

                // Ensure stays under root (prevents ../ traversal)
                if (!IsWithinRoot(rootPath, combined))
                {
                    return new SecurePathResult
                    {
                        IsValid = false,
                        ErrorMessage = "Path escapes the allowed root.",
                        ViolationType = SecurityViolationType.PathEscapesRoot
                    };
                }

                return new SecurePathResult
                {
                    IsValid = true,
                    FullPath = combined,
                    ViolationType = SecurityViolationType.None
                };
            }
            catch (Exception ex)
            {
                return new SecurePathResult
                {
                    IsValid = false,
                    ErrorMessage = $"Invalid path: {ex.Message}",
                    ViolationType = SecurityViolationType.InvalidPath
                };
            }
        }

        /// <summary>
        /// Converts an absolute path to a relative path from a root directory.
        /// Normalizes path separators to forward slashes.
        /// </summary>
        /// <param name="rootPath">The root directory path</param>
        /// <param name="fullPath">The full path to convert</param>
        /// <returns>Relative path with forward slashes</returns>
        public static string ToRelativePath(string rootPath, string fullPath)
        {
            if (string.IsNullOrEmpty(rootPath) || string.IsNullOrEmpty(fullPath))
                return string.Empty;

            var relativePath = Path.GetRelativePath(rootPath, fullPath);
            return NormalizePathSeparators(relativePath);
        }

        /// <summary>
        /// Validates a path for common security issues.
        /// </summary>
        /// <param name="path">The path to validate</param>
        /// <returns>True if the path passes basic security validation</returns>
        public static bool IsSecurePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return true; // Empty paths are handled separately

            // Check for null bytes
            if (path.Contains('\0'))
                return false;

            // Check for common path traversal patterns
            if (path.Contains(".."))
                return false;

            return true;
        }

        /// <summary>
        /// Gets the parent directory path from a relative path.
        /// </summary>
        /// <param name="relativePath">The relative path</param>
        /// <returns>The parent directory path, or empty string for root-level paths</returns>
        public static string GetParentRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return string.Empty;

            var normalized = NormalizePathSeparators(relativePath).TrimEnd('/');

            var lastSlashIndex = normalized.LastIndexOf('/');
            if (lastSlashIndex <= 0)
                return string.Empty;

            return normalized[..lastSlashIndex];
        }
    }
}
