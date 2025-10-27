/*
================================================================================
AI Provider Plugin Base Class
Abstract base class for AI/LLM provider plugins that map conversations to
a virtual filesystem structure.

Common Features:
- Conversations as directories under /conversations/
- Messages as numbered .txt files (001.txt, 002.txt, etc.)
- In-memory conversation storage with Message history
- Path parsing and validation
- Model listing under /models/

Derived classes only need to implement:
- API-specific message sending logic
- Available model listing
- API key retrieval from settings

This eliminates ~400 lines of duplicated code per AI provider plugin.
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Plugins;

public abstract class AIProviderPluginBase : PluginBase, IFileSystemPlugin
{
    protected readonly HttpClient _httpClient;
    protected readonly string _apiKey;
    protected readonly Dictionary<string, List<Message>> _conversations = new();

    protected class Message
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    protected enum PathType
    {
        Root,
        Conversations,
        ConversationDir,
        ConversationMessage,
        Models,
        Invalid
    }

    protected AIProviderPluginBase(string? apiKey, HttpClient? httpClient = null)
    {
        _apiKey = ResolveApiKey(apiKey);
        _httpClient = httpClient ?? new HttpClient();
        ConfigureHttpClient();
    }

    // Abstract methods for derived classes to implement
    protected abstract string? GetApiKeyFromSettings();
    protected abstract string? GetApiKeyFromEnvironment();
    protected abstract Task<string> SendMessageToProviderAsync(List<Message> conversationHistory, string userMessage);
    protected abstract IEnumerable<string> GetAvailableModels();
    protected abstract void ConfigureHttpClient();

    /// <summary>
    /// Public method to send a message directly to the LLM with conversation history
    /// </summary>
    public async Task<string> SendChatMessageAsync(List<(string Role, string Content)> history, string userMessage)
    {
        var messages = history.Select(h => new Message { Role = h.Role, Content = h.Content }).ToList();
        return await SendMessageToProviderAsync(messages, userMessage);
    }

    private string ResolveApiKey(string? providedKey)
    {
        if (!string.IsNullOrWhiteSpace(providedKey))
            return providedKey;

        try
        {
            var settingsKey = GetApiKeyFromSettings();
            if (!string.IsNullOrWhiteSpace(settingsKey))
                return settingsKey;
        }
        catch { /* Settings service not available yet */ }

        return GetApiKeyFromEnvironment() ?? "";
    }

    // ============ IFileSystemPlugin: Files ============

    public bool FileExists(string path)
    {
        var (type, convId, fileName) = ParsePath(path);
        if (type == PathType.ConversationMessage && _conversations.ContainsKey(convId))
        {
            var idx = GetMessageIndex(fileName);
            return idx >= 0 && idx < _conversations[convId].Count;
        }
        return false;
    }

    public string ReadFile(string path)
    {
        var (type, convId, fileName) = ParsePath(path);

        if (type == PathType.ConversationMessage && _conversations.TryGetValue(convId, out var messages))
        {
            var idx = GetMessageIndex(fileName);
            if (idx >= 0 && idx < messages.Count)
            {
                var msg = messages[idx];
                return $"[{msg.Timestamp:u}] {msg.Role}: {msg.Content}";
            }
        }

        throw new FileNotFoundException($"File not found: {path}");
    }

    public byte[] ReadFileBytes(string path)
    {
        // For AI providers, we only store text messages, so convert to bytes
        var text = ReadFile(path);
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        var (type, convId, fileName) = ParsePath(path);

        if (type == PathType.ConversationMessage)
        {
            // Create new conversation if "new" is specified
            if (convId == "new")
            {
                convId = Guid.NewGuid().ToString("N");
                _conversations[convId] = new List<Message>();
            }

            if (!_conversations.ContainsKey(convId))
            {
                _conversations[convId] = new List<Message>();
            }

            // Send to provider API and get response
            var history = _conversations[convId];
            var response = SendMessageToProviderAsync(history, contents).Result;

            // Add user message
            _conversations[convId].Add(new Message
            {
                Role = "user",
                Content = contents
            });

            // Add assistant response
            _conversations[convId].Add(new Message
            {
                Role = "assistant",
                Content = response
            });
        }
        else
        {
            throw new IOException($"Cannot write to path: {path}");
        }
    }

    public void AppendToFile(string path, string contents)
    {
        WriteFile(path, contents, overwrite: true);
    }

    public void DeleteFile(string path)
    {
        var (type, convId, fileName) = ParsePath(path);

        if (type == PathType.ConversationMessage && _conversations.ContainsKey(convId))
        {
            var idx = GetMessageIndex(fileName);
            if (idx >= 0 && idx < _conversations[convId].Count)
            {
                _conversations[convId].RemoveAt(idx);
            }
        }
    }

    // ============ IFileSystemPlugin: Directories ============

    public bool DirectoryExists(string path)
    {
        var (type, convId, _) = ParsePath(path);

        return type switch
        {
            PathType.Root => true,
            PathType.Conversations => true,
            PathType.ConversationDir => _conversations.ContainsKey(convId),
            PathType.Models => true,
            _ => false
        };
    }

    public void CreateDirectory(string path)
    {
        var (type, convId, _) = ParsePath(path);

        if (type == PathType.ConversationDir && !_conversations.ContainsKey(convId))
        {
            _conversations[convId] = new List<Message>();
        }
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        var (type, convId, _) = ParsePath(path);

        if (type == PathType.ConversationDir)
        {
            _conversations.Remove(convId);
        }
    }

    // ============ IFileSystemPlugin: Info & Listing ============

    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, convId, _) = ParsePath(directoryPath);

        if (type == PathType.ConversationDir && _conversations.TryGetValue(convId, out var messages))
        {
            for (int i = 0; i < messages.Count; i++)
            {
                yield return $"conversations/{convId}/{(i + 1):D3}.txt";
            }
        }
        else if (type == PathType.Models)
        {
            foreach (var model in GetAvailableModels())
            {
                yield return $"models/{model}.txt";
            }
        }
    }

    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var (type, _, _) = ParsePath(directoryPath);

        if (type == PathType.Root)
        {
            yield return "conversations";
            yield return "models";
        }
        else if (type == PathType.Conversations)
        {
            foreach (var convId in _conversations.Keys)
            {
                yield return $"conversations/{convId}";
            }
        }
    }

    public IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var d in GetDirectories(directoryPath, searchPattern, searchOption))
        {
            if (seen.Add(d)) yield return d;
        }

        foreach (var f in GetFiles(directoryPath, searchPattern, searchOption))
        {
            if (seen.Add(f)) yield return f;
        }
    }

    public long GetFileSize(string path)
    {
        var content = ReadFile(path);
        return Encoding.UTF8.GetByteCount(content);
    }

    // ============ Helper Methods ============

    protected (PathType type, string convId, string fileName) ParsePath(string path)
    {
        path = path.Trim('/').Replace('\\', '/');

        if (string.IsNullOrEmpty(path))
            return (PathType.Root, "", "");

        var parts = path.Split('/');

        if (parts[0] == "conversations")
        {
            if (parts.Length == 1) return (PathType.Conversations, "", "");
            if (parts.Length == 2) return (PathType.ConversationDir, parts[1], "");
            if (parts.Length == 3) return (PathType.ConversationMessage, parts[1], parts[2]);
        }
        else if (parts[0] == "models")
        {
            return (PathType.Models, "", "");
        }

        return (PathType.Invalid, "", "");
    }

    protected int GetMessageIndex(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (int.TryParse(name, out var idx))
            return idx - 1; // Convert from 1-based to 0-based
        return -1;
    }

    // ============ IPlugin Implementation ============

    public override bool HasValidAuthorization()
    {
        return !string.IsNullOrWhiteSpace(_apiKey);
    }

    public override Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
    }
}
