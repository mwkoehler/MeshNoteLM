using System;
using System.Collections.Generic;
using System.Linq;
using MeshNoteLM.Plugins;

namespace MeshNoteLM.Helpers
{
    /// <summary>
    /// Pure logic class for file viewer operations.
    /// Contains no UI dependencies - fully testable.
    /// </summary>
    public static class FileViewerLogic
    {
        /// <summary>
        /// Result of a viewer creation decision
        /// </summary>
        public class ViewerDecision
        {
            /// <summary>
            /// The type of viewer to create
            /// </summary>
            public FileTypeDetector.ViewerType ViewerType { get; init; }

            /// <summary>
            /// Whether conversion is required for this viewer type
            /// </summary>
            public bool RequiresConversion { get; init; }

            /// <summary>
            /// Whether the viewer type is supported
            /// </summary>
            public bool IsSupported { get; init; }

            /// <summary>
            /// Error message if not supported
            /// </summary>
            public string? ErrorMessage { get; init; }

            /// <summary>
            /// User-friendly description of the viewer action
            /// </summary>
            public string ActionDescription { get; init; } = string.Empty;
        }

        /// <summary>
        /// Determines the appropriate viewer action for a file
        /// </summary>
        /// <param name="fileName">The file name to analyze</param>
        /// <returns>Viewer decision with action information</returns>
        public static ViewerDecision GetViewerDecision(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return new ViewerDecision
                {
                    ViewerType = FileTypeDetector.ViewerType.None,
                    IsSupported = false,
                    ErrorMessage = "File name cannot be empty",
                    ActionDescription = "Invalid file"
                };
            }

            var viewerType = FileTypeDetector.GetViewerType(fileName);

            return viewerType switch
            {
                FileTypeDetector.ViewerType.MSOffice => new ViewerDecision
                {
                    ViewerType = viewerType,
                    IsSupported = true,
                    RequiresConversion = true,
                    ActionDescription = $"Convert {FileTypeDetector.GetFileTypeDescription(fileName)} to PDF for viewing"
                },

                FileTypeDetector.ViewerType.OpenOffice => new ViewerDecision
                {
                    ViewerType = viewerType,
                    IsSupported = true,
                    RequiresConversion = false,
                    ActionDescription = $"View {FileTypeDetector.GetFileTypeDescription(fileName)} directly"
                },

                FileTypeDetector.ViewerType.Pdf => new ViewerDecision
                {
                    ViewerType = viewerType,
                    IsSupported = true,
                    RequiresConversion = false,
                    ActionDescription = "View PDF document"
                },

                FileTypeDetector.ViewerType.Markdown => new ViewerDecision
                {
                    ViewerType = viewerType,
                    IsSupported = true,
                    RequiresConversion = false,
                    ActionDescription = "Render Markdown as HTML"
                },

                FileTypeDetector.ViewerType.Text => new ViewerDecision
                {
                    ViewerType = viewerType,
                    IsSupported = true,
                    RequiresConversion = false,
                    ActionDescription = "Open in text editor"
                },

                _ => new ViewerDecision
                {
                    ViewerType = FileTypeDetector.ViewerType.None,
                    IsSupported = false,
                    ErrorMessage = $"No viewer available for {System.IO.Path.GetExtension(fileName)} files",
                    ActionDescription = "Unsupported file type"
                }
            };
        }

        /// <summary>
        /// Interface for Google Workspace detection to avoid direct plugin dependency
        /// </summary>
        public interface IGoogleWorkspaceDetector
        {
            bool IsGoogleWorkspaceFile(string path);
        }

        /// <summary>
        /// Determines if a Google Drive file requires special handling
        /// </summary>
        /// <param name="path">The file path or URL</param>
        /// <param name="detector">Optional Google workspace detector</param>
        /// <returns>True if the file requires Google Workspace handling</returns>
        public static bool RequiresGoogleWorkspaceHandling(string path, IGoogleWorkspaceDetector? detector = null)
        {
            // Check if it's a Google Docs URL using pure logic
            if (FileTypeDetector.IsGoogleDocsUrl(path))
            {
                return true;
            }

            // If a detector is provided, check if file is a Workspace document
            if (detector != null)
            {
                try
                {
                    return detector.IsGoogleWorkspaceFile(path);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Formats file size for display
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted file size string</returns>
        public static string FormatFileSize(long bytes)
        {
            if (bytes < 0)
                return "0 bytes";

            string[] sizes = { "bytes", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Generates an appropriate error message for file viewer failures
        /// </summary>
        /// <param name="fileName">The file name that failed to load</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="viewerType">The type of viewer that failed</param>
        /// <returns>User-friendly error message</returns>
        public static string GenerateErrorMessage(string fileName, Exception exception, FileTypeDetector.ViewerType viewerType)
        {
            var fileExtension = System.IO.Path.GetExtension(fileName);
            var viewerDescription = viewerType != FileTypeDetector.ViewerType.None
                ? viewerType.ToString()
                : "Unknown";

            return exception.Message.Contains("conversion", StringComparison.OrdinalIgnoreCase)
                ? $"Failed to convert {fileExtension} file for viewing: {exception.Message}"
                : $"Error loading {viewerDescription} viewer for {fileName}: {exception.Message}";
        }

        /// <summary>
        /// Validates file data before attempting to create a viewer
        /// </summary>
        /// <param name="fileData">The file data to validate</param>
        /// <param name="fileName">The file name for context</param>
        /// <returns>Validation result with any issues found</returns>
        public static FileValidationResult ValidateFileData(byte[] fileData, string fileName)
        {
            if (fileData == null)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File data is null",
                    Severity = ValidationSeverity.Error
                };
            }

            if (fileData.Length == 0)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "File is empty",
                    Severity = ValidationSeverity.Error
                };
            }

            // Check for very large files (>100MB)
            if (fileData.Length > 100 * 1024 * 1024)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"File is too large ({FormatFileSize(fileData.Length)}). Maximum supported size is 100 MB.",
                    Severity = ValidationSeverity.Warning
                };
            }

            // Basic file type consistency check
            var expectedType = FileTypeDetector.GetViewerType(fileName);
            if (expectedType == FileTypeDetector.ViewerType.None)
            {
                return new FileValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Unsupported file type: {System.IO.Path.GetExtension(fileName)}",
                    Severity = ValidationSeverity.Error
                };
            }

            return new FileValidationResult
            {
                IsValid = true,
                FileSize = fileData.Length,
                ViewerType = expectedType
            };
        }

        /// <summary>
        /// Result of file validation
        /// </summary>
        public class FileValidationResult
        {
            public bool IsValid { get; init; }
            public string? ErrorMessage { get; init; }
            public ValidationSeverity Severity { get; init; }
            public long FileSize { get; init; }
            public FileTypeDetector.ViewerType ViewerType { get; init; }
        }

        /// <summary>
        /// Validation severity levels
        /// </summary>
        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }
    }
}
