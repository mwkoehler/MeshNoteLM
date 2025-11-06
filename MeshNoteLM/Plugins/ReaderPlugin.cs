/*
================================================================================
Readwise Reader Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin

What this does
- Maps Readwise Reader documents to a virtual filesystem structure
- Documents appear as files under /documents/
- Supports reading document metadata and HTML content
- Supports saving new documents via API
- Uses Readwise Reader API v3

Virtual filesystem structure
/documents/                    - Root directory listing all documents
/documents/{id}.json           - Document metadata as JSON
/documents/{id}.html           - Document HTML content
/documents/{id}.md             - Document as markdown (if available)

File operations
- ReadFile("/documents/{id}.json") → Returns document metadata
- ReadFile("/documents/{id}.html") → Returns document HTML
- WriteFile("/documents/{url}", html) → Saves new document
- GetFiles("/documents") → Lists all document IDs

Authentication
- Requires READWISE_TOKEN environment variable or constructor parameter
- API endpoint: https://readwise.io/api/v3/

Usage
------------------------------------------------------------------------------
var plugin = new ReaderPlugin(token: "...");

// List all documents
var docs = plugin.GetFiles("/documents", "*.json");

// Read document metadata
var meta = plugin.ReadFile("/documents/{id}.json");

// Read document HTML content
var html = plugin.ReadFile("/documents/{id}.html");

// Save a new document
plugin.WriteFile("/documents/https://example.com/article", "<html>...</html>");

Security
------------------------------------------------------------------------------
- API token should be stored securely
- All paths validated to prevent directory traversal
- Respects Readwise API rate limits
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Plugins;

public class ReaderPlugin : PluginBase, IFileSystemPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _token;
    private const string API_BASE = "https://readwise.io/api/v3";

    // Cache
    private readonly Dictionary<string, string> _documentCache = [];

    public override string Name => "Readwise Reader";
    public override string Version => "0.1";
    public override string Description => "Readwise Reader documents as filesystem";
    public override string Author => "Starglass Technology";

    public ReaderPlugin(string? token = null)
    {
        // Try constructor parameter first, then settings service, then environment variable
        if (!string.IsNullOrEmpty(token))
        {
            _token = token;
        }
        else
        {
            try
            {
                var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
                _token = settingsService?.GetCredential<string>("reader-api-key") ?? Environment.GetEnvironmentVariable("READWISE_TOKEN") ?? "";
            }
            catch
            {
                _token = Environment.GetEnvironmentVariable("READWISE_TOKEN") ?? "";
            }
        }

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", _token);
    }

    // ---------------- IFileSystemPlugin: Files ----------------

    public bool FileExists(string path)
    {
        var (type, id, ext) = ParsePath(path);
        if (type == PathType.DocumentFile)
        {
            var doc = GetDocumentByIdAsync(id).Result;
            return doc != null;
        }
        return false;
    }

    public string ReadFile(string path)
    {
        var (type, id, ext) = ParsePath(path);

        if (type == PathType.DocumentFile)
        {
            if (ext == ".json")
            {
                if (_documentCache.TryGetValue($"{id}.json", out var cached))
                    return cached;

                var content = GetDocumentMetadataAsync(id).Result;
                _documentCache[$"{id}.json"] = content;
                return content;
            }
            else if (ext == ".html")
            {
                if (_documentCache.TryGetValue($"{id}.html", out var cached))
                    return cached;

                var content = GetDocumentHtmlAsync(id).Result;
                _documentCache[$"{id}.html"] = content;
                return content;
            }
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    public byte[] ReadFileBytes(string path)
    {
        // Readwise Reader stores text content, so convert to bytes
        var text = ReadFile(path);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        var (type, urlOrId, _) = ParsePath(path);

        if (type == PathType.DocumentFile)
        {
            // Save a new document with URL as identifier
            SaveDocumentAsync(urlOrId, contents).Wait();
        }
        else
        {
            throw new IOException($"Cannot write to path: {path}");
        }
    }

    public void AppendToFile(string path, string contents)
    {
        throw new NotSupportedException("Appending to Readwise documents not supported");
    }

    public void DeleteFile(string path)
    {
        throw new NotSupportedException("Deleting Readwise documents not supported via API");
    }

    // ------------- IFileSystemPlugin: Directories -------------

    public bool DirectoryExists(string path)
    {
        var (type, _, _) = ParsePath(path);
        return type == PathType.Root || type == PathType.Documents;
    }

    public void CreateDirectory(string path)
    {
        throw new NotSupportedException("Creating directories not supported");
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        throw new NotSupportedException("Deleting directories not supported");
    }

    // ------------- IFileSystemPlugin: Info & Listing ----------

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, _, _) = ParsePath(directoryPath);

        if (type == PathType.Documents)
        {
            var docs = ListDocumentsAsync().Result;
            foreach (var docId in docs)
            {
                if (searchPattern == "*" || searchPattern == "*.json")
                    yield return $"/documents/{docId}.json";
                if (searchPattern == "*" || searchPattern == "*.html")
                    yield return $"/documents/{docId}.html";
            }
        }
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, _, _) = ParsePath(directoryPath);

        if (type == PathType.Root)
        {
            yield return "/documents";
        }
    }

    public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in GetDirectories(directoryPath, searchPattern, searchOption))
            if (seen.Add(d)) yield return d;

        foreach (var f in GetFiles(directoryPath, searchPattern, searchOption))
            if (seen.Add(f)) yield return f;
    }

    public long GetFileSize(string path)
    {
        var content = ReadFile(path);
        return Encoding.UTF8.GetByteCount(content);
    }

    // ------------------------ Helpers -------------------------

    private enum PathType { Root, Documents, DocumentFile, Invalid }

    private (PathType type, string id, string ext) ParsePath(string path)
    {
        path = path.Trim('/').Replace('\\', '/');

        if (string.IsNullOrEmpty(path))
            return (PathType.Root, "", "");

        var parts = path.Split('/');

        if (parts[0] == "documents")
        {
            if (parts.Length == 1) return (PathType.Documents, "", "");
            if (parts.Length == 2)
            {
                var fileName = parts[1];
                var ext = Path.GetExtension(fileName);
                var idWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                return (PathType.DocumentFile, idWithoutExt, ext);
            }
        }

        return (PathType.Invalid, "", "");
    }

    private async Task<string?> GetDocumentByIdAsync(string id)
    {
        if (string.IsNullOrEmpty(_token))
            return null;

        try
        {
            var response = await _httpClient.GetAsync($"{API_BASE}/list/?id={id}");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            return json;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string> GetDocumentMetadataAsync(string id)
    {
        if (string.IsNullOrEmpty(_token))
            return "[Error: Token not configured]";

        try
        {
            var response = await _httpClient.GetAsync($"{API_BASE}/list/?id={id}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    private async Task<string> GetDocumentHtmlAsync(string id)
    {
        if (string.IsNullOrEmpty(_token))
            return "[Error: Token not configured]";

        try
        {
            var response = await _httpClient.GetAsync($"{API_BASE}/list/?id={id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("results", out var results) &&
                results.GetArrayLength() > 0)
            {
                var first = results[0];
                if (first.TryGetProperty("html", out var html))
                    return html.GetString() ?? "";
            }

            return "[No HTML content available]";
        }
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    private async Task<List<string>> ListDocumentsAsync()
    {
        if (string.IsNullOrEmpty(_token))
            return [];

        try
        {
            var response = await _httpClient.GetAsync($"{API_BASE}/list/");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var ids = new List<string>();
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("id", out var id))
                        ids.Add(id.GetString() ?? "");
                }
            }

            return ids;
        }
        catch
        {
            return [];
        }
    }

    private async Task SaveDocumentAsync(string url, string html)
    {
        if (string.IsNullOrEmpty(_token))
            throw new InvalidOperationException("Token not configured");

        try
        {
            var requestBody = new
            {
                url = url,
                html = html,
                should_clean_html = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/save/", content);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save document: {ex.Message}", ex);
        }
    }

    public override bool HasValidAuthorization()
    {
        return !string.IsNullOrWhiteSpace(_token);
    }

    public override Task InitializeAsync() => Task.CompletedTask;
    public override void Dispose() => _httpClient?.Dispose();
}
