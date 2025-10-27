using MeshNoteLM.Interfaces;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Requests;
using Google.Apis.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MeshNoteLM.Plugins
{
    /// <summary>
    /// Information about a Google Drive file
    /// </summary>
    public class GoogleDriveFileInfo
    {
        public string FileId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? MimeType { get; set; }
        public long? Size { get; set; }
        public DateTime? CreatedTime { get; set; }
        public DateTime? ModifiedTime { get; set; }
        public string? WebViewLink { get; set; }
        public string? ThumbnailLink { get; set; }
        public string? OwnerName { get; set; }
        public bool IsShared { get; set; }
        public bool IsGoogleWorkspace { get; set; }
    }

    public class GoogleDrivePlugin : PluginBase, IFileSystemPlugin
    {
        private readonly ILogger<GoogleDrivePlugin> _logger;
        private readonly bool _hasValidCredentials;

        public override string Name => "Google Drive";
        public override string Version => "0.1";
        public override string Description => "Access to Google Drive";
        public override string Author => "Starglass Technology";

        // File Operations
        private readonly DriveService _driveService;
        private readonly string _rootFolderId;

        // Cache: path → ID
        private readonly Dictionary<string, string> _fileIdCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _folderIdCache = new(StringComparer.OrdinalIgnoreCase);

        public GoogleDrivePlugin(string? credentialsPath = null, string? applicationName = null, ILogger<GoogleDrivePlugin>? logger = null, string rootFolderId = "root")
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleDrivePlugin>.Instance;

            var resolvedCredentialsPath = credentialsPath ?? Environment.GetEnvironmentVariable("GOOGLE_DRIVE_CREDENTIALS_PATH") ?? "";
            var resolvedApplicationName = applicationName ?? Environment.GetEnvironmentVariable("GOOGLE_DRIVE_APP_NAME") ?? "MeshNoteLM";

            try
            {
                GoogleCredential? credential = null;
                if (!string.IsNullOrEmpty(resolvedCredentialsPath) && File.Exists(resolvedCredentialsPath))
                {
                    using (var stream = new FileStream(resolvedCredentialsPath, FileMode.Open, FileAccess.Read))
                    {
                        var serviceAccountCredential = Google.Apis.Auth.OAuth2.ServiceAccountCredential.FromServiceAccountData(stream);
                        credential = serviceAccountCredential.ToGoogleCredential()
                            .CreateScoped(DriveService.Scope.Drive);
                    }
                    _hasValidCredentials = true;
                }
                else
                {
                    _hasValidCredentials = false;
                }

                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = resolvedApplicationName
                });

                _rootFolderId = rootFolderId;
                _folderIdCache[string.Empty] = rootFolderId;
                _folderIdCache["/"] = rootFolderId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Google Drive service. Plugin will be disabled.");
                _hasValidCredentials = false;
                // Create a minimal service that won't crash but won't work either
                _driveService = new DriveService(new BaseClientService.Initializer()
                {
                    ApplicationName = resolvedApplicationName
                });
                _rootFolderId = rootFolderId;
                _folderIdCache[string.Empty] = rootFolderId;
                _folderIdCache["/"] = rootFolderId;
            }
        }

        // --- File Operations ---
        public bool FileExists(string path) => GetItemId(path, false) != null;

        public string ReadFile(string path)
        {
            var fileId = GetItemId(path, false) ?? throw new FileNotFoundException($"Google Drive file not found: {path}");
            using var ms = new MemoryStream();
            _driveService.Files.Get(fileId).Download(ms);
            ms.Position = 0;
            using var reader = new StreamReader(ms);
            return reader.ReadToEnd();
        }

        public byte[] ReadFileBytes(string path)
        {
            var fileId = GetItemId(path, false) ?? throw new FileNotFoundException($"Google Drive file not found: {path}");
            using var ms = new MemoryStream();
            _driveService.Files.Get(fileId).Download(ms);
            return ms.ToArray();
        }

        public void WriteFile(string path, string contents, bool overwrite = true)
        {
            var fileId = GetItemId(path, false);

            if (fileId != null && !overwrite)
                throw new IOException("File exists and overwrite is false.");

            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(contents));

            if (fileId == null)
            {
                var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                {
                    Name = Path.GetFileName(path),
                    Parents = [GetParentFolderId(path)]
                };

                var request = _driveService.Files.Create(fileMetadata, stream, "text/plain");
                request.Upload();

                // Cache new ID

                // With this safer assignment:
                if (request.ResponseBody?.Id != null)
                {
                    _fileIdCache[NormalizePath(path)] = request.ResponseBody.Id;
                }
            }
            else
            {
                var request = _driveService.Files.Update(null, fileId, stream, "text/plain");
                request.Upload();
            }
        }

        public void AppendToFile(string path, string contents)
        {
            var current = FileExists(path) ? ReadFile(path) : "";
            WriteFile(path, current + contents, overwrite: true);
        }

        public void DeleteFile(string path)
        {
            var fileId = GetItemId(path, false);
            if (fileId != null)
            {
                _driveService.Files.Delete(fileId).Execute();
                _fileIdCache.Remove(NormalizePath(path));
            }
        }

        // --- Directory Operations ---
        public bool DirectoryExists(string path) => GetItemId(path, true) != null;

        public void CreateDirectory(string path)
        {
            if (DirectoryExists(path)) return;

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(path),
                MimeType = "application/vnd.google-apps.folder",
                Parents = [GetParentFolderId(path)]
            };

            var created = _driveService.Files.Create(fileMetadata).Execute();

            _folderIdCache[NormalizePath(path)] = created.Id;
        }

        public void DeleteDirectory(string path, bool recursive = false)
        {
            var folderId = GetItemId(path, true);
            if (folderId != null)
            {
                _driveService.Files.Delete(folderId).Execute();
                _folderIdCache.Remove(NormalizePath(path));
            }
        }

        // --- Info ---
        public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            var folderId = GetItemId(directoryPath, true) ?? _rootFolderId;
            var query = $"'{folderId}' in parents and mimeType != 'application/vnd.google-apps.folder' and trashed = false";
            var listRequest = _driveService.Files.List();
            listRequest.Q = query;
            var response = listRequest.Execute();
            return response.Files.Select(f => f.Name);
        }

        public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            var folderId = GetItemId(directoryPath, true) ?? _rootFolderId;
            var query = $"'{folderId}' in parents and mimeType = 'application/vnd.google-apps.folder' and trashed = false";
            var listRequest = _driveService.Files.List();
            listRequest.Q = query;
            var response = listRequest.Execute();
            return response.Files.Select(f => f.Name);
        }

        public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1) Directories
            foreach (var d in GetDirectories(directoryPath, searchPattern, searchOption))
            {
                if (seen.Add(d))
                    yield return d;
            }

            // 2) Files
            foreach (var f in GetFiles(directoryPath, searchPattern, searchOption))
            {
                if (seen.Add(f))
                    yield return f;
            }
        }

        public long GetFileSize(string path)
        {
            var fileId = GetItemId(path, false) ?? throw new FileNotFoundException(path);
            var file = _driveService.Files.Get(fileId).Execute();
            return file.Size ?? 0;
        }

        // --- Google Workspace Detection ---

        /// <summary>
        /// Checks if a file is a Google Workspace document (Docs, Sheets, Slides, etc.)
        /// </summary>
        public bool IsGoogleWorkspaceFile(string path)
        {
            var fileId = GetItemId(path, false);
            if (fileId == null) return false;

            var file = _driveService.Files.Get(fileId).Execute();
            return file.MimeType?.StartsWith("application/vnd.google-apps.") ?? false;
        }

        /// <summary>
        /// Gets the Google Workspace file type (Document, Spreadsheet, Presentation, etc.)
        /// Returns null if not a Workspace file
        /// </summary>
        public string? GetWorkspaceFileType(string path)
        {
            var fileId = GetItemId(path, false);
            if (fileId == null) return null;

            var file = _driveService.Files.Get(fileId).Execute();
            if (file.MimeType == null || !file.MimeType.StartsWith("application/vnd.google-apps."))
                return null;

            return file.MimeType switch
            {
                "application/vnd.google-apps.document" => "Document",
                "application/vnd.google-apps.spreadsheet" => "Spreadsheet",
                "application/vnd.google-apps.presentation" => "Presentation",
                "application/vnd.google-apps.form" => "Form",
                "application/vnd.google-apps.drawing" => "Drawing",
                "application/vnd.google-apps.map" => "MyMap",
                "application/vnd.google-apps.site" => "Sites",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Gets detailed file information including Google Workspace metadata
        /// </summary>
        public GoogleDriveFileInfo? GetFileInfo(string path)
        {
            var fileId = GetItemId(path, false);
            if (fileId == null) return null;

            var request = _driveService.Files.Get(fileId);
            request.Fields = "id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, owners, shared";
            var file = request.Execute();

            return new GoogleDriveFileInfo
            {
                FileId = file.Id,
                Name = file.Name,
                MimeType = file.MimeType,
                Size = file.Size,
                CreatedTime = file.CreatedTimeDateTimeOffset?.DateTime,
                ModifiedTime = file.ModifiedTimeDateTimeOffset?.DateTime,
                WebViewLink = file.WebViewLink,
                ThumbnailLink = file.ThumbnailLink,
                OwnerName = file.Owners?.FirstOrDefault()?.DisplayName,
                IsShared = file.Shared ?? false,
                IsGoogleWorkspace = file.MimeType?.StartsWith("application/vnd.google-apps.") ?? false
            };
        }

        /// <summary>
        /// Searches for files in Google Drive
        /// </summary>
        public IEnumerable<GoogleDriveFileInfo> SearchFiles(string query, int maxResults = 100)
        {
            var request = _driveService.Files.List();
            request.Q = $"{query} and trashed = false";
            request.Fields = "files(id, name, mimeType, size, createdTime, modifiedTime, webViewLink, thumbnailLink, owners, shared)";
            request.PageSize = maxResults;

            var response = request.Execute();

            return response.Files.Select(file => new GoogleDriveFileInfo
            {
                FileId = file.Id,
                Name = file.Name,
                MimeType = file.MimeType,
                Size = file.Size,
                CreatedTime = file.CreatedTimeDateTimeOffset?.DateTime,
                ModifiedTime = file.ModifiedTimeDateTimeOffset?.DateTime,
                WebViewLink = file.WebViewLink,
                ThumbnailLink = file.ThumbnailLink,
                OwnerName = file.Owners?.FirstOrDefault()?.DisplayName,
                IsShared = file.Shared ?? false,
                IsGoogleWorkspace = file.MimeType?.StartsWith("application/vnd.google-apps.") ?? false
            });
        }

        /// <summary>
        /// Gets all Google Workspace documents in a directory
        /// </summary>
        public IEnumerable<GoogleDriveFileInfo> GetWorkspaceDocuments(string directoryPath)
        {
            var folderId = GetItemId(directoryPath, true) ?? _rootFolderId;
            var query = $"'{folderId}' in parents and mimeType contains 'application/vnd.google-apps.'";
            return SearchFiles(query);
        }

        // --- Batch metadata lookup ---
        public void PreloadIds(IEnumerable<(string Path, bool IsFolder)> items)
        {
            var batch = new BatchRequest(_driveService);
            foreach (var (path, isFolder) in items)
            {
                var norm = NormalizePath(path);
                if ((isFolder && _folderIdCache.ContainsKey(norm)) || (!isFolder && _fileIdCache.ContainsKey(norm)))
                    continue;

                var name = Path.GetFileName(path);
                var parentId = GetParentFolderId(path);
                var mimeCondition = isFolder
                    ? "mimeType = 'application/vnd.google-apps.folder'"
                    : "mimeType != 'application/vnd.google-apps.folder'";

                var query = $"name = '{name}' and '{parentId}' in parents and {mimeCondition} and trashed = false";
                var req = _driveService.Files.List();
                req.Q = query;

                batch.Queue<Google.Apis.Drive.v3.Data.FileList>(
                    req,
                    (content, error, i, message) =>
                    {
                        if (error == null && content.Files.Any())
                        {
                            var id = content.Files.First().Id;
                            if (isFolder) _folderIdCache[norm] = id;
                            else _fileIdCache[norm] = id;
                        }
                    }
                );
            }
            batch.ExecuteAsync().Wait();
        }

        // --- Helpers ---
        private string GetItemId(string path, bool isFolder)
        {
            var norm = NormalizePath(path);
            if (isFolder && _folderIdCache.TryGetValue(norm, out var cachedId))
                return cachedId;
            if (!isFolder && _fileIdCache.TryGetValue(norm, out var cachedFileId))
                return cachedFileId;

            var name = Path.GetFileName(path);
            var parentId = GetParentFolderId(path);
            var mimeCondition = isFolder
                ? "mimeType = 'application/vnd.google-apps.folder'"
                : "mimeType != 'application/vnd.google-apps.folder'";

            var query = $"name = '{name}' and '{parentId}' in parents and {mimeCondition} and trashed = false";
            var req = _driveService.Files.List();
            req.Q = query;
            var result = req.Execute();

            var id = result.Files.FirstOrDefault()?.Id;
            if (id != null)
            {
                if (isFolder) _folderIdCache[norm] = id;
                else _fileIdCache[norm] = id;
            }
            return id ?? "";
        }

        private string GetParentFolderId(string path)
        {
            var directoryPath = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "";
            if (string.IsNullOrEmpty(directoryPath))
                return _rootFolderId;

            return GetItemId(directoryPath, true) ?? _rootFolderId;
        }

        private static string NormalizePath(string path)
        {
            return path?.Trim().Replace("\\", "/").TrimStart('/') ?? "";
        }

        public override bool HasValidAuthorization()
        {
            return _hasValidCredentials;
        }
    }
}
