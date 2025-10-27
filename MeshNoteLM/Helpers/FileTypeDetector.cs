using System;
using System.IO;

namespace MeshNoteLM.Helpers
{
    /// <summary>
    /// Pure logic class for file type detection and validation.
    /// Contains no UI dependencies - fully testable.
    /// </summary>
    public static class FileTypeDetector
    {
        /// <summary>
        /// File viewer types supported by the application
        /// </summary>
        public enum ViewerType
        {
            None,
            MSOffice,
            OpenOffice,
            Pdf,
            Markdown,
            Text,
            GoogleDocs
        }

        /// <summary>
        /// Checks if a file can be viewed in the application
        /// </summary>
        public static bool CanViewFile(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            return extension switch
            {
                // MS Office
                ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => true,
                // Open Office
                ".odt" or ".ods" or ".odp" => true,
                // PDF
                ".pdf" => true,
                // Markdown
                ".md" or ".markdown" => true,
                // Text files
                ".txt" or ".json" or ".xml" or ".cs" or ".xaml" or ".html" or ".css" or ".js" => true,
                _ => false
            };
        }

        /// <summary>
        /// Determines the appropriate viewer type for a file
        /// </summary>
        public static ViewerType GetViewerType(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();

            return extension switch
            {
                ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => ViewerType.MSOffice,
                ".odt" or ".ods" or ".odp" => ViewerType.OpenOffice,
                ".pdf" => ViewerType.Pdf,
                ".md" or ".markdown" => ViewerType.Markdown,
                ".txt" or ".json" or ".xml" or ".cs" or ".xaml" or ".html" or ".css" or ".js" => ViewerType.Text,
                _ => ViewerType.None
            };
        }

        /// <summary>
        /// Checks if a path is a Google Docs URL
        /// </summary>
        public static bool IsGoogleDocsUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return path.Contains("docs.google.com/document") ||
                   path.Contains("docs.google.com/spreadsheets") ||
                   path.Contains("docs.google.com/presentation") ||
                   path.Contains("docs.google.com/forms") ||
                   path.Contains("docs.google.com/drawings");
        }

        /// <summary>
        /// Checks if a file extension is for Microsoft Office
        /// </summary>
        public static bool IsMSOfficeFile(string fileName)
        {
            return GetViewerType(fileName) == ViewerType.MSOffice;
        }

        /// <summary>
        /// Checks if a file extension is for Open Office
        /// </summary>
        public static bool IsOpenOfficeFile(string fileName)
        {
            return GetViewerType(fileName) == ViewerType.OpenOffice;
        }

        /// <summary>
        /// Checks if a file extension is for PDF
        /// </summary>
        public static bool IsPdfFile(string fileName)
        {
            return GetViewerType(fileName) == ViewerType.Pdf;
        }

        /// <summary>
        /// Checks if a file extension is for Markdown
        /// </summary>
        public static bool IsMarkdownFile(string fileName)
        {
            return GetViewerType(fileName) == ViewerType.Markdown;
        }

        /// <summary>
        /// Checks if a file extension is for plain text
        /// </summary>
        public static bool IsTextFile(string fileName)
        {
            return GetViewerType(fileName) == ViewerType.Text;
        }

        /// <summary>
        /// Gets a user-friendly description of the file type
        /// </summary>
        public static string GetFileTypeDescription(string fileName)
        {
            return GetViewerType(fileName) switch
            {
                ViewerType.MSOffice => "Microsoft Office Document",
                ViewerType.OpenOffice => "OpenOffice Document",
                ViewerType.Pdf => "PDF Document",
                ViewerType.Markdown => "Markdown Document",
                ViewerType.Text => "Text File",
                ViewerType.GoogleDocs => "Google Docs Document",
                _ => "Unknown File Type"
            };
        }
    }
}
