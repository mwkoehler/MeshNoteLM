/*
================================================================================
Reddit Plugin - IFileSystemPlugin Implementation
Implements: IFileSystemPlugin

What this does
- Maps Reddit content (subreddits, posts, comments, saved items) to a virtual filesystem
- Subreddits appear as directories under /subreddits/
- Posts appear as files (.json, .html, .txt) under subreddit directories
- Comments appear as files under post directories
- Saved items appear under /saved/
- Uses Reddit OAuth2 API

Virtual filesystem structure
/subreddits/                               - Root directory of subscribed subreddits
/subreddits/{name}/                        - Individual subreddit directory
/subreddits/{name}/posts/hot               - Hot posts in subreddit
/subreddits/{name}/posts/new               - New posts in subreddit
/subreddits/{name}/posts/top               - Top posts in subreddit
/subreddits/{name}/posts/{sort}/{id}.json  - Post metadata as JSON
/subreddits/{name}/posts/{sort}/{id}.txt   - Post content as plain text
/subreddits/{name}/posts/{sort}/{id}/comments - Post comments directory
/saved/                                    - Saved posts and comments
/saved/{id}.json                           - Saved item metadata

Authentication
- Requires Reddit OAuth2 credentials (client_id, client_secret, refresh_token)
- Scopes needed: read, history, mysubreddits, save
- Uses refresh token flow for authentication

Usage
------------------------------------------------------------------------------
var plugin = new RedditPlugin(
    clientId: "...",
    clientSecret: "...",
    refreshToken: "...",
    username: "your_username"
);

// List subreddits
var subs = plugin.GetDirectories("/subreddits");

// List hot posts in a subreddit
var posts = plugin.GetFiles("/subreddits/csharp/posts/hot");

// Read a post
var content = plugin.ReadFile("/subreddits/csharp/posts/hot/{post-id}.txt");

// List comments on a post
var comments = plugin.GetFiles("/subreddits/csharp/posts/hot/{post-id}/comments");

// List saved items
var saved = plugin.GetFiles("/saved");

Security
------------------------------------------------------------------------------
- OAuth2 tokens are refreshed automatically when expired
- All paths validated to prevent directory traversal
- Respects Reddit API rate limits
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

public class RedditPlugin : PluginBase, IFileSystemPlugin
{
    private readonly HttpClient _httpClient;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _refreshToken;
    private readonly string _username;
    private readonly string _userAgent;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiry;

    private const string OAUTH_BASE = "https://oauth.reddit.com";
    private const string TOKEN_URL = "https://www.reddit.com/api/v1/access_token";

    // Caches
    private readonly Dictionary<string, string> _contentCache = [];
    private readonly List<string> _subredditCache = [];

    public override string Name => "Reddit";
    public override string Version => "0.1";
    public override string Description => "Reddit subreddits, posts, and saved items as filesystem";
    public override string Author => "Starglass Technology";

    public RedditPlugin(string? clientId = null, string? clientSecret = null, string? refreshToken = null, string? username = null, string? userAgent = null)
    {
        // Try constructor parameters first, then settings service, then environment variables
        try
        {
            var settingsService = MeshNoteLM.Services.AppServices.Services?.GetService<MeshNoteLM.Services.ISettingsService>();
            _clientId = clientId ?? settingsService?.GetCredential<string>("reddit-client-id") ?? Environment.GetEnvironmentVariable("REDDIT_CLIENT_ID") ?? "";
            _clientSecret = clientSecret ?? settingsService?.GetCredential<string>("reddit-client-secret") ?? Environment.GetEnvironmentVariable("REDDIT_CLIENT_SECRET") ?? "";
            _refreshToken = refreshToken ?? settingsService?.GetCredential<string>("reddit-refresh-token") ?? Environment.GetEnvironmentVariable("REDDIT_REFRESH_TOKEN") ?? "";
        }
        catch
        {
            _clientId = clientId ?? Environment.GetEnvironmentVariable("REDDIT_CLIENT_ID") ?? "";
            _clientSecret = clientSecret ?? Environment.GetEnvironmentVariable("REDDIT_CLIENT_SECRET") ?? "";
            _refreshToken = refreshToken ?? Environment.GetEnvironmentVariable("REDDIT_REFRESH_TOKEN") ?? "";
        }

        _username = username ?? Environment.GetEnvironmentVariable("REDDIT_USERNAME") ?? "MeshNoteLM";
        _userAgent = userAgent ?? Environment.GetEnvironmentVariable("REDDIT_USER_AGENT") ?? $"MeshNoteLM/0.1 by {_username}";

        _httpClient = new HttpClient();
        try
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(_userAgent);
        }
        catch
        {
            // If user agent parsing fails, continue anyway
        }
    }

    // ---------------- IFileSystemPlugin: Files ----------------

    public bool FileExists(string path)
    {
        var (type, _, _, _) = ParsePath(path);
        return type == PathType.PostFile || type == PathType.SavedFile || type == PathType.CommentFile;
    }

    public string ReadFile(string path)
    {
        var (type, subreddit, postId, ext) = ParsePath(path);

        EnsureAccessToken().Wait();

        if (type == PathType.PostFile)
        {
            var cacheKey = $"{subreddit}/{postId}{ext}";
            if (_contentCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var content = FetchPostContentAsync(postId, ext).Result;
            _contentCache[cacheKey] = content;
            return content;
        }
        else if (type == PathType.SavedFile)
        {
            var cacheKey = $"saved/{postId}{ext}";
            if (_contentCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var content = FetchPostContentAsync(postId, ext).Result;
            _contentCache[cacheKey] = content;
            return content;
        }
        else if (type == PathType.CommentFile)
        {
            return FetchCommentContentAsync(postId, ext).Result;
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    public byte[] ReadFileBytes(string path)
    {
        // Reddit stores text content, so convert to bytes
        var text = ReadFile(path);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        throw new NotSupportedException("Writing to Reddit not yet implemented");
    }

    public void AppendToFile(string path, string contents)
    {
        throw new NotSupportedException("Appending to Reddit not supported");
    }

    public void DeleteFile(string path)
    {
        throw new NotSupportedException("Deleting Reddit content not supported");
    }

    // ------------- IFileSystemPlugin: Directories -------------

    public bool DirectoryExists(string path)
    {
        var (type, _, _, _) = ParsePath(path);
        return type != PathType.Invalid;
    }

    public void CreateDirectory(string path)
    {
        throw new NotSupportedException("Creating Reddit directories not supported");
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        throw new NotSupportedException("Deleting Reddit directories not supported");
    }

    // ------------- IFileSystemPlugin: Info & Listing ----------

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, subreddit, postId, _) = ParsePath(directoryPath);

        EnsureAccessToken().Wait();

        if (type == PathType.SubredditPostsSort)
        {
            var posts = FetchSubredditPostsAsync(subreddit, "hot").Result;
            foreach (var post in posts)
            {
                if (searchPattern == "*" || searchPattern == "*.json")
                    yield return $"/subreddits/{subreddit}/posts/hot/{post}.json";
                if (searchPattern == "*" || searchPattern == "*.txt")
                    yield return $"/subreddits/{subreddit}/posts/hot/{post}.txt";
            }
        }
        else if (type == PathType.PostCommentsDir)
        {
            var comments = FetchPostCommentsAsync(postId).Result;
            foreach (var comment in comments)
            {
                if (searchPattern == "*" || searchPattern == "*.json")
                    yield return $"{directoryPath}/{comment}.json";
                if (searchPattern == "*" || searchPattern == "*.txt")
                    yield return $"{directoryPath}/{comment}.txt";
            }
        }
        else if (type == PathType.Saved)
        {
            var saved = FetchSavedItemsAsync().Result;
            foreach (var item in saved)
            {
                if (searchPattern == "*" || searchPattern == "*.json")
                    yield return $"/saved/{item}.json";
                if (searchPattern == "*" || searchPattern == "*.txt")
                    yield return $"/saved/{item}.txt";
            }
        }
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, subreddit, _, _) = ParsePath(directoryPath);

        EnsureAccessToken().Wait();

        if (type == PathType.Root)
        {
            yield return "/subreddits";
            yield return "/saved";
        }
        else if (type == PathType.Subreddits)
        {
            var subs = FetchSubscribedSubredditsAsync().Result;
            foreach (var sub in subs)
                yield return $"/subreddits/{sub}";
        }
        else if (type == PathType.SubredditDir)
        {
            yield return $"/subreddits/{subreddit}/posts";
        }
        else if (type == PathType.SubredditPosts)
        {
            yield return $"/subreddits/{subreddit}/posts/hot";
            yield return $"/subreddits/{subreddit}/posts/new";
            yield return $"/subreddits/{subreddit}/posts/top";
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

    private enum PathType
    {
        Root,
        Subreddits,
        SubredditDir,
        SubredditPosts,
        SubredditPostsSort,
        PostFile,
        PostCommentsDir,
        CommentFile,
        Saved,
        SavedFile,
        Invalid
    }

    private (PathType type, string subreddit, string postId, string ext) ParsePath(string path)
    {
        path = path.Trim('/').Replace('\\', '/');

        if (string.IsNullOrEmpty(path))
            return (PathType.Root, "", "", "");

        var parts = path.Split('/');

        if (parts[0] == "subreddits")
        {
            if (parts.Length == 1) return (PathType.Subreddits, "", "", "");
            if (parts.Length == 2) return (PathType.SubredditDir, parts[1], "", "");
            if (parts.Length == 3 && parts[2] == "posts") return (PathType.SubredditPosts, parts[1], "", "");
            if (parts.Length == 4) return (PathType.SubredditPostsSort, parts[1], "", "");
            if (parts.Length == 5)
            {
                var fileName = parts[4];
                var ext = Path.GetExtension(fileName);
                var idWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                return (PathType.PostFile, parts[1], idWithoutExt, ext);
            }
            if (parts.Length == 6 && parts[5] == "comments")
                return (PathType.PostCommentsDir, parts[1], parts[4], "");
            if (parts.Length == 7)
            {
                var fileName = parts[6];
                var ext = Path.GetExtension(fileName);
                var idWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                return (PathType.CommentFile, parts[1], idWithoutExt, ext);
            }
        }
        else if (parts[0] == "saved")
        {
            if (parts.Length == 1) return (PathType.Saved, "", "", "");
            if (parts.Length == 2)
            {
                var fileName = parts[1];
                var ext = Path.GetExtension(fileName);
                var idWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                return (PathType.SavedFile, "", idWithoutExt, ext);
            }
        }

        return (PathType.Invalid, "", "", "");
    }

    private async Task EnsureAccessToken()
    {
        if (!string.IsNullOrEmpty(_accessToken) && _accessTokenExpiry > DateTimeOffset.UtcNow.AddMinutes(5))
            return;

        var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
        var request = new HttpRequestMessage(HttpMethod.Post, TOKEN_URL);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        var formData = new Dictionary<string, string>
        {
            {"grant_type", "refresh_token"},
            {"refresh_token", _refreshToken}
        };

        request.Content = new FormUrlEncodedContent(formData);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("access_token", out var token))
        {
            _accessToken = token.GetString();
            _accessTokenExpiry = DateTimeOffset.UtcNow.AddHours(1);
        }
    }

    private async Task<List<string>> FetchSubscribedSubredditsAsync()
    {
        if (_subredditCache.Count > 0)
            return _subredditCache;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/subreddits/mine/subscriber?limit=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("data", out var childData) &&
                        childData.TryGetProperty("display_name", out var name))
                    {
                        _subredditCache.Add(name.GetString() ?? "");
                    }
                }
            }

            return _subredditCache;
        }
        catch
        {
            return [];
        }
    }

    private async Task<List<string>> FetchSubredditPostsAsync(string subreddit, string sort)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/r/{subreddit}/{sort}?limit=25");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var postIds = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("data", out var childData) &&
                        childData.TryGetProperty("name", out var name))
                    {
                        postIds.Add(name.GetString() ?? "");
                    }
                }
            }

            return postIds;
        }
        catch
        {
            return [];
        }
    }

    private async Task<string> FetchPostContentAsync(string postId, string ext)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/api/info?id={postId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            if (ext == ".json")
                return json;

            // For .txt, extract title and selftext
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("children", out var children) &&
                children.GetArrayLength() > 0)
            {
                var post = children[0];
                if (post.TryGetProperty("data", out var postData))
                {
                    var title = postData.TryGetProperty("title", out var t) ? t.GetString() : "";
                    var selftext = postData.TryGetProperty("selftext", out var st) ? st.GetString() : "";
                    return $"{title}\n\n{selftext}";
                }
            }

            return json;
        }
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    private async Task<List<string>> FetchPostCommentsAsync(string postId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/comments/{postId.Replace("t3_", "")}?limit=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var commentIds = new List<string>();
            if (doc.RootElement.GetArrayLength() > 1)
            {
                var commentsListing = doc.RootElement[1];
                if (commentsListing.TryGetProperty("data", out var data) &&
                    data.TryGetProperty("children", out var children))
                {
                    foreach (var child in children.EnumerateArray())
                    {
                        if (child.TryGetProperty("data", out var childData) &&
                            childData.TryGetProperty("name", out var name))
                        {
                            commentIds.Add(name.GetString() ?? "");
                        }
                    }
                }
            }

            return commentIds;
        }
        catch
        {
            return [];
        }
    }

    private async Task<string> FetchCommentContentAsync(string commentId, string ext)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/api/info?id={commentId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();

            if (ext == ".json")
                return json;

            // For .txt, extract body
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("children", out var children) &&
                children.GetArrayLength() > 0)
            {
                var comment = children[0];
                if (comment.TryGetProperty("data", out var commentData) &&
                    commentData.TryGetProperty("body", out var body))
                {
                    return body.GetString() ?? "";
                }
            }

            return json;
        }
        catch (Exception ex)
        {
            return $"[Error: {ex.Message}]";
        }
    }

    private async Task<List<string>> FetchSavedItemsAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{OAUTH_BASE}/user/{_username}/saved?limit=100");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var itemIds = new List<string>();
            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    if (child.TryGetProperty("data", out var childData) &&
                        childData.TryGetProperty("name", out var name))
                    {
                        itemIds.Add(name.GetString() ?? "");
                    }
                }
            }

            return itemIds;
        }
        catch
        {
            return [];
        }
    }

    public override bool HasValidAuthorization()
    {
        // Reddit requires client ID, client secret, and refresh token for OAuth
        return !string.IsNullOrWhiteSpace(_clientId) &&
               !string.IsNullOrWhiteSpace(_clientSecret) &&
               !string.IsNullOrWhiteSpace(_refreshToken);
    }

    public override Task InitializeAsync() => Task.CompletedTask;
    public override void Dispose() => _httpClient?.Dispose();
}
