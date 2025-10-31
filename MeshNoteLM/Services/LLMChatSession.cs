/*
================================================================================
LLM Chat Session Service
Manages conversation state, file context, and markdown persistence
================================================================================
*/

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshNoteLM.Models;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Services;

public class LLMChatSession
{
    private string? _currentFilePath;
    private string? _currentFileContent;
    private string? _chatFilePath;
    private bool _isDirectChatFile = false; // Flag to track if we're using direct chat file

    /// <summary>
    /// All messages in the current conversation
    /// </summary>
    public ObservableCollection<LLMChatMessage> Messages { get; } = new();

    /// <summary>
    /// The currently selected file path (for context)
    /// </summary>
    public string? CurrentFilePath
    {
        get => _currentFilePath;
        set
        {
            if (_currentFilePath != value)
            {
                SaveCurrentSession();
                _currentFilePath = value;
                _chatFilePath = GetChatFilePath(value);
                _isDirectChatFile = false; // Reset flag when context file is set
                LoadSessionFromFile();
            }
        }
    }

    /// <summary>
    /// The content of the currently selected file
    /// </summary>
    public string? CurrentFileContent
    {
        get => _currentFileContent;
        set => _currentFileContent = value;
    }

    /// <summary>
    /// Check if the chat session is properly configured (has either context file or direct chat file)
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(_currentFilePath) || _isDirectChatFile;

    /// <summary>
    /// Add a user message to the conversation
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = new LLMChatMessage
        {
            Role = "user",
            Content = content,
            Timestamp = DateTime.Now
        };

        Messages.Add(message);
        SaveCurrentSession();
    }

    /// <summary>
    /// Add an assistant response to the conversation
    /// </summary>
    public void AddAssistantMessage(string content, string llmProvider)
    {
        var message = new LLMChatMessage
        {
            Role = "assistant",
            Content = content,
            LLMProvider = llmProvider,
            Timestamp = DateTime.Now
        };

        Messages.Add(message);
        SaveCurrentSession();
    }

    /// <summary>
    /// Add an error message to the conversation
    /// </summary>
    public void AddErrorMessage(string errorMessage, string llmProvider)
    {
        var message = new LLMChatMessage
        {
            Role = "assistant",
            LLMProvider = llmProvider,
            ErrorMessage = errorMessage,
            Timestamp = DateTime.Now
        };

        Messages.Add(message);
        SaveCurrentSession();
    }

    /// <summary>
    /// Get the full conversation context for sending to an LLM
    /// Includes file content and all previous messages
    /// </summary>
    public string GetConversationContext()
    {
        var context = new StringBuilder();

        // Add file context if available
        if (!string.IsNullOrEmpty(CurrentFilePath))
        {
            context.AppendLine($"# Context: File {CurrentFilePath}");
            context.AppendLine();

            if (!string.IsNullOrEmpty(CurrentFileContent))
            {
                context.AppendLine("## File Content:");
                context.AppendLine("```");
                context.AppendLine(CurrentFileContent);
                context.AppendLine("```");
                context.AppendLine();
            }
        }

        // Add conversation history
        if (Messages.Count > 0)
        {
            context.AppendLine("## Previous Conversation:");
            foreach (var msg in Messages.Where(m => !m.IsError))
            {
                if (msg.Role == "user")
                {
                    context.AppendLine($"User: {msg.Content}");
                }
                else
                {
                    context.AppendLine($"{msg.LLMProvider}: {msg.Content}");
                }
                context.AppendLine();
            }
        }

        return context.ToString();
    }

    /// <summary>
    /// Get conversation history formatted for AIProviderPluginBase
    /// </summary>
    public List<(string Role, string Content)> GetMessageHistory()
    {
        return Messages
            .Where(m => !m.IsError)
            .Select(m => (m.Role, m.Content))
            .ToList();
    }

    /// <summary>
    /// Save the current session to the .chat.md file
    /// </summary>
    public void SaveCurrentSession()
    {
        if (string.IsNullOrEmpty(_chatFilePath))
            return;

        try
        {
            var markdown = new StringBuilder();
            markdown.AppendLine($"# Chat for {Path.GetFileName(_currentFilePath)}");
            markdown.AppendLine();

            foreach (var message in Messages)
            {
                markdown.AppendLine(message.ToMarkdown());
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_chatFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_chatFilePath, markdown.ToString());
            System.Diagnostics.Debug.WriteLine($"[LLMChatSession] Saved chat to: {_chatFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatSession] Error saving chat: {ex.Message}");
        }
    }

    /// <summary>
    /// Load an existing session from the .chat.md file
    /// </summary>
    private void LoadSessionFromFile()
    {
        Messages.Clear();

        if (string.IsNullOrEmpty(_chatFilePath) || !File.Exists(_chatFilePath))
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatSession] No existing chat file found: {_chatFilePath}");
            return;
        }

        try
        {
            var content = File.ReadAllText(_chatFilePath);
            var blocks = content.Split(new[] { "## " }, StringSplitOptions.RemoveEmptyEntries);

            // Skip the first block (title)
            foreach (var block in blocks.Skip(1))
            {
                var message = LLMChatMessage.FromMarkdown("## " + block);
                if (message != null)
                {
                    Messages.Add(message);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatSession] Loaded {Messages.Count} messages from: {_chatFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatSession] Error loading chat: {ex.Message}");
        }
    }

    /// <summary>
    /// Get the chat file path for a given file
    /// </summary>
    private static string? GetChatFilePath(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        return filePath + ".chat.md";
    }

    /// <summary>
    /// Set the chat file path directly (for chats without context files)
    /// </summary>
    public void SetChatFilePath(string chatFilePath)
    {
        System.Diagnostics.Debug.WriteLine($"[LLMChatSession] SetChatFilePath called with: '{chatFilePath}'");
        SaveCurrentSession();
        _currentFilePath = null; // No context file
        _currentFileContent = null; // No context file content
        _chatFilePath = chatFilePath; // Direct chat file path
        _isDirectChatFile = true; // Mark as direct chat file
        System.Diagnostics.Debug.WriteLine($"[LLMChatSession] SetChatFilePath - _chatFilePath set to: '{_chatFilePath}'");
        System.Diagnostics.Debug.WriteLine($"[LLMChatSession] SetChatFilePath - _isDirectChatFile set to: {_isDirectChatFile}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatSession] SetChatFilePath - IsConfigured: {IsConfigured}");
        LoadSessionFromFile();
        System.Diagnostics.Debug.WriteLine($"[LLMChatSession] SetChatFilePath - After LoadSessionFromFile - IsConfigured: {IsConfigured}");
    }

    /// <summary>
    /// Clear the current conversation (start fresh)
    /// </summary>
    public void ClearConversation()
    {
        Messages.Clear();
        if (!string.IsNullOrEmpty(_chatFilePath) && File.Exists(_chatFilePath))
        {
            try
            {
                File.Delete(_chatFilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatSession] Error deleting chat file: {ex.Message}");
            }
        }
    }
}
