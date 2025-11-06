/*
================================================================================
Gmail Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin via PluginBase

Uses Gmail API (Google.Apis.Gmail.v1) to access Gmail messages

Virtual filesystem structure:
/                           - Root directory listing all system labels
/Inbox/                     - Directory containing inbox messages
/Sent/                      - Directory containing sent messages
/Drafts/                    - Directory containing draft messages
/[LabelName]/               - Directory for each custom Gmail label
/Inbox/Subject_[MessageId].eml - Individual email messages as .eml files
/Sent/Subject_[MessageId].eml - Sent messages as .eml files

Authentication:
- Uses Google OAuth2 (same as Google Drive plugin)
- Requires Gmail API scope: https://www.googleapis.com/auth/gmail.readonly
- Shares credentials with Google Drive plugin
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Plugins
{
    /// <summary>
    /// Information about a Gmail message
    /// </summary>
    public class GmailMessageInfo
    {
        public string Id { get; set; } = string.Empty;
        public string ThreadId { get; set; } = string.Empty;
        public string? Subject { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public string? Date { get; set; }
        public string? Snippet { get; set; }
        public List<string> LabelIds { get; set; } = new();
        public bool IsRead { get; set; }
        public long? SizeEstimate { get; set; }
    }

    public class GmailPlugin : PluginBase, IFileSystemPlugin
    {
        private const string GMAIL_API_SCOPE = "https://www.googleapis.com/auth/gmail.readonly";
        private const string CREDENTIALS_FILE = "gmail_credentials.json";
        private const string USER = "me";

        private GmailService? _gmailService;
        private readonly Dictionary<string, string> _systemLabels = new()
        {
            ["INBOX"] = "Inbox",
            ["SENT"] = "Sent",
            ["DRAFT"] = "Drafts",
            ["SPAM"] = "Spam",
            ["TRASH"] = "Trash",
            ["UNREAD"] = "Unread",
            ["STARRED"] = "Starred",
            ["IMPORTANT"] = "Important",
            ["CATEGORY_PERSONAL"] = "Personal",
            ["CATEGORY_SOCIAL"] = "Social",
            ["CATEGORY_PROMOTIONS"] = "Promotions",
            ["CATEGORY_UPDATES"] = "Updates",
            ["CATEGORY_FORUMS"] = "Forums"
        };

        public override string Name => "Gmail";
        public override string Version => "1.0";
        public override string Description => "Access Gmail messages as filesystem";
        public override string Author => "Starglass Technology";

        public override async Task InitializeAsync()
        {
            try
            {
                await InitializeGmailService();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] InitializeAsync error: {ex.Message}");
                throw;
            }
        }

        public override bool HasValidAuthorization()
        {
            try
            {
                return _gmailService != null;
            }
            catch
            {
                return false;
            }
        }

        public override async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                if (!HasValidAuthorization())
                {
                    return (false, "Invalid - Not authenticated with Gmail");
                }

                if (_gmailService == null)
                {
                    return (false, "Error - Gmail service not initialized");
                }

                // Test with a simple API call - get user profile
                var profileRequest = _gmailService.Users.GetProfile(USER);
                var profile = await profileRequest.ExecuteAsync();

                return (true, $"✓ Valid - Connected to Gmail ({profile.EmailAddress})");
            }
            catch (Exception ex)
            {
                return (false, $"Error - {ex.Message}");
            }
        }

        private async Task InitializeGmailService()
        {
            try
            {
                // Get credentials from settings service (like other plugins)
                var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
                var clientId = settingsService?.GoogleClientId ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID") ?? "";
                var clientSecret = settingsService?.GoogleClientSecret ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET") ?? "";

                if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                {
                    throw new InvalidOperationException("Google Client ID and Client Secret must be configured in Settings");
                }

                // Use OAuth2 credential mechanism for Gmail
                var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    new ClientSecrets
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret
                    },
                    new[] { GMAIL_API_SCOPE },
                    "MeshNoteLM_Gmail",
                    System.Threading.CancellationToken.None,
                    new FileDataStore(CREDENTIALS_FILE, fullPath: true));

                _gmailService = new GmailService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MeshNoteLM"
                });

                System.Diagnostics.Debug.WriteLine("[GmailPlugin] Gmail service initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] InitializeGmailService error: {ex.Message}");
                throw;
            }
        }

        // File system operations implementation
        public bool FileExists(string path)
        {
            try
            {
                var (labelId, messageId) = ParseFilePath(path);
                return !string.IsNullOrEmpty(labelId) && !string.IsNullOrEmpty(messageId);
            }
            catch
            {
                return false;
            }
        }

        public string ReadFile(string path)
        {
            try
            {
                if (_gmailService == null)
                    throw new InvalidOperationException("Gmail service not initialized");

                var (labelId, messageId) = ParseFilePath(path);
                if (string.IsNullOrEmpty(messageId))
                    throw new FileNotFoundException($"Message ID not found in path: {path}");

                var request = _gmailService.Users.Messages.Get(USER, messageId);
                request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Raw;
                var message = request.Execute();

                // Decode the raw message
                var rawBytes = Convert.FromBase64String(message.Raw.Replace('-', '+').Replace('_', '/'));
                return Encoding.UTF8.GetString(rawBytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] ReadFile error: {ex.Message}");
                throw;
            }
        }

        public byte[] ReadFileBytes(string path)
        {
            return Encoding.UTF8.GetBytes(ReadFile(path));
        }

        public void WriteFile(string path, string contents, bool overwrite = true)
        {
            throw new NotSupportedException("Gmail plugin is read-only");
        }

        public void AppendToFile(string path, string contents)
        {
            throw new NotSupportedException("Gmail plugin is read-only");
        }

        public void DeleteFile(string path)
        {
            throw new NotSupportedException("Gmail plugin is read-only");
        }

        // Directory operations
        public bool DirectoryExists(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || path == "/")
                    return true;

                var normalizedPath = NormalizePath(path);
                if (normalizedPath == "/")
                    return true;

                // Check if it's a system label
                if (_systemLabels.ContainsKey(normalizedPath.Trim('/').ToUpper()))
                    return true;

                // Check if it's a custom label by querying labels
                return Task.Run(async () => await GetCustomLabels()).Result
                    .Any(l => l.Name != null && l.Name.Equals(normalizedPath.Trim('/'), StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        public void CreateDirectory(string path)
        {
            throw new NotSupportedException("Gmail plugin is read-only");
        }

        public void DeleteDirectory(string path, bool recursive = false)
        {
            throw new NotSupportedException("Gmail plugin is read-only");
        }

        // File and directory listing
        public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            try
            {
                if (_gmailService == null)
                    return Enumerable.Empty<string>();

                var normalizedPath = NormalizePath(directoryPath);
                var labelId = GetLabelIdFromPath(normalizedPath);

                if (string.IsNullOrEmpty(labelId))
                    return Enumerable.Empty<string>();

                var messages = Task.Run(async () => await GetMessagesInLabel(labelId)).Result;
                return messages.Select(msg => $"{normalizedPath}/{SanitizeFileName(msg.Subject ?? "No Subject")}_{msg.Id}.eml");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] GetFiles error: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            try
            {
                var normalizedPath = NormalizePath(directoryPath);

                if (normalizedPath == "/")
                {
                    // Return all available labels as directories
                    var directories = _systemLabels.Values.Select(label => $"/{label}").ToList();

                    // Add custom labels
                    var customLabels = Task.Run(async () => await GetCustomLabels()).Result;
                    directories.AddRange(customLabels.Select(label => $"/{SanitizeDirectoryName(label.Name ?? "")}"));

                    return directories;
                }

                return Enumerable.Empty<string>(); // No subdirectories in Gmail labels
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] GetDirectories error: {ex.Message}");
                return Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
        {
            return GetDirectories(directoryPath, searchPattern, searchOption)
                   .Union(GetFiles(directoryPath, searchPattern, searchOption));
        }

        public long GetFileSize(string path)
        {
            try
            {
                if (_gmailService == null)
                    return 0;

                var (_, messageId) = ParseFilePath(path);
                if (string.IsNullOrEmpty(messageId))
                    return 0;

                var request = _gmailService.Users.Messages.Get(USER, messageId);
                request.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                var message = request.Execute();

                return message.SizeEstimate ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // Helper methods
        private async Task<List<Google.Apis.Gmail.v1.Data.Label>> GetCustomLabels()
        {
            try
            {
                if (_gmailService == null)
                    return new List<Google.Apis.Gmail.v1.Data.Label>();

                var request = _gmailService.Users.Labels.List(USER);
                var labels = await request.ExecuteAsync();

                return labels.Labels?
                    .Where(l => l.Type == "user" && !string.IsNullOrEmpty(l.Name))
                    .ToList() ?? new List<Google.Apis.Gmail.v1.Data.Label>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] GetCustomLabels error: {ex.Message}");
                return new List<Google.Apis.Gmail.v1.Data.Label>();
            }
        }

        private async Task<List<GmailMessageInfo>> GetMessagesInLabel(string labelId)
        {
            try
            {
                if (_gmailService == null)
                    return new List<GmailMessageInfo>();

                var request = _gmailService.Users.Messages.List(USER);
                request.LabelIds = new List<string> { labelId };
                request.MaxResults = 50; // Limit to 50 messages for performance

                var response = await request.ExecuteAsync();

                if (response.Messages == null)
                    return new List<GmailMessageInfo>();

                var messages = new List<GmailMessageInfo>();
                foreach (var messageRef in response.Messages)
                {
                    try
                    {
                        var msgRequest = _gmailService.Users.Messages.Get(USER, messageRef.Id);
                        msgRequest.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Metadata;
                        msgRequest.MetadataHeaders = new List<string> { "Subject", "From", "To", "Date" };

                        var message = await msgRequest.ExecuteAsync();

                        var msgInfo = new GmailMessageInfo
                        {
                            Id = message.Id,
                            ThreadId = message.ThreadId,
                            LabelIds = message.LabelIds?.ToList() ?? new List<string>(),
                            IsRead = !message.LabelIds?.Contains("UNREAD") ?? true,
                            SizeEstimate = message.SizeEstimate
                        };

                        // Extract headers
                        if (message.Payload?.Headers != null)
                        {
                            msgInfo.Subject = message.Payload.Headers.FirstOrDefault(h => h.Name == "Subject")?.Value;
                            msgInfo.From = message.Payload.Headers.FirstOrDefault(h => h.Name == "From")?.Value;
                            msgInfo.To = message.Payload.Headers.FirstOrDefault(h => h.Name == "To")?.Value;
                            msgInfo.Date = message.Payload.Headers.FirstOrDefault(h => h.Name == "Date")?.Value;
                        }

                        msgInfo.Snippet = message.Snippet;
                        messages.Add(msgInfo);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GmailPlugin] Error processing message {messageRef.Id}: {ex.Message}");
                    }
                }

                return messages;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GmailPlugin] GetMessagesInLabel error: {ex.Message}");
                return new List<GmailMessageInfo>();
            }
        }

        private string GetLabelIdFromPath(string path)
        {
            var normalizedPath = NormalizePath(path);

            if (normalizedPath == "/")
                return "INBOX"; // Default to inbox

            var pathPart = normalizedPath.Trim('/');

            // Check system labels first
            foreach (var kvp in _systemLabels)
            {
                if (kvp.Value.Equals(pathPart, StringComparison.OrdinalIgnoreCase))
                    return kvp.Key;
            }

            // For custom labels, we'll use the path part as-is
            // In a real implementation, you'd need to query the Gmail API to get the actual label ID
            return pathPart.ToUpper();
        }

        private (string labelId, string messageId) ParseFilePath(string path)
        {
            var normalizedPath = NormalizePath(path);
            var parts = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                var labelPath = "/" + string.Join("/", parts.Take(parts.Length - 1));
                var fileName = parts.Last();

                var labelId = GetLabelIdFromPath(labelPath);

                // Extract message ID from filename
                var match = Regex.Match(fileName, @"_(.+?)\.eml$");
                var messageId = match.Success ? match.Groups[1].Value : "";

                return (labelId, messageId);
            }

            return ("", "");
        }

        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "/";

            return path.Replace('\\', '/').TrimStart('/');
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove or replace invalid characters
            return Regex.Replace(fileName, "[<>:\"/\\\\|?*]", "_").Trim();
        }

        private string SanitizeDirectoryName(string dirName)
        {
            // Remove or replace invalid characters for directories
            return Regex.Replace(dirName, "[<>:\"/\\\\|?*]", "_").Trim();
        }

        public override void Dispose()
        {
            _gmailService?.Dispose();
            base.Dispose();
        }
    }
}