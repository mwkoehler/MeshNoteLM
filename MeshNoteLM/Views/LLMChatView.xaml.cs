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
    private readonly List<AIProviderPluginBase> _selectedLLMs = [];
    private readonly Dictionary<Button, AIProviderPluginBase> _llmButtonMap = [];
    private readonly Dictionary<string, Color> _llmColors = [];

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
        this.Loaded += (s, e) =>
        {
            SelectDefaultLLM();
            SetupKeyboardHandlers();
        };
    }

    /// <summary>
    /// Handle Enter key press in Editor (when user presses Enter on external keyboard)
    /// </summary>
    private void OnMessageInputCompleted(object sender, EventArgs e)
    {
        // Enter key was pressed - send the message
        _ = SendMessage();
    }

    
    /// <summary>
    /// Setup keyboard handlers for keyboard shortcuts
    /// </summary>
    private void SetupKeyboardHandlers()
    {
        // Platform-specific keyboard handling
#if WINDOWS || MACCATALYST
        SetupDesktopKeyboardHandlers();
#else
        // Mobile platforms - use touch/standard mobile patterns
        System.Diagnostics.Debug.WriteLine("[LLMChatView] Mobile platform: Using standard touch interface");
#endif
    }

#if WINDOWS || MACCATALYST
    /// <summary>
    /// Setup desktop-specific keyboard handlers
    /// </summary>
    private void SetupDesktopKeyboardHandlers()
    {
        // Desktop platforms support better keyboard handling
        System.Diagnostics.Debug.WriteLine("[LLMChatView] Desktop platform: Setting up enhanced keyboard shortcuts");

        // Add platform-specific keyboard event handling
        this.Loaded += OnDesktopViewLoaded;
    }

    /// <summary>
    /// Handle desktop view loaded to setup keyboard shortcuts
    /// </summary>
    private void OnDesktopViewLoaded(object? sender, EventArgs e)
    {
        // Focus the message input for better keyboard handling
        MessageInput.Focus();

#if WINDOWS
        SetupWindowsKeyboardHandlers();
#elif MACCATALYST
        SetupMacKeyboardHandlers();
#endif
    }

#if WINDOWS
    /// <summary>
    /// Windows-specific keyboard handling
    /// </summary>
    private void SetupWindowsKeyboardHandlers()
    {
        // Enhanced Windows keyboard handling
        try
        {
            // On Windows, we can add keyboard accelerators programmatically
            // The Editor's Completed event is more reliable on Windows
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Windows: Enhanced keyboard handling enabled");

            // On Windows, users can use Ctrl+Enter which may trigger the Completed event
            // Or they can use the Send button
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Windows keyboard setup error: {ex.Message}");
        }
    }
#endif

#if MACCATALYST
    /// <summary>
    /// macOS-specific keyboard handling
    /// </summary>
    private void SetupMacKeyboardHandlers()
    {
        // Enhanced macOS keyboard handling
        try
        {
            // On macOS, the Completed event works more reliably with keyboard shortcuts
            // Standard Mac keyboard shortcuts are more naturally supported
            System.Diagnostics.Debug.WriteLine("[LLMChatView] macOS: Enhanced keyboard handling enabled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] macOS keyboard setup error: {ex.Message}");
        }
    }
#endif
#endif

    
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

        // Clear existing selections and add new ones
        _selectedLLMs.Clear();
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
                Padding = new Thickness(8, 4),
                FontSize = 11,
                HeightRequest = 28,
                MinimumWidthRequest = 60
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
        if (!_selectedLLMs.Remove(llm))
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
        await SendMessage();
    }

    
    /// <summary>
    /// Send message common logic
    /// </summary>
    private async Task SendMessage()
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
    /// Send message to all selected LLMs or a specific LLM if prefixed with "LLM Name:"
    /// </summary>
    private async Task SendMessageToLLM(string userMessage)
    {
        if (_selectedLLMs.Count == 0)
            return;

        try
        {
            // Check if message is directed to a specific LLM
            var targetedLLMs = ParseLLMTargeting(userMessage, out var actualMessage);

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

            // Send message to targeted LLMs in parallel
            var tasks = targetedLLMs.Select(async llm =>
            {
                try
                {
                    var response = await llm.SendChatMessageAsync(history, actualMessage);
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
    /// Parse message to check if it's directed to a specific LLM
    /// Format: "LLM Name: message"
    /// Returns the list of LLMs to send the message to and the actual message (without LLM prefix)
    /// </summary>
    private List<AIProviderPluginBase> ParseLLMTargeting(string userMessage, out string actualMessage)
    {
        actualMessage = userMessage;

        // Check for LLM targeting pattern: "LLM Name: message"
        var colonIndex = userMessage.IndexOf(':');
        if (colonIndex > 0) // Must have content before colon
        {
            var targetLLMName = userMessage.Substring(0, colonIndex).Trim();
            actualMessage = userMessage.Substring(colonIndex + 1).Trim();

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] LLM targeting detected: '{targetLLMName}' -> '{actualMessage}'");

            // Find the targeted LLM among all available plugins
            var allLLMs = _pluginManager.GetAllPlugins()
                .OfType<AIProviderPluginBase>()
                .Where(p => p.IsEnabled && p.HasValidAuthorization())
                .ToList();

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Available LLMs: {string.Join(", ", allLLMs.Select(l => l.Name))}");

            // Try exact match first, then case-insensitive match
            var targetedLLM = allLLMs.FirstOrDefault(llm =>
                llm.Name.Equals(targetLLMName, StringComparison.OrdinalIgnoreCase));

            if (targetedLLM != null)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Message directed to specific LLM: {targetedLLM.Name}");
                return new List<AIProviderPluginBase> { targetedLLM };
            }
            else
            {
                // LLM not found or not enabled, restore original message and use selected LLMs
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Targeted LLM '{targetLLMName}' not found or not enabled, using selected LLMs");
                actualMessage = userMessage; // Restore original message
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] No LLM targeting detected, using selected LLMs");
        }

        // No specific targeting, use selected LLMs
        return new List<AIProviderPluginBase>(_selectedLLMs);
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
    private static async Task<string?> PromptForSaveLocation()
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

        // Add chat file link if chat is configured
        if (_chatSession.IsConfigured && !string.IsNullOrEmpty(_chatSession.ChatFilePath))
        {
            var fileLinkView = CreateChatFileLinkView(_chatSession.ChatFilePath);
            MessagesContainer.Children.Add(fileLinkView);
        }

        foreach (var message in _chatSession.Messages)
        {
            var messageView = CreateMessageView(message);
            MessagesContainer.Children.Add(messageView);
        }

        // Debug scroll information
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Messages count: {_chatSession.Messages.Count}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] ScrollView content height: {ChatScrollView.Content?.Height ?? 0}");

        // Scroll to bottom with more reliable method
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(150); // Give UI more time to render

            try
            {
                // Force layout update
                this.InvalidateMeasure();
                await Task.Delay(50);

                // Multiple scroll attempts for reliability
                await ChatScrollView.ScrollToAsync(ChatScrollView, ScrollToPosition.End, animated: false);
                await Task.Delay(25);

                // Alternative scroll method
                if (ChatScrollView.Content != null)
                {
                    await ChatScrollView.ScrollToAsync(0, ChatScrollView.Content.Height, animated: false);
                }

                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Scrolled to bottom. ScrollView height: {ChatScrollView.Height}, Content height: {ChatScrollView.Content?.Height ?? 0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error scrolling to bottom: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Create a view for the chat file link
    /// </summary>
    private static Border CreateChatFileLinkView(string chatFilePath)
    {
        var border = new Border
        {
            Padding = 8,
            Margin = new Thickness(0, 5),
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            Stroke = Color.FromArgb("#DDDDDD"),
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var stack = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center
        };

        var fileIcon = new Label
        {
            Text = "📄",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        };

        var linkLabel = new Label
        {
            Text = $"Chat file: {System.IO.Path.GetFileName(chatFilePath)}",
            FontSize = 12,
            TextColor = Colors.Blue,
            VerticalOptions = LayoutOptions.Center,
            GestureRecognizers =
            {
                new TapGestureRecognizer
                {
                    Command = new Command(async () => await OpenChatFile(chatFilePath))
                }
            }
        };

        var pathLabel = new Label
        {
            Text = chatFilePath,
            FontSize = 10,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        };

        stack.Children.Add(fileIcon);
        stack.Children.Add(linkLabel);
        stack.Children.Add(pathLabel);

        border.Content = stack;
        return border;
    }

    /// <summary>
    /// Open the chat file
    /// </summary>
    private static async Task OpenChatFile(string chatFilePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Opening chat file: {chatFilePath}");

            // Try to open with default application
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(chatFilePath)
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error opening chat file: {ex.Message}");
            await DisplayAlert("Error", $"Could not open file: {ex.Message}", "OK");
        }
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
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] User message header LLMs: {llmNames} (Count: {_selectedLLMs.Count})");
            header.Text = $"You -> {llmNames} - {message.Timestamp:MM/dd/yyyy HH:mm:ss}";
            border.BackgroundColor = Color.FromArgb("#E3F2FD");
        }
        else if (message.IsError)
        {
            header.Text = $"{message.LLMProvider ?? "LLM"} Error - {message.Timestamp:MM/dd/yyyy HH:mm:ss}";
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
            header.Text = $"{message.LLMProvider ?? "LLM"} replied - {message.Timestamp:MM/dd/yyyy HH:mm:ss}";
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
    private static Task DisplayAlert(string title, string message, string cancel)
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
    private static Task<string> DisplayActionSheet(string title, string cancel, string? destruction, params string[] buttons)
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
    private static Task<string> DisplayPromptAsync(string title, string message, string accept, string cancel, string placeholder)
    {
        var page = Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            return page.DisplayPromptAsync(title, message, accept, cancel, placeholder);
        }
        return Task.FromResult(string.Empty);
    }
}
