using MeshNoteLM.Interfaces;
using System;
using System.IO;
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
        string? RedditClientId { get; set; }
        string? RedditClientSecret { get; set; }
        string? RedditRefreshToken { get; set; }
        string? ReaderApiKey { get; set; }

        void Save();
        void Load();
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
            public string? RedditClientId { get; set; }
            public string? RedditClientSecret { get; set; }
            public string? RedditRefreshToken { get; set; }
            public string? ReaderApiKey { get; set; }
        }
    }
}
