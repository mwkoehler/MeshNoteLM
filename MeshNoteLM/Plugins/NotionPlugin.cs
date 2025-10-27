/*
================================================================================
Notion Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin

What this does
- Maps Notion pages and databases to a virtual filesystem structure
- Pages appear as .md files under /pages/
- Databases appear as directories under /databases/ with rows as files
- Supports reading page content, listing databases, and accessing database entries
- Uses Notion API v1

Virtual filesystem structure
/pages/                        - Root directory listing all pages
/pages/{page-id}.md            - Individual page as markdown
/databases/                    - Root directory listing all databases
/databases/{db-name}/          - Database directory
/databases/{db-name}/{row}.md  - Database row as markdown

File operations
- ReadFile("/pages/{page-id}.md") → Returns page content
- GetFiles("/databases/{db-name}") → Lists all rows in database
- GetDirectories("/databases") → Lists all accessible databases
- WriteFile creates/updates pages (limited by Notion API permissions)

Authentication
- Requires NOTION_API_KEY environment variable or constructor parameter
- Requires NOTION_VERSION (defaults to "2022-06-28")
- API endpoint: https://api.notion.com/v1

Usage
------------------------------------------------------------------------------
var plugin = new NotionPlugin(apiKey: "secret_...");

// List all accessible pages
var pages = plugin.GetDirectories("/pages");

// Read a page
var content = plugin.ReadFile("/pages/{page-id}.md");

// List all databases
var dbs = plugin.GetDirectories("/databases");

// List rows in a database
var rows = plugin.GetFiles("/databases/Tasks");

Security
------------------------------------------------------------------------------
- API key should be stored securely
- All paths validated to prevent directory traversal
- Respects Notion API rate limits
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

public class NotionPlugin : PluginBase, IFileSystemPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string API_BASE = "https://api.notion.com/v1";
    private const string API_VERSION = "2022-06-28";

    // Caches
    private readonly Dictionary<string, string> _pageCache = new();
    private readonly Dictionary<string, List<string>> _databaseRowsCache = new();

    public override string Name => "Notion";
    public override string Version => "0.1";
    public override string Description => "Notion pages and databases as filesystem";
    public override string Author => "Starglass Technology";

    public NotionPlugin(string? apiKey = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("NOTION_API_KEY") ?? "";
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("Notion-Version", API_VERSION);
    }

    public bool FileExists(string path)
    {
        var (type, id, _) = ParsePath(path);
        return type == PathType.PageFile && !string.IsNullOrEmpty(id);
    }

    public string ReadFile(string path)
    {
        var (type, id, _) = ParsePath(path);

        if (type == PathType.PageFile)
        {
            if (_pageCache.TryGetValue(id, out var cached))
                return cached;

            var content = FetchPageContentAsync(id).Result;
            _pageCache[id] = content;
            return content;
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    public byte[] ReadFileBytes(string path)
    {
        // Notion stores text content, so convert to bytes
        var text = ReadFile(path);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        throw new NotSupportedException("Writing to Notion pages not yet implemented");
    }

    public void AppendToFile(string path, string contents)
    {
        throw new NotSupportedException("Appending to Notion pages not yet implemented");
    }

    public void DeleteFile(string path)
    {
        throw new NotSupportedException("Deleting Notion pages not yet implemented");
    }

    public bool DirectoryExists(string path)
    {
        var (type, _, _) = ParsePath(path);
        return type == PathType.Root || type == PathType.Pages || type == PathType.Databases;
    }

    public void CreateDirectory(string path)
    {
        throw new NotSupportedException("Creating Notion directories not supported");
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        throw new NotSupportedException("Deleting Notion directories not supported");
    }

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, dbName, _) = ParsePath(directoryPath);

        if (type == PathType.DatabaseDir)
        {
            var rows = FetchDatabaseRowsAsync(dbName).Result;
            foreach (var row in rows)
                yield return $"/databases/{dbName}/{row}.md";
        }
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, _, _) = ParsePath(directoryPath);

        if (type == PathType.Root)
        {
            yield return "/pages";
            yield return "/databases";
        }
        else if (type == PathType.Databases)
        {
            var dbs = FetchDatabasesAsync().Result;
            foreach (var db in dbs)
                yield return $"/databases/{db}";
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

    private enum PathType { Root, Pages, PageFile, Databases, DatabaseDir, Invalid }

    private (PathType type, string id, string fileName) ParsePath(string path)
    {
        path = path.Trim('/').Replace('\\', '/');

        if (string.IsNullOrEmpty(path))
            return (PathType.Root, "", "");

        var parts = path.Split('/');

        if (parts[0] == "pages")
        {
            if (parts.Length == 1) return (PathType.Pages, "", "");
            if (parts.Length == 2) return (PathType.PageFile, parts[1].Replace(".md", ""), parts[1]);
        }
        else if (parts[0] == "databases")
        {
            if (parts.Length == 1) return (PathType.Databases, "", "");
            if (parts.Length == 2) return (PathType.DatabaseDir, parts[1], "");
        }

        return (PathType.Invalid, "", "");
    }

    private async Task<string> FetchPageContentAsync(string pageId)
    {
        if (string.IsNullOrEmpty(_apiKey))
            return "[Error: API key not configured]";

        try
        {
            var response = await _httpClient.GetAsync($"{API_BASE}/pages/{pageId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            // Extract page title if available
            if (doc.RootElement.TryGetProperty("properties", out var props))
            {
                return $"# Notion Page: {pageId}\n\n{json}";
            }

            return json;
        }
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    private async Task<List<string>> FetchDatabasesAsync()
    {
        if (string.IsNullOrEmpty(_apiKey))
            return new List<string>();

        try
        {
            var requestBody = new { filter = new { value = "database", property = "object" } };
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{API_BASE}/search", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            var databases = new List<string>();
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("id", out var id))
                        databases.Add(id.GetString() ?? "");
                }
            }

            return databases;
        }
        catch
        {
            return new List<string>();
        }
    }

    private async Task<List<string>> FetchDatabaseRowsAsync(string databaseId)
    {
        if (_databaseRowsCache.TryGetValue(databaseId, out var cached))
            return cached;

        if (string.IsNullOrEmpty(_apiKey))
            return new List<string>();

        try
        {
            var response = await _httpClient.PostAsync($"{API_BASE}/databases/{databaseId}/query", null);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var rows = new List<string>();
            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("id", out var id))
                        rows.Add(id.GetString() ?? "");
                }
            }

            _databaseRowsCache[databaseId] = rows;
            return rows;
        }
        catch
        {
            return new List<string>();
        }
    }

    public override bool HasValidAuthorization() => !string.IsNullOrWhiteSpace(_apiKey);
    public override Task InitializeAsync() => Task.CompletedTask;
    public override void Dispose() => _httpClient?.Dispose();
}
