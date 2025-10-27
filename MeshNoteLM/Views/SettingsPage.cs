using MeshNoteLM.Services;
using MeshNoteLM.Plugins;
using MeshNoteLM.ViewModels;
using MeshNoteLM.Interfaces;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MeshNoteLM.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly ISettingsService _settingsService;
        private readonly PluginManager _pluginManager;
        private readonly SourcesTreeViewModel _sourcesTreeViewModel;
        private readonly IMicrosoftAuthService _microsoftAuthService;
        private readonly VerticalStackLayout _mainLayout;
        private readonly VerticalStackLayout _llmProvidersLayout;
        private Label? _microsoftStatusLabel;
        private Button? _microsoftAuthButton;

        private readonly Dictionary<string, (string PropertyName, Func<string?, Task<string>> SaveAction)> _availableLLMProviders = [];

        public SettingsPage()
        {
            _settingsService = MeshNoteLM.Services.AppServices.Services!.GetRequiredService<ISettingsService>();
            _pluginManager = MeshNoteLM.Services.AppServices.Services!.GetRequiredService<PluginManager>();
            _sourcesTreeViewModel = MeshNoteLM.Services.AppServices.Services!.GetRequiredService<SourcesTreeViewModel>();
            _microsoftAuthService = MeshNoteLM.Services.AppServices.Services!.GetRequiredService<IMicrosoftAuthService>();

            Title = "Settings";

            _mainLayout = new VerticalStackLayout
            {
                Padding = new Thickness(10),
                Spacing = 8
            };

            // Microsoft 365 Section
            AddMicrosoft365Section();

            // Obsidian Section
            AddSection("Obsidian Vault",
                _settingsService.ObsidianVaultPath,
                async (value) =>
                {
                    _settingsService.ObsidianVaultPath = value;
                    return await ReloadPluginAsync("Obsidian");
                });

            // Define available LLM providers
            _availableLLMProviders["Claude (Anthropic)"] = ("ClaudeApiKey", (value) => { _settingsService.ClaudeApiKey = value; return ReloadPluginAsync("Claude"); });
            _availableLLMProviders["OpenAI"] = ("OpenAIApiKey", (value) => { _settingsService.OpenAIApiKey = value; return ReloadPluginAsync("OpenAI"); });
            _availableLLMProviders["Google Gemini"] = ("GeminiApiKey", (value) => { _settingsService.GeminiApiKey = value; return ReloadPluginAsync("Gemini"); });
            _availableLLMProviders["Grok (xAI)"] = ("GrokApiKey", (value) => { _settingsService.GrokApiKey = value; return ReloadPluginAsync("Grok"); });
            _availableLLMProviders["Meta (Llama)"] = ("MetaApiKey", (value) => { _settingsService.MetaApiKey = value; return ReloadPluginAsync("Meta"); });
            _availableLLMProviders["Mistral AI"] = ("MistralApiKey", (value) => { _settingsService.MistralApiKey = value; return ReloadPluginAsync("Mistral"); });
            _availableLLMProviders["Perplexity"] = ("PerplexityApiKey", (value) => { _settingsService.PerplexityApiKey = value; return ReloadPluginAsync("Perplexity"); });

            // LLM Providers section header
            var providersHeaderRow = new HorizontalStackLayout
            {
                Spacing = 10,
                Margin = new Thickness(0, 10, 0, 5)
            };

            var providersTitle = new Label
            {
                Text = "Providers",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            };

            var addProviderButton = new Button
            {
                Text = "+",
                WidthRequest = 40,
                HeightRequest = 40,
                FontSize = 20,
                Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center
            };
            addProviderButton.Clicked += OnAddLLMProviderClicked;

            providersHeaderRow.Children.Add(providersTitle);
            providersHeaderRow.Children.Add(addProviderButton);

            _mainLayout.Children.Add(providersHeaderRow);

            // LLM Providers list
            _llmProvidersLayout = new VerticalStackLayout { Spacing = 8 };
            _mainLayout.Children.Add(_llmProvidersLayout);

            // Other services
            if (!string.IsNullOrWhiteSpace(_settingsService.NotionApiKey))
            {
                AddApiKeySection("Notion", _settingsService.NotionApiKey,
                    (value) =>
                    {
                        _settingsService.NotionApiKey = value;
                        return ReloadPluginAsync("Notion");
                    });
            }

            if (!string.IsNullOrWhiteSpace(_settingsService.ReaderApiKey))
            {
                AddApiKeySection("Readwise Reader", _settingsService.ReaderApiKey,
                    (value) =>
                    {
                        _settingsService.ReaderApiKey = value;
                        return ReloadPluginAsync("Reader");
                    });
            }

            // Load existing LLM providers
            RefreshLLMProviders();

            Content = new ScrollView
            {
                Content = _mainLayout
            };
        }

        private void RefreshLLMProviders()
        {
            _llmProvidersLayout.Children.Clear();

            if (!string.IsNullOrWhiteSpace(_settingsService.ClaudeApiKey))
                AddLLMProviderSection("Claude (Anthropic)", _settingsService.ClaudeApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.OpenAIApiKey))
                AddLLMProviderSection("OpenAI", _settingsService.OpenAIApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.GeminiApiKey))
                AddLLMProviderSection("Google Gemini", _settingsService.GeminiApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.GrokApiKey))
                AddLLMProviderSection("Grok (xAI)", _settingsService.GrokApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.MetaApiKey))
                AddLLMProviderSection("Meta (Llama)", _settingsService.MetaApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.MistralApiKey))
                AddLLMProviderSection("Mistral AI", _settingsService.MistralApiKey);

            if (!string.IsNullOrWhiteSpace(_settingsService.PerplexityApiKey))
                AddLLMProviderSection("Perplexity", _settingsService.PerplexityApiKey);
        }

        private void AddLLMProviderSection(string providerName, string apiKey)
        {
            if (!_availableLLMProviders.TryGetValue(providerName, out var providerInfo))
                return;

            var section = new VerticalStackLayout { Spacing = 3, Margin = new Thickness(5, 2, 0, 2) };

            // Header row with status icon and provider name
            var headerRow = new HorizontalStackLayout
            {
                Spacing = 8
            };

            var statusIcon = new Label
            {
                Text = "⏳",
                FontSize = 16,
                VerticalOptions = LayoutOptions.Center
            };

            var providerLabel = new Label
            {
                Text = providerName,
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center
            };

            headerRow.Children.Add(statusIcon);
            headerRow.Children.Add(providerLabel);

            section.Children.Add(headerRow);

            var entry = new Entry
            {
                Text = apiKey,
                Placeholder = "Enter API key...",
                IsPassword = true
            };

            var saveButton = new Button
            {
                Text = "Save",
                WidthRequest = 80
            };

            var removeButton = new Button
            {
                Text = "Remove",
                WidthRequest = 80
            };

            var entryRow = new HorizontalStackLayout
            {
                Spacing = 5,
                HorizontalOptions = LayoutOptions.Fill
            };
            entryRow.Children.Add(entry);
            entry.HorizontalOptions = LayoutOptions.Fill;
            entryRow.Children.Add(saveButton);
            entryRow.Children.Add(removeButton);

            var statusLabel = new Label
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };

            saveButton.Clicked += async (s, e) =>
            {
                statusIcon.Text = "⏳";
                statusLabel.Text = "Testing...";
                statusLabel.TextColor = Colors.Gray;

                var result = await providerInfo.SaveAction(entry.Text?.Trim());

                statusLabel.Text = result;

                if (result.Contains("Invalid") || result.Contains("Error"))
                {
                    statusIcon.Text = "❌";
                    statusLabel.TextColor = Colors.Red;
                }
                else if (result.Contains("Valid") || result.Contains("enabled"))
                {
                    statusIcon.Text = "✓";
                    statusLabel.TextColor = Colors.Green;
                }
                else
                {
                    statusIcon.Text = "⏳";
                }

                _sourcesTreeViewModel.RefreshTree();
            };

            removeButton.Clicked += async (s, e) =>
            {
                await providerInfo.SaveAction(null);
                RefreshLLMProviders();
                _sourcesTreeViewModel.RefreshTree();
            };

            section.Children.Add(entryRow);
            section.Children.Add(statusLabel);

            _llmProvidersLayout.Children.Add(section);

            // Validate on load
            Task.Run(async () =>
            {
                await Task.Delay(100);
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    statusIcon.Text = "⏳";
                    var result = await providerInfo.SaveAction(apiKey);

                    if (result.Contains("Invalid") || result.Contains("Error"))
                    {
                        statusIcon.Text = "❌";
                    }
                    else if (result.Contains("Valid") || result.Contains("enabled"))
                    {
                        statusIcon.Text = "✓";
                    }
                });
            });
        }

        private async void OnAddLLMProviderClicked(object? sender, EventArgs e)
        {
            // Get list of providers not yet added
            var configuredProviders = new HashSet<string>();
            if (!string.IsNullOrWhiteSpace(_settingsService.ClaudeApiKey)) configuredProviders.Add("Claude (Anthropic)");
            if (!string.IsNullOrWhiteSpace(_settingsService.OpenAIApiKey)) configuredProviders.Add("OpenAI");
            if (!string.IsNullOrWhiteSpace(_settingsService.GeminiApiKey)) configuredProviders.Add("Google Gemini");
            if (!string.IsNullOrWhiteSpace(_settingsService.GrokApiKey)) configuredProviders.Add("Grok (xAI)");
            if (!string.IsNullOrWhiteSpace(_settingsService.MetaApiKey)) configuredProviders.Add("Meta (Llama)");
            if (!string.IsNullOrWhiteSpace(_settingsService.MistralApiKey)) configuredProviders.Add("Mistral AI");
            if (!string.IsNullOrWhiteSpace(_settingsService.PerplexityApiKey)) configuredProviders.Add("Perplexity");

            var availableProviders = _availableLLMProviders.Keys.Where(k => !configuredProviders.Contains(k)).ToArray();

            if (availableProviders.Length == 0)
            {
                await DisplayAlert("No Providers", "All available LLM providers have been added.", "OK");
                return;
            }

            var selectedProvider = await DisplayActionSheet("Select LLM Provider", "Cancel", null, availableProviders);

            if (selectedProvider == null || selectedProvider == "Cancel")
                return;

            var apiKey = await DisplayPromptAsync("API Key", $"Enter API key for {selectedProvider}:", placeholder: "Enter API key...");

            if (string.IsNullOrWhiteSpace(apiKey))
                return;

            if (_availableLLMProviders.TryGetValue(selectedProvider, out var providerInfo))
            {
                // Save and validate the new provider
                await providerInfo.SaveAction(apiKey);
                RefreshLLMProviders();
                _sourcesTreeViewModel.RefreshTree();
            }
        }

        private void AddSection(string title, string? initialValue, Func<string?, Task<string>> onSave)
        {
            var section = new VerticalStackLayout { Spacing = 3 };

            section.Children.Add(new Label
            {
                Text = title,
                FontSize = 12
            });

            var entry = new Entry
            {
                Text = initialValue ?? string.Empty,
                Placeholder = "Enter path..."
            };

            var saveButton = new Button
            {
                Text = "Save",
                WidthRequest = 80
            };

            var entryRow = new HorizontalStackLayout
            {
                Spacing = 5,
                HorizontalOptions = LayoutOptions.Fill
            };
            entryRow.Children.Add(entry);
            entry.HorizontalOptions = LayoutOptions.Fill;
            entryRow.Children.Add(saveButton);

            var statusLabel = new Label
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };

            saveButton.Clicked += async (s, e) =>
            {
                statusLabel.Text = "Saving...";
                statusLabel.TextColor = Colors.Gray;

                var result = await onSave(entry.Text?.Trim());

                statusLabel.Text = result;
                statusLabel.TextColor = result.Contains("Invalid") || result.Contains("Error")
                    ? Colors.Red
                    : Colors.Green;

                _sourcesTreeViewModel.RefreshTree();
            };

            section.Children.Add(entryRow);
            section.Children.Add(statusLabel);

            _mainLayout.Children.Add(section);
        }

        private void AddApiKeySection(string pluginName, string? initialValue, Func<string?, Task<string>> onSave)
        {
            var section = new VerticalStackLayout { Spacing = 3, Margin = new Thickness(5, 2, 0, 2) };

            section.Children.Add(new Label
            {
                Text = pluginName,
                FontSize = 12
            });

            var entry = new Entry
            {
                Text = initialValue ?? string.Empty,
                Placeholder = "Enter API key...",
                IsPassword = true
            };

            var saveButton = new Button
            {
                Text = "Save",
                WidthRequest = 80
            };

            var entryRow = new HorizontalStackLayout
            {
                Spacing = 5,
                HorizontalOptions = LayoutOptions.Fill
            };
            entryRow.Children.Add(entry);
            entry.HorizontalOptions = LayoutOptions.Fill;
            entryRow.Children.Add(saveButton);

            var statusLabel = new Label
            {
                FontSize = 11,
                Margin = new Thickness(0, 2, 0, 0)
            };

            saveButton.Clicked += async (s, e) =>
            {
                statusLabel.Text = "Testing...";
                statusLabel.TextColor = Colors.Gray;

                var result = await onSave(entry.Text?.Trim());

                statusLabel.Text = result;
                statusLabel.TextColor = result.Contains("Invalid") || result.Contains("Error")
                    ? Colors.Red
                    : Colors.Green;

                _sourcesTreeViewModel.RefreshTree();
            };

            section.Children.Add(entryRow);
            section.Children.Add(statusLabel);

            _mainLayout.Children.Add(section);
        }

        private async Task<string> ReloadPluginAsync(string pluginName)
        {
            try
            {
                await _pluginManager.ReloadPluginAsync(pluginName);

                var plugin = _pluginManager.GetAllPlugins().FirstOrDefault(p => p.Name == pluginName);
                if (plugin == null)
                {
                    return $"Plugin '{pluginName}' not found";
                }

                // Test the actual connection
                var (success, message) = await plugin.TestConnectionAsync();

                if (!success)
                {
                    plugin.IsEnabled = false;
                }

                return message;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private void AddMicrosoft365Section()
        {
            var sectionLayout = new VerticalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Header
            var headerLabel = new Label
            {
                Text = "Microsoft 365",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            };
            sectionLayout.Children.Add(headerLabel);

            // Description
            var descriptionLabel = new Label
            {
                Text = "Sign in to convert Office documents to PDF for viewing",
                FontSize = 12,
                TextColor = Colors.Gray,
                Margin = new Thickness(0, 0, 0, 8)
            };
            sectionLayout.Children.Add(descriptionLabel);

            // Status label
            _microsoftStatusLabel = new Label
            {
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 8)
            };
            sectionLayout.Children.Add(_microsoftStatusLabel);

            // Auth button
            _microsoftAuthButton = new Button
            {
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(16, 8),
                BackgroundColor = Color.FromArgb("#0078D4"), // Microsoft blue
                TextColor = Colors.White,
                CornerRadius = 4
            };
            _microsoftAuthButton.Clicked += (sender, e) => OnMicrosoftAuthButtonClicked(sender!, e);
            sectionLayout.Children.Add(_microsoftAuthButton);

            // Initialize UI state
            UpdateMicrosoftAuthUI();

            _mainLayout.Children.Add(sectionLayout);
        }

        private void UpdateMicrosoftAuthUI()
        {
            if (_microsoftAuthService.IsAuthenticated)
            {
                _microsoftStatusLabel!.Text = $"✓ Signed in as: {_microsoftAuthService.UserDisplayName ?? "User"}";
                _microsoftStatusLabel.TextColor = Colors.Green;
                _microsoftAuthButton!.Text = "Sign Out";
                _microsoftAuthButton.BackgroundColor = Colors.Gray;
            }
            else
            {
                _microsoftStatusLabel!.Text = "Not signed in";
                _microsoftStatusLabel.TextColor = Colors.Orange;
                _microsoftAuthButton!.Text = "Sign in with Microsoft 365";
                _microsoftAuthButton.BackgroundColor = Color.FromArgb("#0078D4");
            }
        }

        private async Task OnMicrosoftAuthButtonClicked(object sender, EventArgs e)
        {
            _microsoftAuthButton!.IsEnabled = false;

            if (_microsoftAuthService.IsAuthenticated)
            {
                // Sign out
                await _microsoftAuthService.SignOutAsync();
                await Application.Current!.Windows[0].Page!.DisplayAlert("Success", "Signed out successfully", "OK");
            }
            else
            {
                // Sign in
                _microsoftAuthButton.Text = "Signing in...";
                var success = await _microsoftAuthService.SignInAsync();

                if (success)
                {
                    await Application.Current!.Windows[0].Page!.DisplayAlert(
                        "Success",
                        $"Signed in as {_microsoftAuthService.UserDisplayName}\n\nYou can now view Office documents inline.",
                        "OK"
                    );
                }
                else
                {
                    await Application.Current!.Windows[0].Page!.DisplayAlert(
                        "Sign-in Failed",
                        "Could not sign in to Microsoft 365. Please check:\n\n" +
                        "1. You have a Microsoft 365 account\n" +
                        "2. Internet connection is available\n" +
                        "3. Azure AD app is configured (see MICROSOFT_GRAPH_SETUP.md)",
                        "OK"
                    );
                }
            }

            UpdateMicrosoftAuthUI();
            _microsoftAuthButton.IsEnabled = true;
        }
    }
}
