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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
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
        _chatSession.Messages.CollectionChanged += (s, e) =>
        {
            RefreshMessageDisplay();
            // Additional delayed scroll to ensure we're at the bottom after UI settles
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(300); // Longer delay for UI to completely settle
                await ScrollToBottomSafely();
            });
        };

        // Hook into Loaded event to ensure plugins are available
        this.Loaded += (s, e) =>
        {
            SelectDefaultLLM();
            SetupKeyboardHandlers();

            // Debug initial layout state
            DebugLayoutState();
        };

        // Handle keyboard events at the page level
#if WINDOWS || MACCATALYST
        this.Focused += OnChatViewFocused;
#endif
    }

    /// <summary>
    /// Debug the layout state to understand sizing issues
    /// </summary>
    private void DebugLayoutState()
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100); // Let layout settle

            System.Diagnostics.Debug.WriteLine($"=== LLMChatView Layout Debug ===");
            System.Diagnostics.Debug.WriteLine($"Main Grid Height: {this.Height}");
            System.Diagnostics.Debug.WriteLine($"Main Grid Width: {this.Width}");
            System.Diagnostics.Debug.WriteLine($"ScrollView Height: {ChatScrollView.Height}");
            System.Diagnostics.Debug.WriteLine($"ScrollView Width: {ChatScrollView.Width}");
            System.Diagnostics.Debug.WriteLine($"ScrollView Content Height: {ChatScrollView.Content?.Height ?? 0}");
            System.Diagnostics.Debug.WriteLine($"MessagesContainer Height: {MessagesContainer.Height}");
            System.Diagnostics.Debug.WriteLine($"MessagesContainer Children Count: {MessagesContainer.Children.Count}");
            System.Diagnostics.Debug.WriteLine($"================================");
        });
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

            // Set up keyboard event handling for the ScrollView
            ChatScrollView.Focused += OnScrollViewFocused;

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

            // Set up keyboard event handling for the ScrollView
            ChatScrollView.Focused += OnScrollViewFocused;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] macOS keyboard setup error: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// Handle ScrollView focus for keyboard navigation (shared)
    /// </summary>
    private void OnScrollViewFocused(object? sender, FocusEventArgs e)
    {
        // ScrollView is focused, ensure it can handle keyboard input
        System.Diagnostics.Debug.WriteLine("[LLMChatView] ScrollView focused, keyboard navigation enabled");

        // Add keyboard event handler for desktop platforms
        SetupKeyboardScrolling();
    }

  /// <summary>
    /// Setup keyboard scrolling for desktop platforms
    /// </summary>
    private void SetupKeyboardScrolling()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Setting up keyboard scrolling for desktop platform");

            // Ensure ScrollView can receive focus
            ChatScrollView.IsEnabled = true;
            ChatScrollView.Focus();

            // Try to set focus to ScrollView when message input loses focus
            MessageInput.Unfocused += OnMessageInputUnfocused;

            // Also try to set focus when ScrollView is tapped
            AddTapGestureRecognizerToScrollView();

            System.Diagnostics.Debug.WriteLine("[LLMChatView] Keyboard navigation enabled - Try clicking in the chat area, then use PageUp/PageDown/Home/End/Arrow keys");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error setting up keyboard scrolling: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle message input losing focus - transfer to ScrollView
    /// </summary>
    private void OnMessageInputUnfocused(object? sender, FocusEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] MessageInput lost focus, setting focus to ScrollView");
            ChatScrollView.Focus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error transferring focus to ScrollView: {ex.Message}");
        }
    }

    /// <summary>
    /// Add tap gesture recognizer to ScrollView for focus
    /// </summary>
    private void AddTapGestureRecognizerToScrollView()
    {
        try
        {
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[LLMChatView] ScrollView tapped, setting focus");
                ChatScrollView.Focus();
            };

            ChatScrollView.GestureRecognizers.Add(tapGesture);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error adding tap gesture: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle chat view getting focus - setup keyboard handlers
    /// </summary>
    private void OnChatViewFocused(object? sender, FocusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[LLMChatView] ChatView focused, setting up global keyboard handlers");
        SetupGlobalKeyboardHandlers();
    }

#if WINDOWS || MACCATALYST
    /// <summary>
    /// Setup global keyboard handlers for the entire view
    /// </summary>
    private void SetupGlobalKeyboardHandlers()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Setting up simple keyboard navigation");

            // For now, add visual instructions for the user
            // .NET MAUI ScrollView keyboard navigation requires complex platform-specific handling
            System.Diagnostics.Debug.WriteLine("[LLMChatView] NOTE: Keyboard navigation in .NET MAUI ScrollView requires:");
            System.Diagnostics.Debug.WriteLine("[LLMChatView] 1. Click in the chat area to focus the ScrollView");
            System.Diagnostics.Debug.WriteLine("[LLMChatView] 2. Use mouse wheel or scrollbar to navigate");
            System.Diagnostics.Debug.WriteLine("[LLMChatView] 3. Custom keyboard handling requires platform-specific implementations");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error setting up keyboard handlers: {ex.Message}");
        }
    }
#endif

    /// <summary>
    /// Handle keyboard events for enhanced scrolling on desktop platforms
    /// </summary>
    private void OnKeyboardKeyPressed(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Keyboard key pressed in ScrollView");
            // This is a placeholder for any custom keyboard handling
            // .NET MAUI ScrollView should handle most keyboard navigation automatically
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error handling keyboard input: {ex.Message}");
        }
    }
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
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] SetFileContext called with: '{filePath}'");
        if (!string.IsNullOrEmpty(fileContent))
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] File content length: {fileContent.Length}");
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] File content preview: {fileContent[..Math.Min(200, fileContent.Length)]}...");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] File content is null or empty");
        }

        _chatSession.CurrentFilePath = filePath;
        _chatSession.CurrentFileContent = fileContent;

        System.Diagnostics.Debug.WriteLine($"[LLMChatView] _chatSession.CurrentFilePath set to: '{_chatSession.CurrentFilePath}'");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] _chatSession.CurrentFileContent length: {_chatSession.CurrentFileContent?.Length ?? 0}");

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
                Padding = new Thickness(6, 2),
                FontSize = 10,
                HeightRequest = 24,
                MinimumWidthRequest = 50
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
    /// Handle Close Chat button click
    /// </summary>
    private void OnCloseChatClicked(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Close Chat button clicked");

            // Clear the current chat session
            _chatSession.ClearConversation();

            // Reset the file context
            _chatSession.CurrentFilePath = null;
            _chatSession.CurrentFileContent = null;

            // Clear the message display
            MessagesContainer.Children.Clear();

            System.Diagnostics.Debug.WriteLine("[LLMChatView] Chat session closed and cleared");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error closing chat: {ex.Message}");
        }
    }

    /// <summary>
    /// Handle Load Chat button click
    /// </summary>
    private async void OnLoadChatClicked(object sender, EventArgs e)
    {
        await LoadChatLogFile();
    }

    /// <summary>
    /// Handle Rename/Move Chat button click
    /// </summary>
    private async void OnRenameMoveChatClicked(object sender, EventArgs e)
    {
        await RenameMoveChatFile();
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
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Chat not configured, auto-creating chat file");
            var savePath = AutoCreateUniqueChatFile();
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] AutoCreateUniqueChatFile returned: '{savePath}' (length: {savePath?.Length ?? 0})");

            if (string.IsNullOrEmpty(savePath))
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Failed to auto-create chat file");
                await DisplayAlert("Error", "Could not create chat file. Please try again.", "OK");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Auto-created chat file: '{savePath}'");
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
                var fileName = Path.GetFileName(_chatSession.CurrentFilePath);

                // Reduce file size limit to avoid token limits (aim for ~2K characters for system message)
                var maxSystemMessageSize = 2_000;
                var contextMessage = $"File: {fileName}\n\n";

                // Truncate file content more aggressively to stay within system message limits
                if (fileContent.Length > maxSystemMessageSize - 200) // Reserve 200 chars for filename and text
                {
                    contextMessage += "[Content truncated for brevity]\n\n";
                    fileContent = fileContent[..(maxSystemMessageSize - 200)];
                }

                contextMessage += fileContent;

                // Final safety check - ensure system message doesn't exceed limit
                if (contextMessage.Length > maxSystemMessageSize)
                {
                    contextMessage = contextMessage[..maxSystemMessageSize] + "...";
                }

                history.Add(("system", contextMessage));

                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Added file context for {fileName}, file content length: {fileContent.Length}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] System message length: {contextMessage.Length}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Context message preview: {contextMessage[..Math.Min(150, contextMessage.Length)]}...");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LLMChatView] No file content available for context");
            }

            // Add conversation history (last 5 messages for context to reduce token usage)
            foreach (var msg in _chatSession.Messages.TakeLast(5).Where(m => !m.IsError))
            {
                history.Add((msg.Role, msg.Content));
            }

            // Send message to targeted LLMs in parallel
            var tasks = targetedLLMs.Select(async llm =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Sending to {llm.Name}:");
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] History count: {history.Count}");
                    if (history.Count > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LLMChatView] First history entry ({history[0].Role}): {history[0].Content[..Math.Min(100, history[0].Content.Length)]}...");
                    }
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] User message: {actualMessage}");

                    // Calculate total content size for debugging
                    var totalContentSize = history.Sum(h => h.Content.Length) + actualMessage.Length;
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Total content size: {totalContentSize} characters");

                    var response = await llm.SendChatMessageAsync(history, actualMessage);
                    _chatSession.AddAssistantMessage(response, llm.Name);
                }
                catch (HttpRequestException httpEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] HTTP Error from {llm.Name}: {httpEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] StatusCode: {httpEx.StatusCode}");
                    _chatSession.AddErrorMessage($"HTTP {httpEx.StatusCode}: {httpEx.Message}", llm.Name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error from {llm.Name}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Exception type: {ex.GetType().Name}");
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
            var targetLLMName = userMessage[..colonIndex].Trim();
            actualMessage = userMessage[(colonIndex + 1)..].Trim();

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
                return [targetedLLM];
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
        return [.. _selectedLLMs];
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
    /// Auto-create a unique chat file in the local filesystem
    /// </summary>
    private static string? AutoCreateUniqueChatFile()
    {
        try
        {
            // Prefer AppDataDirectory for app-specific files, fallback to Documents
            var baseFolder = FileSystem.AppDataDirectory;
            if (string.IsNullOrEmpty(baseFolder))
            {
                baseFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            // If still no valid folder, use current directory as last resort
            if (string.IsNullOrEmpty(baseFolder))
            {
                baseFolder = Directory.GetCurrentDirectory();
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] AutoCreateUniqueChatFile - Base folder: '{baseFolder}'");

            // Create base filename with current date-time including tenths of a second
            var baseFileName = $"Chat_{DateTime.Now:yyyy-MM-dd_HH-mm-ss.f}.chat.md";

            // Generate unique filename (with tenths of second increments if needed)
            var uniquePath = GenerateUniqueFileName(baseFolder, baseFileName);

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] AutoCreateUniqueChatFile - Created unique path: '{uniquePath}'");
            return uniquePath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error auto-creating chat file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Generate a unique filename by retrying with increasing tenths of a second
    /// </summary>
    private static string GenerateUniqueFileName(string folder, string baseFileName)
    {
        var basePath = System.IO.Path.Combine(folder, baseFileName);

        // If the base filename doesn't exist, use it
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        // Extract the full extension (.chat.md) and base name pattern
        var extension = ".chat.md"; // We always use this extension for chat files
        var baseNameOnly = "Chat"; // Base name without timestamp

        // Retry with increasing tenths of a second
        var tenths = 1;
        string uniquePath;

        do
        {
            // Generate new timestamp with additional tenths
            var newTimestamp = DateTime.Now.AddMilliseconds(tenths * 100).ToString("yyyy-MM-dd_HH-mm-ss.f");
            var uniqueName = $"{baseNameOnly}_{newTimestamp}{extension}";
            uniquePath = System.IO.Path.Combine(folder, uniqueName);
            tenths++;
        }
        while (File.Exists(uniquePath) && tenths < 100); // Prevent infinite loop (up to 10 seconds of retries)

        System.Diagnostics.Debug.WriteLine($"[LLMChatView] GenerateUniqueFileName - Base: '{baseFileName}', Unique: '{uniquePath}'");
        return uniquePath;
    }

    /// <summary>
    /// Prompt user for save location if no file is selected (kept for backward compatibility)
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

        // Debug information before layout
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Messages count: {_chatSession.Messages.Count}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] MessagesContainer children: {MessagesContainer.Children.Count}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] ScrollView content height (before): {ChatScrollView.Content?.Height ?? 0}");

        // Force the VerticalStackLayout to measure its content properly
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(100); // Let UI start processing

            try
            {
                // Force the MessagesContainer to recalculate its size
                MessagesContainer.InvalidateMeasure();
                await Task.Delay(50);

                // Force measure on the entire view hierarchy
                this.InvalidateMeasure();
                await Task.Delay(50);

                // Force the ScrollView content to remeasure
                ChatScrollView.Content?.InvalidateMeasure();
                ChatScrollView.InvalidateMeasure();
                await Task.Delay(100);

                // Debug information after layout updates
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] ScrollView height: {ChatScrollView.Height}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] ScrollView content height (after): {ChatScrollView.Content?.Height ?? 0}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] MessagesContainer height: {MessagesContainer.Height}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] MessagesContainer measured height: {MessagesContainer.DesiredSize.Height}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Parent Grid height: {(this.Parent as View)?.Height ?? 0}");

                // Check if content actually exceeds viewport
                var contentHeight = ChatScrollView.Content?.Height ?? 0;
                var viewportHeight = ChatScrollView.Height;
                var needsScrolling = contentHeight > viewportHeight;

                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Content height: {contentHeight}, Viewport height: {viewportHeight}, Needs scrolling: {needsScrolling}");
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Grid Row 1 should be fixed at 250px - ScrollView viewport should be ~250px");

                // If content doesn't seem to exceed viewport but we have many messages,
                // the ScrollView should still show scrollbar due to fixed Grid row height (250px)
                if (!needsScrolling && _chatSession.Messages.Count > 3)
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Content should scroll due to Grid Row 1 fixed height=250px");

                    // Force a final layout measure to ensure scrollbar appears
                    ChatScrollView.InvalidateMeasure();
                    await Task.Delay(50);
                }

                // Enhanced auto-scroll to bottom with multiple fallback methods
                if (ChatScrollView.Content != null)
                {
                    System.Diagnostics.Debug.WriteLine("[LLMChatView] Starting auto-scroll to bottom...");

                    // Method 1: Direct scroll to end position
                    try
                    {
                        await ChatScrollView.ScrollToAsync(ChatScrollView, ScrollToPosition.End, animated: false);
                        System.Diagnostics.Debug.WriteLine("[LLMChatView] Method 1: ScrollToPosition.End completed");
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Method 1 failed: {ex.Message}");
                    }

                    // Method 2: Scroll by calculated height if Method 1 didn't work
                    try
                    {
                        var finalContentHeight = ChatScrollView.Content?.Height ?? 0;
                        await ChatScrollView.ScrollToAsync(0, finalContentHeight, animated: false);
                        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Method 2: Scroll to height {finalContentHeight} completed");
                        await Task.Delay(50);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Method 2 failed: {ex.Message}");
                    }

                    // Method 3: Force layout and scroll again (final attempt)
                    try
                    {
                        ChatScrollView.LayoutTo(new Rect(ChatScrollView.Bounds.X, ChatScrollView.Bounds.Y,
                                                       ChatScrollView.Bounds.Width, ChatScrollView.Bounds.Height));
                        await Task.Delay(50);
                        await ChatScrollView.ScrollToAsync(ChatScrollView, ScrollToPosition.End, animated: false);
                        System.Diagnostics.Debug.WriteLine("[LLMChatView] Method 3: Layout + scroll completed");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Method 3 failed: {ex.Message}");
                    }
                }

#if WINDOWS || MACCATALYST
                // Enable keyboard navigation by focusing the ScrollView for desktop platforms
                ChatScrollView.Focus();
                System.Diagnostics.Debug.WriteLine("[LLMChatView] ScrollView focused for keyboard navigation");
                System.Diagnostics.Debug.WriteLine("[LLMChatView] Try clicking in the chat area and then use PageUp/PageDown/Home/End/Arrow keys");

                // Additional focus attempt after a delay
                await Task.Delay(100);
                ChatScrollView.Focus();
                System.Diagnostics.Debug.WriteLine("[LLMChatView] Second focus attempt completed");
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error in RefreshMessageDisplay: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Safely scroll to the bottom of the chat with error handling
    /// </summary>
    private async Task ScrollToBottomSafely()
    {
        try
        {
            if (ChatScrollView != null && MessagesContainer != null)
            {
                System.Diagnostics.Debug.WriteLine("[LLMChatView] ScrollToBottomSafely: Starting scroll to bottom");

                // Wait a bit more for any pending UI operations
                await Task.Delay(100);

                // Use the most reliable method
                await ChatScrollView.ScrollToAsync(ChatScrollView, ScrollToPosition.End, animated: false);

                System.Diagnostics.Debug.WriteLine("[LLMChatView] ScrollToBottomSafely: Successfully scrolled to bottom");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] ScrollToBottomSafely: Error scrolling to bottom: {ex.Message}");
        }
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
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
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

        // Check if this is a real file that exists on the filesystem
        var isRealFile = File.Exists(chatFilePath);
        var isCloudFile = IsCloudFile(chatFilePath, out var cloudUrl);
        var isVirtualPath = !isRealFile && !isCloudFile;

        // Debug output for path checking
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] Chat file path: '{chatFilePath}'");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] File.Exists result: {isRealFile}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] IsCloudFile result: {isCloudFile}");
        System.Diagnostics.Debug.WriteLine($"[LLMChatView] IsVirtualPath: {isVirtualPath}");
        if (!string.IsNullOrEmpty(cloudUrl))
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Cloud URL: '{cloudUrl}'");
        }

        var linkLabel = new Label
        {
            Text = $"Chat file: {System.IO.Path.GetFileName(chatFilePath)}",
            FontSize = 12,
            TextColor = isVirtualPath ? Colors.Gray : Colors.Blue,
            VerticalOptions = LayoutOptions.Center
        };

        // Add tap gesture for real files or cloud files
        if (isRealFile)
        {
            linkLabel.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await OpenChatFile(chatFilePath))
            });
        }
        else if (isCloudFile && !string.IsNullOrEmpty(cloudUrl))
        {
            linkLabel.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await OpenCloudFile(cloudUrl))
            });
        }

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
    /// Check if the file path represents a cloud file and generate web URL
    /// </summary>
    private static bool IsCloudFile(string filePath, out string webUrl)
    {
        webUrl = string.Empty;

        try
        {
            // Google Drive patterns
            if (filePath.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("/Google Drive/", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("gdocs://", StringComparison.OrdinalIgnoreCase))
            {
                // Extract file ID from Google Drive URL or path
                var fileId = ExtractGoogleDriveFileId(filePath);
                if (!string.IsNullOrEmpty(fileId))
                {
                    webUrl = $"https://drive.google.com/file/d/{fileId}/view";
                    return true;
                }
            }

            // OneDrive patterns
            if (filePath.Contains("onedrive.live.com", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("/OneDrive/", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("1drv.ms/", StringComparison.OrdinalIgnoreCase))
            {
                // Extract OneDrive file ID or use direct URL
                if (filePath.Contains("onedrive.live.com", StringComparison.OrdinalIgnoreCase))
                {
                    webUrl = filePath;
                    return true;
                }
            }

            // Dropbox patterns
            if (filePath.Contains("dropbox.com", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("/Dropbox/", StringComparison.OrdinalIgnoreCase))
            {
                // Generate Dropbox share URL
                if (filePath.Contains("dropbox.com", StringComparison.OrdinalIgnoreCase))
                {
                    webUrl = filePath;
                    return true;
                }
            }

            // iCloud patterns
            if (filePath.Contains("icloud.com", StringComparison.OrdinalIgnoreCase) ||
                filePath.Contains("/iCloud/", StringComparison.OrdinalIgnoreCase))
            {
                if (filePath.Contains("icloud.com", StringComparison.OrdinalIgnoreCase))
                {
                    webUrl = filePath;
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error checking cloud file: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Extract Google Drive file ID from various path formats
    /// </summary>
    private static string? ExtractGoogleDriveFileId(string filePath)
    {
        // Pattern 1: Direct Google Drive URL
        var urlMatch = Regex.Match(filePath, @"drive\.google\.com\/file\/d\/([a-zA-Z0-9_-]+)");
        if (urlMatch.Success)
            return urlMatch.Groups[1].Value;

        // Pattern 2: Google Drive export URL
        var exportMatch = Regex.Match(filePath, @"docs\.google\.com\/spreadsheets\/d\/([a-zA-Z0-9_-]+)");
        if (exportMatch.Success)
            return exportMatch.Groups[1].Value;

        // Pattern 3: Look for existing file ID in path
        var idMatch = Regex.Match(filePath, @"([a-zA-Z0-9_-]{33})");
        if (idMatch.Success)
            return idMatch.Groups[1].Value;

        return null;
    }

    /// <summary>
    /// Open cloud file in browser
    /// </summary>
    private static async Task OpenCloudFile(string webUrl)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Opening cloud file in browser: {webUrl}");

            // Open in default browser
            await Browser.Default.OpenAsync(webUrl, BrowserLaunchMode.SystemPreferred);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error opening cloud file: {ex.Message}");
            await DisplayAlert("Error", $"Could not open cloud file: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Open the chat file
    /// </summary>
    private static async Task OpenChatFile(string chatFilePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Opening chat file: {chatFilePath}");
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Full path: {Path.GetFullPath(chatFilePath)}");
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Current directory: {Directory.GetCurrentDirectory()}");
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] File.Exists before open: {File.Exists(chatFilePath)}");

            // Check if the file actually exists on the filesystem
            if (!File.Exists(chatFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Cannot open virtual/non-existent file: {chatFilePath}");
                await DisplayAlert("Cannot Open File",
                    "This chat file is stored in a virtual location and cannot be opened with the system file manager.", "OK");
                return;
            }

            // Try to open with default application
            // Use the full absolute path for better compatibility
            var fullPath = Path.GetFullPath(chatFilePath);
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Attempting to open full path: {fullPath}");

            try
            {
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(fullPath)
                });
            }
            catch (Exception launcherEx)
            {
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Launcher.OpenAsync failed: {launcherEx.Message}");

                // Fallback: try opening with Process.Start on Windows
                #if WINDOWS
                try
                {
                    using var process = new System.Diagnostics.Process();
                    process.StartInfo.FileName = fullPath;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Opened file with Process.Start");
                }
                catch (Exception processEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[LLMChatView] Process.Start also failed: {processEx.Message}");
                    throw; // Re-throw the original exception
                }
                #else
                throw; // Re-throw the original launcher exception on non-Windows platforms
                #endif
            }
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
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
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
        var content = new Editor
        {
            Text = message.IsError ? message.ErrorMessage : message.Content,
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.TextChanges,
            IsReadOnly = true,
            BackgroundColor = Colors.Transparent
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

    /// <summary>
    /// Load an existing chat log file
    /// </summary>
    private async Task LoadChatLogFile()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LLMChatView] Loading chat log file...");

            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Chat Log File"
            });

            if (result != null)
            {
                // Validate file extension
                var validExtensions = new[] { ".chat.md", ".md", ".txt" };
                var fileExtension = System.IO.Path.GetExtension(result.FullPath).ToLowerInvariant();

                if (!validExtensions.Contains(fileExtension))
                {
                    await DisplayAlert("Invalid File Type", "Please select a chat file (.chat.md, .md, or .txt)", "OK");
                    return;
                }

                await ProcessChatLogFile(result.FullPath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LLMChatView] No file selected");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error loading chat file: {ex.Message}");
            await DisplayAlert("Error", $"Could not load chat file: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Rename or move the current chat file
    /// </summary>
    private async Task RenameMoveChatFile()
    {
        try
        {
            // Check if we have a chat file to rename/move
            if (!_chatSession.IsConfigured || string.IsNullOrEmpty(_chatSession.ChatFilePath))
            {
                await DisplayAlert("No Chat File", "No chat file is currently open. Start a chat first.", "OK");
                return;
            }

            var currentChatPath = _chatSession.ChatFilePath;
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] RenameMoveChatFile - Current path: '{currentChatPath}'");

            // Check if the current chat file exists on the filesystem (only show option for real files)
            var isRealFile = File.Exists(currentChatPath);
            if (!isRealFile)
            {
                await DisplayAlert("Cannot Rename", "This chat file is stored in a virtual location and cannot be renamed or moved.", "OK");
                return;
            }

            var currentFileName = Path.GetFileName(currentChatPath);
            var currentDirectory = Path.GetDirectoryName(currentChatPath);

            // Show dialog for new filename
            var newFileName = await DisplayPromptAsync(
                "Rename/Move Chat",
                "Enter new file name or path:",
                "Save",
                "Cancel",
                currentFileName
            );

            if (string.IsNullOrEmpty(newFileName))
            {
                System.Diagnostics.Debug.WriteLine("[LLMChatView] User cancelled rename/move");
                return;
            }

            // Determine if user provided a full path or just a filename
            string newPath;
            if (newFileName.Contains('\\') || newFileName.Contains('/'))
            {
                // User provided a full path
                newPath = newFileName;
            }
            else
            {
                // User provided just a filename, use same directory
                newPath = Path.Combine(currentDirectory!, newFileName);
            }

            // Check if target file already exists
            if (File.Exists(newPath))
            {
                var overwrite = await DisplayActionSheet(
                    "File Exists",
                    $"A file named '{Path.GetFileName(newPath)}' already exists.",
                    "Cancel",
                    null,
                    "Overwrite"
                );

                if (overwrite != "Overwrite")
                {
                    System.Diagnostics.Debug.WriteLine("[LLMChatView] User cancelled overwrite");
                    return;
                }
            }

            // Determine if context file should be moved too
            var hasContextFile = !string.IsNullOrEmpty(_chatSession.CurrentFilePath);
            var moveContextFile = false;

            if (hasContextFile)
            {
                var contextFileName = Path.GetFileName(_chatSession.CurrentFilePath);
                var moveContext = await DisplayActionSheet(
                    "Move Context File",
                    $"Also move the context file '{contextFileName}' to the same location?",
                    "No",
                    null,
                    "Yes"
                );

                moveContextFile = moveContext == "Yes";
            }

            // Perform the rename/move operation
            await PerformRenameMove(currentChatPath, newPath, moveContextFile);

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Successfully renamed/moved chat file to: '{newPath}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error renaming/moving chat file: {ex.Message}");
            await DisplayAlert("Error", $"Could not rename/move chat file: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Perform the actual rename/move operation
    /// </summary>
    private async Task PerformRenameMove(string currentChatPath, string newChatPath, bool moveContextFile)
    {
        try
        {
            // Ensure target directory exists
            var targetDirectory = Path.GetDirectoryName(newChatPath);
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Save current session to the old path before moving
            _chatSession.SaveCurrentSession();

            // Move the chat file
            if (File.Exists(currentChatPath))
            {
                File.Move(currentChatPath, newChatPath);
            }

            // Move context file if requested and it exists
            string? newContextPath = null;
            if (moveContextFile && !string.IsNullOrEmpty(_chatSession.CurrentFilePath) && File.Exists(_chatSession.CurrentFilePath))
            {
                var contextFileName = Path.GetFileName(_chatSession.CurrentFilePath);
                newContextPath = Path.Combine(targetDirectory!, contextFileName);

                // Handle case where context file would overwrite existing file
                if (File.Exists(newContextPath))
                {
                    var counter = 1;
                    var contextBaseName = Path.GetFileNameWithoutExtension(contextFileName);
                    var contextExtension = Path.GetExtension(contextFileName);

                    do
                    {
                        var tempName = $"{contextBaseName}_{counter:D2}{contextExtension}";
                        newContextPath = Path.Combine(targetDirectory!, tempName);
                        counter++;
                    }
                    while (File.Exists(newContextPath) && counter < 100);
                }

                File.Move(_chatSession.CurrentFilePath, newContextPath);
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Moved context file to: '{newContextPath}'");
            }

            // Update the chat session with the new path
            if (moveContextFile && !string.IsNullOrEmpty(newContextPath))
            {
                // Update both context file and chat file
                _chatSession.CurrentFilePath = newContextPath;
                _chatSession.SetChatFilePath(newChatPath);

                // Reload context content from new location
                var contextContent = await File.ReadAllTextAsync(newContextPath);
                _chatSession.CurrentFileContent = contextContent;
            }
            else
            {
                // Only update chat file path
                _chatSession.SetChatFilePath(newChatPath);
            }

            // Save the session to the new location
            _chatSession.SaveCurrentSession();

            // Refresh the display to show the new file path
            RefreshMessageDisplay();

            var successMessage = $"Chat file renamed/moved to: {Path.GetFileName(newChatPath)}";
            if (moveContextFile && !string.IsNullOrEmpty(newContextPath))
            {
                successMessage += $"\nContext file moved to: {Path.GetFileName(newContextPath)}";
            }

            await DisplayAlert("Success", successMessage, "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error in PerformRenameMove: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Process the selected chat log file
    /// </summary>
    private async Task ProcessChatLogFile(string filePath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Processing chat file: {filePath}");

            // Read the file content
            var fileContent = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(fileContent))
            {
                await DisplayAlert("Empty File", "The selected file is empty.", "OK");
                return;
            }

            // Parse the chat log
            var chatMessages = ParseChatLog(fileContent);

            if (chatMessages.Count == 0)
            {
                await DisplayAlert("Invalid Format", "No valid chat messages found in the file.", "OK");
                return;
            }

            // Look for related context file
            var contextFile = FindRelatedContextFile(filePath);

            // Clear current chat session and load new messages
            _chatSession.ClearConversation();

            // Set chat file path for saving new messages
            _chatSession.SetChatFilePath(filePath);

            // If context file found, load it
            if (contextFile != null)
            {
                var contextContent = await File.ReadAllTextAsync(contextFile);
                SetFileContext(contextFile, contextContent);
                System.Diagnostics.Debug.WriteLine($"[LLMChatView] Loaded context file: {contextFile}");
            }

            // Load parsed messages into chat session
            foreach (var message in chatMessages)
            {
                if (message.Role == "user")
                {
                    _chatSession.AddUserMessage(message.Content);
                }
                else if (message.Role == "assistant")
                {
                    _chatSession.AddAssistantMessage(message.Content, message.LLMProvider ?? "Unknown");
                }
                else if (message.IsError)
                {
                    _chatSession.AddErrorMessage(message.Content, message.LLMProvider ?? "Unknown");
                }
            }

            // Refresh display
            RefreshMessageDisplay();

            var successMessage = $"Loaded {chatMessages.Count} messages from chat log";
            if (contextFile != null)
            {
                successMessage += $" with context file: {System.IO.Path.GetFileName(contextFile)}";
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] {successMessage}");

            // Focus the input for new messages
            MessageInput.Focus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error processing chat file: {ex.Message}");
            await DisplayAlert("Processing Error", $"Could not process chat file: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Parse chat log content into messages
    /// </summary>
    private static List<LLMChatMessage> ParseChatLog(string content)
    {
        var messages = new List<LLMChatMessage>();

        try
        {
            // Common patterns for chat log formats
            var patterns = new[]
            {
                // Format: "YYYY-MM-DD HH:mm:ss - User: message"
                @"^(?<timestamp>\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2}:\d{2})\s*-\s*(?<role>[^:]+):\s*(?<content>.*)$",
                // Format: "[User] message"
                @"^\[(?<role>[^\]]+)\]\s*(?<content>.*)$",
                // Format: "User: message" (simple)
                @"^(?<role>[^:]+):\s*(?<content>.*)$"
            };

            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            string currentRole = "unknown";
            string currentContent = "";
            DateTime currentTimestamp = DateTime.Now;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                bool matched = false;

                foreach (var pattern in patterns)
                {
                    var match = Regex.Match(trimmedLine, pattern);
                    if (match.Success)
                    {
                        // Save previous message if exists
                        if (!string.IsNullOrEmpty(currentContent.Trim()))
                        {
                            var message = new LLMChatMessage
                            {
                                Role = currentRole.ToLower(),
                                Content = currentContent.Trim(),
                                Timestamp = currentTimestamp,
                                // Use case-insensitive Contains to detect "error" without ToLower allocation
                                ErrorMessage = currentRole.Contains("error", StringComparison.OrdinalIgnoreCase) ? currentContent.Trim() : null
                            };
                            messages.Add(message);
                        }

                        // Start new message
                        currentRole = match.Groups["role"].Value;
                        currentContent = match.Groups["content"].Value;

                        // Try to parse timestamp
                        if (match.Groups.ContainsKey("timestamp") && DateTime.TryParse(match.Groups["timestamp"].Value, out var ts))
                        {
                            currentTimestamp = ts;
                        }
                        else
                        {
                            currentTimestamp = DateTime.Now;
                        }

                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    // Continuation of current message
                    currentContent += "\n" + trimmedLine;
                }
            }

            // Add the last message
            if (!string.IsNullOrEmpty(currentContent.Trim()))
            {
                var message = new LLMChatMessage
                {
                    Role = currentRole.ToLower(),
                    Content = currentContent.Trim(),
                    Timestamp = currentTimestamp,
                    // Use case-insensitive Contains to detect "error" without ToLower allocation
                    ErrorMessage = currentRole.Contains("error", StringComparison.OrdinalIgnoreCase) ? currentContent.Trim() : null
                };
                messages.Add(message);
            }

            // Normalize roles
            foreach (var message in messages)
            {
                if (message.Role.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                    message.Role.Contains("you", StringComparison.OrdinalIgnoreCase) ||
                    message.Role.Contains("human", StringComparison.OrdinalIgnoreCase))
                {
                    message.Role = "user";
                }
                else if (message.Role.Contains("assistant", StringComparison.OrdinalIgnoreCase) ||
                         message.Role.Contains("ai", StringComparison.OrdinalIgnoreCase) ||
                         message.Role.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
                         message.Role.Contains("llm", StringComparison.OrdinalIgnoreCase))
                {
                    message.Role = "assistant";
                }
                else if (message.Role.Contains("system", StringComparison.OrdinalIgnoreCase))
                {
                    message.Role = "system";
                }
            }

            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Parsed {messages.Count} messages from chat log");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error parsing chat log: {ex.Message}");
        }

        return messages;
    }

    /// <summary>
    /// Find a related context file for the chat log
    /// </summary>
    private static string? FindRelatedContextFile(string chatFilePath)
    {
        try
        {
            var chatDir = System.IO.Path.GetDirectoryName(chatFilePath);
            var chatFileName = System.IO.Path.GetFileNameWithoutExtension(chatFilePath);

            // Look for files with similar names
            var possibleNames = new[]
            {
                chatFileName,
                chatFileName.Replace(".chat", ""),
                chatFileName.Replace("_chat", ""),
                chatFileName.Replace("-chat", ""),
                System.IO.Path.GetFileNameWithoutExtension(chatFilePath)
            };

            var possibleExtensions = new[] { ".txt", ".md", ".cs", ".js", ".py", ".java", ".cpp", ".h", ".html", ".css" };

            foreach (var name in possibleNames)
            {
                foreach (var ext in possibleExtensions)
                {
                    var possibleFile = System.IO.Path.Combine(chatDir!, name + ext);
                    if (File.Exists(possibleFile) && possibleFile != chatFilePath)
                    {
                        // Check if it's not a chat file
                        if (!possibleFile.EndsWith(".chat.md", StringComparison.OrdinalIgnoreCase) && !possibleFile.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
                        {
                            return possibleFile;
                        }
                    }
                }
            }

            // Look for any non-chat file in the directory with matching name pattern
            var files = Directory.GetFiles(chatDir!)
                .Where(f => !f.EndsWith(".chat.md", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".chat", StringComparison.OrdinalIgnoreCase))
                .Where(f => System.IO.Path.GetFileNameWithoutExtension(f)
                    .Contains(chatFileName.Replace(".chat", "").Replace("_chat", "").Replace("-chat", ""), StringComparison.OrdinalIgnoreCase))
                .ToList();

            return files.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Error finding context file: {ex.Message}");
            return null;
        }
    }
}
