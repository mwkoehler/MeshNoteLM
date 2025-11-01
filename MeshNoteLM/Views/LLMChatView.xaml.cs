/*
================================================================================
LLM Chat View - Code Behind
UI for LLM conversations with plugin selection and error handling
================================================================================
*/

#nullable enable
namespace MeshNoteLM.Views;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MeshNoteLM.Services;
using MeshNoteLM.Models;
using MeshNoteLM.Plugins;


public partial class LLMChatView : ContentView
{
    private readonly LLMChatSession _chatSession;
    private readonly PluginManager _pluginManager;
    private readonly List<AIProviderPluginBase> _selectedLLMs = new();
    private readonly Dictionary<Button, AIProviderPluginBase> _llmButtonMap = new();
    private readonly Dictionary<string, Color> _llmColors = new();

    public LLMChatView()
    {
        InitializeComponent();

        _chatSession = new LLMChatSession();
        _pluginManager = AppServices.Services?.GetService<PluginManager>()
            ?? throw new InvalidOperationException("PluginManager not available");

        // Initialize LLM color mappings
        InitializeLLMColors();

        // Subscribe to message collection changes
        _chatSession.Messages.CollectionChanged += (s, e) => RefreshMessageDisplay();

        // Hook into Loaded event to ensure plugins are available
        this.Loaded += (s, e) => SelectDefaultLLM();
    }

    /// <summary>
    /// Initialize color mappings for different LLM providers
    /// </summary>
    private void InitializeLLMColors()
    {
        // Assign distinct light colors for different LLM providers
        _llmColors["Claude"] = Color.FromArgb("#FFF3E0"); // Light Orange
        _llmColors["OpenAI"] = Color.FromArgb("#E8F5E8"); // Light Green
        _llmColors["Gemini"] = Color.FromArgb("#E3F2FD"); // Light Blue
        _llmColors["Grok"] = Color.FromArgb("#FCE4EC"); // Light Pink
        _llmColors["LLaMA"] = Color.FromArgb("#F3E5F5"); // Light Purple
        _llmColors["Mistral"] = Color.FromArgb("#FFF8E1"); // Light Yellow
        _llmColors["Perplexity"] = Color.FromArgb("#E0F2F1"); // Light Teal
        _llmColors["Cohere"] = Color.FromArgb("#FBE9E7"); // Light Red
        _llmColors["Hugging Face"] = Color.FromArgb("#E8EAF6"); // Light Indigo
        _llmColors["Anthropic"] = Color.FromArgb("#FFF3E0"); // Light Orange (same as Claude)
    }

    /// <summary>
    /// Set the current file context for the chat
    /// </summary>
    public void SetFileContext(string? filePath, string? fileContent)
    {
        _chatSession.CurrentFilePath = filePath;
        _chatSession.CurrentFileContent = fileContent;

        // Refresh display to show loaded messages
        RefreshMessageDisplay();
    }

    /// <summary>
    /// Select the default LLM (first enabled AI provider with valid API key)
    /// </summary>
    private void SelectDefaultLLM()
    {
        var allPlugins = _pluginManager.GetAllPlugins().ToList();
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Total plugins loaded: {allPlugins.Count}");

        var aiProviders = allPlugins
            .OfType<AIProviderPluginBase>()
            .ToList();
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] AI provider plugins found: {aiProviders.Count}");

        foreach (var provider in aiProviders)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView]   - {provider.Name}: IsEnabled={provider.IsEnabled}");
        }

        var enabledProviders = aiProviders.Where(p => p.IsEnabled).ToList();
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Enabled AI providers: {enabledProviders.Count}");

        // Select only providers with valid API keys
        var validProviders = enabledProviders.Where(p => p.HasValidAuthorization()).ToList();
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Valid AI providers (with API keys): {validProviders.Count}");

        _selectedLLMs.AddRange(validProviders);
        if (_selectedLLMs.Count > 0)
        {
            var llmNames = string.Join(", ", _selectedLLMs.Select(llm => llm.Name));
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Selected default LLMs: {llmNames}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] No valid LLM providers available");
        }

        CreateLLMButtons();
    }

    /// <summary>
    /// Update the LLM buttons highlighting for multi-selection
    /// </summary>
    private void UpdateLLMButtons()
    {
        foreach (var kvp in _llmButtonMap)
        {
            var button = kvp.Key;
            var llm = kvp.Value;

            if (_selectedLLMs.Contains(llm))
            {
                // Highlight selected LLMs
                button.BackgroundColor = Color.FromArgb("#512BD4");
                button.TextColor = Colors.White;
            }
            else
            {
                // Unhighlight other LLMs
                button.BackgroundColor = Colors.LightGray;
                button.TextColor = Colors.Black;
            }
        }
    }

    /// <summary>
    /// Create LLM buttons for all enabled providers
    /// </summary>
    private void CreateLLMButtons()
    {
        LLMButtonsStack.Children.Clear();
        _llmButtonMap.Clear();

        var enabledLLMs = _pluginManager.GetAllPlugins()
            .OfType<AIProviderPluginBase>()
            .Where(p => p.IsEnabled && p.HasValidAuthorization())
            .ToList();

        foreach (var llm in enabledLLMs)
        {
            var button = new Button
            {
                Text = llm.Name,
                Padding = new Thickness(10, 5),
                FontSize = 12,
                WidthRequest = 80,
                HeightRequest = 30
            };

            button.Clicked += (s, e) => SelectLLM(llm);

            _llmButtonMap[button] = llm;
            LLMButtonsStack.Children.Add(button);
        }

        UpdateLLMButtons();
    }

    /// <summary>
    /// Toggle LLM selection and update highlighting
    /// </summary>
    private void SelectLLM(AIProviderPluginBase llm)
    {
        if (_selectedLLMs.Contains(llm))
        {
            _selectedLLMs.Remove(llm);
        }
        else
        {
            _selectedLLMs.Add(llm);
        }

        UpdateLLMButtons();
        var llmNames = string.Join(", ", _selectedLLMs.Select(l => l.Name));
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Selected LLMs: {llmNames}");
    }

    
    /// <summary>
    /// Handle send button click
    /// </summary>
    private async void OnSendClicked(object sender, EventArgs e)
    {
        var userMessage = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(userMessage))
            return;

        // Check if we have a file selected or chat configured
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] OnSendClicked - IsConfigured: '{_chatSession.IsConfigured}', CurrentFilePath: '{_chatSession.CurrentFilePath}'");

        if (!_chatSession.IsConfigured)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Chat not configured, prompting for save location");
            var savePath = await PromptForSaveLocation();
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation returned: '{savePath}' (length: {savePath?.Length ?? 0})");

            if (string.IsNullOrEmpty(savePath))
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] User cancelled save location prompt or empty path returned");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] User selected save path: '{savePath}'");
            // For direct chat files (no context file), set the chat file path directly
            _chatSession.SetChatFilePath(savePath);
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] After SetChatFilePath - IsConfigured: '{_chatSession.IsConfigured}'");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Chat already configured - CurrentFilePath: '{_chatSession.CurrentFilePath}'");
        }

        // Check if we have any LLMs selected
        if (_selectedLLMs.Count == 0)
        {
            await DisplayAlert("No LLM Selected", "Please select at least one LLM provider.", "OK");
            return;
        }

        // Clear input and add user message
        MessageInput.Text = string.Empty;
        _chatSession.AddUserMessage(userMessage);

        // Send to LLM
        await SendMessageToLLM(userMessage);
    }

    /// <summary>
    /// Send message to all selected LLMs
    /// </summary>
    private async Task SendMessageToLLM(string userMessage)
    {
        if (_selectedLLMs.Count == 0)
            return;

        try
        {
            // Build conversation history with file context
            var history = new List<(string Role, string Content)>();

            // Add file context as system message if available
            if (!string.IsNullOrEmpty(_chatSession.CurrentFileContent))
            {
                var fileContent = _chatSession.CurrentFileContent;

                // Check if file is too large (>100KB)
                var contextMessage = $"File: {_chatSession.CurrentFilePath}\n\n";
                if (fileContent.Length > 100_000)
                {
                    contextMessage += "[File content truncated due to size]\n\n";
                    fileContent = fileContent[..100_000];
                }

                contextMessage += "```\n" + fileContent + "\n```";
                history.Add(("system", contextMessage));
            }

            // Add conversation history (last 10 messages for context)
            foreach (var msg in _chatSession.Messages.TakeLast(10).Where(m => !m.IsError))
            {
                history.Add((msg.Role, msg.Content));
            }

            // Send message to all selected LLMs in parallel
            var tasks = _selectedLLMs.Select(async llm =>
            {
                try
                {
                    var response = await llm.SendChatMessageAsync(history, userMessage);
                    _chatSession.AddAssistantMessage(response, llm.Name);
                }
                catch (Exception ex)
                {
                    _chatSession.AddErrorMessage(ex.Message, llm.Name);
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            // If there's a general error (not specific to one LLM), add error message
            var llmNames = string.Join(", ", _selectedLLMs.Select(llm => llm.Name));
            _chatSession.AddErrorMessage(ex.Message, llmNames);
            await HandleLLMError(ex);
        }
    }

    /// <summary>
    /// Handle LLM error with retry options
    /// </summary>
    private async Task HandleLLMError(Exception ex)
    {
        var otherLLMs = _pluginManager.GetAllPlugins()
            .OfType<AIProviderPluginBase>()
            .Where(p => p.IsEnabled && p.HasValidAuthorization() && !_selectedLLMs.Contains(p))
            .ToList();

        var options = new List<string> { "Retry with same LLMs" };

        if (otherLLMs.Count > 0)
        {
            options.AddRange(otherLLMs.Select(llm => $"Try with {llm.Name}"));
        }

        options.Add("Cancel");

        var action = await DisplayActionSheet(
            $"Error: {ex.Message}",
            "Cancel",
            null,
            [.. options]
        );

        if (action == "Retry with same LLMs")
        {
            var lastUserMessage = _chatSession.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage != null)
            {
                await SendMessageToLLM(lastUserMessage.Content);
            }
        }
        else if (action != null && action.StartsWith("Try with "))
        {
            var llmName = action["Try with ".Length..];
            var newLLM = otherLLMs.FirstOrDefault(llm => llm.Name == llmName);
            if (newLLM != null)
            {
                _selectedLLMs.Clear();
                _selectedLLMs.Add(newLLM);
                UpdateLLMButtons();
            }

            var lastUserMessage = _chatSession.Messages.LastOrDefault(m => m.Role == "user");
            if (lastUserMessage != null)
            {
                await SendMessageToLLM(lastUserMessage.Content);
            }
        }
    }

    /// <summary>
    /// Prompt user for save location if no file is selected
    /// </summary>
    private async Task<string?> PromptForSaveLocation()
    {
        // Get platform-specific Documents folder
        var documentsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // Create default filename with current date-time
        var defaultFileName = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.chat.md";
        var defaultPath = System.IO.Path.Combine(documentsFolder, defaultFileName);

        System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - Documents folder: '{documentsFolder}'");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - Default filename: '{defaultFileName}'");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - Default path: '{defaultPath}'");

        // If Documents folder path is empty or invalid, use a simpler fallback
        if (string.IsNullOrEmpty(documentsFolder) || documentsFolder.Length < 5)
        {
            defaultPath = defaultFileName; // Just use the filename
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - Using fallback path: '{defaultPath}'");
        }

        // Try the prompt dialog with a shorter message and default value
        var result = await DisplayPromptAsync(
            "Save Chat",
            "Enter file name or accept default:",
            "Save",
            "Cancel",
            defaultFileName // Use shorter default value
        );

        System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - DisplayPromptAsync result: '{result}' (length: {result?.Length ?? 0})");

        // Check if user cancelled the dialog
        if (result == null)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - User clicked Cancel");
            return null; // Return null to indicate cancellation
        }

        // Check if user entered empty text and clicked OK
        if (string.IsNullOrEmpty(result))
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - User entered empty text, using default path");
            result = defaultPath;
        }
        else
        {
            // If user provided just a filename, combine with Documents folder
            if (!result.Contains('\\') && !result.Contains('/'))
            {
                result = System.IO.Path.Combine(documentsFolder, result);
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] PromptForSaveLocation - Combined with Documents folder: '{result}'");
            }
        }

        return result;
    }

    /// <summary>
    /// Refresh the message display
    /// </summary>
    private void RefreshMessageDisplay()
    {
        MessagesContainer.Children.Clear();

        foreach (var message in _chatSession.Messages)
        {
            var messageView = CreateMessageView(message);
            MessagesContainer.Children.Add(messageView);
        }

        // Scroll to bottom
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100); // Give UI time to render
            await ChatScrollView.ScrollToAsync(0, ChatScrollView.Content.Height, false);
        });
    }

    /// <summary>
    /// Create a view for a single message
    /// </summary>
    private Border CreateMessageView(LLMChatMessage message)
    {
        var border = new Border
        {
            Padding = 10,
            Margin = new Thickness(0, 5),
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Stroke = Colors.Transparent
        };

        var stack = new VerticalStackLayout { Spacing = 5 };

        // Header with timestamp and sender
        var header = new Label
        {
            FontAttributes = FontAttributes.Bold,
            FontSize = 12
        };

        if (message.Role == "user")
        {
            var llmNames = _selectedLLMs.Count > 0
                ? string.Join(", ", _selectedLLMs.Select(llm => llm.Name))
                : "LLM";
            header.Text = $"You -> {llmNames} - {message.Timestamp:HH:mm:ss}";
            border.BackgroundColor = Color.FromArgb("#E3F2FD");
        }
        else if (message.IsError)
        {
            header.Text = $"{message.LLMProvider ?? "LLM"} Error - {message.Timestamp:HH:mm:ss}";
            // Use a light red background for errors, but still try to get LLM-specific color
            var llmProvider = message.LLMProvider ?? "";
            if (_llmColors.TryGetValue(llmProvider, out var errorColor))
            {
                // Blend with light red - make it slightly darker/pinker
                border.BackgroundColor = Color.FromArgb("#FFCDD2"); // Light red
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#FFEBEE"); // Default light red
            }
        }
        else
        {
            header.Text = $"{message.LLMProvider ?? "LLM"} replied - {message.Timestamp:HH:mm:ss}";
            // Use LLM-specific color for normal responses
            var llmProvider = message.LLMProvider ?? "";
            if (_llmColors.TryGetValue(llmProvider, out var llmColor))
            {
                border.BackgroundColor = llmColor;
            }
            else
            {
                border.BackgroundColor = Color.FromArgb("#F1F8E9"); // Default light green
            }
        }

        stack.Children.Add(header);

        // Content
        var content = new Label
        {
            Text = message.IsError ? message.ErrorMessage : message.Content,
            TextType = TextType.Text,
            FontSize = 14
        };

        stack.Children.Add(content);

        border.Content = stack;
        return border;
    }

    /// <summary>
    /// Helper to display alerts
    /// </summary>
    private Task DisplayAlert(string title, string message, string cancel)
    {
        var page = Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            return page.DisplayAlert(title, message, cancel);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Helper to display action sheets
    /// </summary>
    private Task<string> DisplayActionSheet(string title, string cancel, string? destruction, params string[] buttons)
    {
        var page = Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            return page.DisplayActionSheet(title, cancel, destruction, buttons);
        }
        return Task.FromResult(cancel);
    }

    /// <summary>
    /// Helper to display prompts
    /// </summary>
    private Task<string> DisplayPromptAsync(string title, string message, string accept, string cancel, string placeholder)
    {
        var page = Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            return page.DisplayPromptAsync(title, message, accept, cancel, placeholder);
        }
        return Task.FromResult(string.Empty);
    }
}
