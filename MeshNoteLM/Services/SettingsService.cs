using MeshNoteLM.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MeshNoteLM.Services
{
    public interface ISettingsService
    {
        string? ObsidianVaultPath { get; set; }

        // LLM Plugin API Keys
        string? ClaudeApiKey { get; set; }
        string? OpenAIApiKey { get; set; }
        string? GeminiApiKey { get; set; }
        string? GrokApiKey { get; set; }
        string? MetaApiKey { get; set; }
        string? MistralApiKey { get; set; }
        string? PerplexityApiKey { get; set; }

        // Other Service Credentials
        string? NotionApiKey { get; set; }
        string? GoogleDriveCredentialsPath { get; set; }
        string? GoogleClientId { get; set; }
        string? GoogleClientSecret { get; set; }
        string? RedditClientId { get; set; }
        string? RedditClientSecret { get; set; }
        string? RedditRefreshToken { get; set; }
        string? ReaderApiKey { get; set; }

        void Save();
        void Load();

        // Generic credential methods
        T? GetCredential<T>(string key, T? defaultValue = default);
        void SetCredential<T>(string key, T? value);
        bool HasCredential(string key);
        bool HasCredentials(params string[] keys);
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;
        private Settings _settings;

        public SettingsService(IFileSystemService fileSystem)
        {
            _settingsFilePath = Path.Combine(fileSystem.AppDataDirectory, "settings.json");
            _settings = new Settings();
            Load();
        }

        public string? ObsidianVaultPath
        {
            get => _settings.ObsidianVaultPath;
            set
            {
                _settings.ObsidianVaultPath = value;
                Save();
            }
        }

        // LLM Plugin API Keys
        public string? ClaudeApiKey
        {
            get => _settings.ClaudeApiKey;
            set { _settings.ClaudeApiKey = value; Save(); }
        }

        public string? OpenAIApiKey
        {
            get => _settings.OpenAIApiKey;
            set { _settings.OpenAIApiKey = value; Save(); }
        }

        public string? GeminiApiKey
        {
            get => _settings.GeminiApiKey;
            set { _settings.GeminiApiKey = value; Save(); }
        }

        public string? GrokApiKey
        {
            get => _settings.GrokApiKey;
            set { _settings.GrokApiKey = value; Save(); }
        }

        public string? MetaApiKey
        {
            get => _settings.MetaApiKey;
            set { _settings.MetaApiKey = value; Save(); }
        }

        public string? MistralApiKey
        {
            get => _settings.MistralApiKey;
            set { _settings.MistralApiKey = value; Save(); }
        }

        public string? PerplexityApiKey
        {
            get => _settings.PerplexityApiKey;
            set { _settings.PerplexityApiKey = value; Save(); }
        }

        // Other Service Credentials
        public string? NotionApiKey
        {
            get => _settings.NotionApiKey;
            set { _settings.NotionApiKey = value; Save(); }
        }

        public string? GoogleDriveCredentialsPath
        {
            get => _settings.GoogleDriveCredentialsPath;
            set { _settings.GoogleDriveCredentialsPath = value; Save(); }
        }

        public string? GoogleClientId
        {
            get => _settings.GoogleClientId;
            set { _settings.GoogleClientId = value; Save(); }
        }

        public string? GoogleClientSecret
        {
            get => _settings.GoogleClientSecret;
            set { _settings.GoogleClientSecret = value; Save(); }
        }

        public string? RedditClientId
        {
            get => _settings.RedditClientId;
            set { _settings.RedditClientId = value; Save(); }
        }

        public string? RedditClientSecret
        {
            get => _settings.RedditClientSecret;
            set { _settings.RedditClientSecret = value; Save(); }
        }

        public string? RedditRefreshToken
        {
            get => _settings.RedditRefreshToken;
            set { _settings.RedditRefreshToken = value; Save(); }
        }

        public string? ReaderApiKey
        {
            get => _settings.ReaderApiKey;
            set { _settings.ReaderApiKey = value; Save(); }
        }

        // Generic credential methods implementation
        public T? GetCredential<T>(string key, T? defaultValue = default)
        {
            try
            {
                if (_settings.GenericCredentials.TryGetValue(key, out var value) && value != null)
                {
                    // Handle JSON serialization for complex types
                    if (typeof(T) == typeof(string) && value is string stringValue)
                        return (T?)(object?)stringValue;

                    // For simple types, try direct conversion
                    if (value is T directValue)
                        return directValue;

                    // Try JSON serialization for complex objects
                    var json = JsonSerializer.Serialize(value);
                    return JsonSerializer.Deserialize<T>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Error getting credential '{key}': {ex.Message}");
            }

            return defaultValue;
        }

        public void SetCredential<T>(string key, T? value)
        {
            try
            {
                if (value == null)
                {
                    _settings.GenericCredentials.Remove(key);
                }
                else
                {
                    _settings.GenericCredentials[key] = value;
                }
                Save();

                System.Diagnostics.Debug.WriteLine($"[SettingsService] Set credential '{key}' of type {typeof(T).Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Error setting credential '{key}': {ex.Message}");
            }
        }

        public bool HasCredential(string key)
        {
            return _settings.GenericCredentials.ContainsKey(key) &&
                   _settings.GenericCredentials[key] != null;
        }

        public bool HasCredentials(params string[] keys)
        {
            return keys.All(HasCredential);
        }

        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Saved settings to: {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
            }
        }

        public void Load()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    _settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] Loaded settings from: {_settingsFilePath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] No settings file found, using defaults");
                    _settings = new Settings();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Error loading settings: {ex.Message}");
                _settings = new Settings();
            }
        }

        private class Settings
        {
            public string? ObsidianVaultPath { get; set; }

            // LLM Plugin API Keys
            public string? ClaudeApiKey { get; set; }
            public string? OpenAIApiKey { get; set; }
            public string? GeminiApiKey { get; set; }
            public string? GrokApiKey { get; set; }
            public string? MetaApiKey { get; set; }
            public string? MistralApiKey { get; set; }
            public string? PerplexityApiKey { get; set; }

            // Other Service Credentials
            public string? NotionApiKey { get; set; }
            public string? GoogleDriveCredentialsPath { get; set; }
            public string? GoogleClientId { get; set; }
            public string? GoogleClientSecret { get; set; }
            public string? RedditClientId { get; set; }
            public string? RedditClientSecret { get; set; }
            public string? RedditRefreshToken { get; set; }
            public string? ReaderApiKey { get; set; }

            // Generic credentials storage
            public Dictionary<string, object?> GenericCredentials { get; set; } = new();
        }
    }
}
