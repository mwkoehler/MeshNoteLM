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
    private AIProviderPluginBase? _selectedLLM;

    public LLMChatView()
    {
        InitializeComponent();

        _chatSession = new LLMChatSession();
        _pluginManager = AppServices.Services?.GetService<PluginManager>()
            ?? throw new InvalidOperationException("PluginManager not available");

        // Subscribe to message collection changes
        _chatSession.Messages.CollectionChanged += (s, e) => RefreshMessageDisplay();

        // Hook into Loaded event to ensure plugins are available
        this.Loaded += (s, e) => SelectDefaultLLM();
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
    /// Select the default LLM (first enabled AI provider)
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

        _selectedLLM = enabledProviders.FirstOrDefault();
        if (_selectedLLM != null)
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] Selected default LLM: {_selectedLLM.Name}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[LLMChatView] No enabled LLM providers available");
        }

        UpdateLLMButtonText();
    }

    /// <summary>
    /// Update the LLM selector button text
    /// </summary>
    private void UpdateLLMButtonText()
    {
        if (_selectedLLM != null)
        {
            LLMSelectorButton.Text = $"LLM: {_selectedLLM.Name}";
        }
        else
        {
            LLMSelectorButton.Text = "No LLM Available";
            LLMSelectorButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// Handle LLM selector button click
    /// </summary>
    private async void OnLLMSelectorClicked(object sender, EventArgs e)
    {
        // Refresh plugin list in case they were loaded after construction
        var aiProviders = _pluginManager.GetAllPlugins()
            .OfType<AIProviderPluginBase>()
            .Where(p => p.IsEnabled)
            .ToList();

        if (aiProviders.Count == 0)
        {
            await DisplayAlert("No LLMs Available", "Please enable at least one LLM provider in Settings.\n\nMake sure you have:\n1. Added an API key for at least one LLM provider\n2. The provider shows as enabled in Settings", "OK");
            return;
        }

        // If no LLM was previously selected, select the first one
        if (_selectedLLM == null && aiProviders.Count > 0)
        {
            _selectedLLM = aiProviders.First();
            UpdateLLMButtonText();
        }

        var providerNames = aiProviders.Select(p => p.Name).ToArray();
        var selected = await DisplayActionSheet("Select LLM Provider", "Cancel", null, providerNames);

        if (selected != null && selected != "Cancel")
        {
            _selectedLLM = aiProviders.FirstOrDefault(p => p.Name == selected);
            UpdateLLMButtonText();
        }
    }

    /// <summary>
    /// Handle send button click
    /// </summary>
    private async void OnSendClicked(object sender, EventArgs e)
    {
        var userMessage = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(userMessage))
            return;

        // Check if we have a file selected
        if (string.IsNullOrEmpty(_chatSession.CurrentFilePath))
        {
            var savePath = await PromptForSaveLocation();
            if (string.IsNullOrEmpty(savePath))
                return;

            _chatSession.CurrentFilePath = savePath;
        }

        // Check if we have an LLM selected
        if (_selectedLLM == null)
        {
            await DisplayAlert("No LLM Selected", "Please select an LLM provider first.", "OK");
            return;
        }

        // Clear input and add user message
        MessageInput.Text = string.Empty;
        _chatSession.AddUserMessage(userMessage);

        // Send to LLM
        await SendMessageToLLM(userMessage);
    }

    /// <summary>
    /// Send message to the selected LLM
    /// </summary>
    private async Task SendMessageToLLM(string userMessage)
    {
        if (_selectedLLM == null)
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

            // Send message to LLM
            var response = await _selectedLLM.SendChatMessageAsync(history, userMessage);

            _chatSession.AddAssistantMessage(response, _selectedLLM.Name);
        }
        catch (Exception ex)
        {
            _chatSession.AddErrorMessage(ex.Message, _selectedLLM.Name);
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
            .Where(p => p.IsEnabled && p != _selectedLLM)
            .ToList();

        var options = new List<string> { "Retry with same LLM" };

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

        if (action == "Retry with same LLM")
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
            _selectedLLM = otherLLMs.FirstOrDefault(llm => llm.Name == llmName);
            UpdateLLMButtonText();

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
        var result = await DisplayPromptAsync(
            "Save Location",
            "Enter the file path where the chat should be saved:",
            "Save",
            "Cancel",
            "chat_session.txt"
        );

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
            header.Text = $"You - {message.Timestamp:HH:mm:ss}";
            border.BackgroundColor = Color.FromArgb("#E3F2FD");
        }
        else if (message.IsError)
        {
            header.Text = $"{message.LLMProvider} (Error) - {message.Timestamp:HH:mm:ss}";
            border.BackgroundColor = Color.FromArgb("#FFEBEE");
        }
        else
        {
            header.Text = $"{message.LLMProvider} - {message.Timestamp:HH:mm:ss}";
            border.BackgroundColor = Color.FromArgb("#F1F8E9");
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
