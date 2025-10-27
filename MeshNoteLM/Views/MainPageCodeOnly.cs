namespace MeshNoteLM.Views;

using MeshNoteLM.ViewModels;
using MeshNoteLM.Controls;
using MeshNoteLM.Helpers;
using Microsoft.Maui.Controls;

public partial class MainPageCodeOnly : ContentPage
{
    private readonly ThreePaneLayout _paneHost;
    private readonly Label _editorPlaceholder = null!;
    private readonly Editor _editor = null!;
    private readonly ContentView _viewerContainer = null!;
    private TreeNodeViewModel? _currentNode;
    private readonly LLMChatView _chatView = null!;

    public MainPageCodeOnly(SourcesTreeViewModel vm)
    {
        System.Diagnostics.Debug.WriteLine("=== MainPageCodeOnly constructor START ===");

                BindingContext = vm;

        // Create the three-pane layout
        _paneHost = new ThreePaneLayout();

        // Pane A - Sources Tree View
        var sourcesTreeView = CreateSourcesTreeView();
        _paneHost.PaneA = new Border
        {
            StrokeThickness = 1,
            Content = new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Padding = new Thickness(12),
                    Spacing = 12,
                    Children =
                    {
                        new Label { Text = "Sources", FontAttributes = FontAttributes.Bold },
                        sourcesTreeView
                    }
                }
            }
        };

        // Pane B - Editor/Viewer
        var editorContent = new VerticalStackLayout
        {
            Padding = new Thickness(12),
            Spacing = 12
        };

        var editorLabel = new Label { Text = "Viewer", FontAttributes = FontAttributes.Bold };
        var editorPlaceholder = new Label { Text = "Select a file to view" };
        var editor = new Editor
        {
            IsVisible = false,
            FontFamily = "Courier New",
            FontSize = 14,
            AutoSize = EditorAutoSizeOption.TextChanges
        };

        // Container for document viewers (using ContentView instead of ScrollView to avoid nesting)
        var viewerContainer = new ContentView
        {
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            MinimumHeightRequest = 600,
            MinimumWidthRequest = 400
        };

        editorContent.Children.Add(editorLabel);
        editorContent.Children.Add(editorPlaceholder);
        editorContent.Children.Add(editor);
        editorContent.Children.Add(viewerContainer);

        _paneHost.PaneB = new Border
        {
            StrokeThickness = 1,
            Content = new ScrollView
            {
                Content = editorContent
            }
        };

        // Store references for file loading
        _editorPlaceholder = editorPlaceholder;
        _editor = editor;
        _viewerContainer = viewerContainer;

        // Pane C - LLM Chat
        _chatView = new LLMChatView();
        _paneHost.PaneC = new Border
        {
            StrokeThickness = 1,
            Content = _chatView
        };

        // Set default visibility after all panes are assigned
        _paneHost.VisiblePanels = PanelVisibility.Sources | PanelVisibility.View;

        // Create header with toggle buttons
        var headerStack = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                CreateToggleButton("📁 Sources", "sources"),
                CreateToggleButton("📄 View", "view"),
                CreateToggleButton("💬 Chat", "chat"),
                CreateSettingsButton()
            }
        };

        // Main layout
        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }
            },
            Padding = new Thickness(12),
            Children =
            {
                headerStack,
                _paneHost
            }
        };

        Grid.SetRow(_paneHost, 1);

        System.Diagnostics.Debug.WriteLine("=== MainPageCodeOnly constructor END ===");
    }

    private VerticalStackLayout CreateSourcesTreeView()
    {
        var treeContainer = new VerticalStackLayout
        {
            Spacing = 0
        };

        // Manually build tree when view model nodes change
        var vm = (SourcesTreeViewModel)BindingContext;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SourcesTreeViewModel.Nodes))
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Nodes changed, rebuilding tree. Node count: {vm.Nodes?.Count ?? 0}");
                treeContainer.Children.Clear();
                if (vm.Nodes != null)
                {
                    foreach (var node in vm.Nodes)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Adding root node: {node.Name}");
                        treeContainer.Children.Add(CreateTreeNodeView(node, 0));
                    }
                }
            }
        };

        // Initial load
        if (vm.Nodes != null)
        {
            foreach (var node in vm.Nodes)
            {
                treeContainer.Children.Add(CreateTreeNodeView(node, 0));
            }
        }

        return treeContainer;
    }

    private VerticalStackLayout CreateTreeNodeView(TreeNodeViewModel node, int depth)
    {
        var container = new VerticalStackLayout { Spacing = 0 };

        // Create node header
        var header = new HorizontalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(depth * 20, 2, 0, 2)
        };

        // Expander for directories
        if (node.IsDirectory)
        {
            var expander = new Label
            {
                Text = "▶",
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 20
            };

            var nameLabel = new Label
            {
                Text = node.Name,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            };

            header.Children.Add(expander);
            header.Children.Add(nameLabel);

            // Children container (initially hidden)
            var childrenContainer = new VerticalStackLayout
            {
                Spacing = 0,
                IsVisible = false
            };

            // Toggle expand/collapse on tap
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Tapped node: {node.Name}, IsExpanded: {node.IsExpanded}");

                if (!node.IsExpanded)
                {
                    // Expand
                    expander.Text = "▼";
                    node.IsExpanded = true;

                    // Load children if not already loaded
                    if (childrenContainer.Children.Count == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Loading children for: {node.Name}");
                        var children = await node.EnsureChildrenLoadedAsync();
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Loaded {children.Count} children for: {node.Name}");

                        foreach (var child in children)
                        {
                            childrenContainer.Children.Add(CreateTreeNodeView(child, depth + 1));
                        }
                    }

                    childrenContainer.IsVisible = true;
                }
                else
                {
                    // Collapse
                    expander.Text = "▶";
                    node.IsExpanded = false;
                    childrenContainer.IsVisible = false;
                }
            };

            header.GestureRecognizers.Add(tapGesture);

            container.Children.Add(header);
            container.Children.Add(childrenContainer);
        }
        else
        {
            // File node
            var fileLabel = new Label
            {
                Text = "  " + node.Name,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                Padding = new Thickness(depth * 20 + 20, 2, 0, 2)
            };

            // Add tap gesture to load file
            var fileTapGesture = new TapGestureRecognizer();
            fileTapGesture.Tapped += async (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] File tapped: {node.Name}");
                await LoadFileAsync(node);
            };
            fileLabel.GestureRecognizers.Add(fileTapGesture);

            container.Children.Add(fileLabel);
        }

        return container;
    }

    private Button CreateToggleButton(string text, string panelName)
    {
        var button = new Button
        {
            Text = text,
            Padding = new Thickness(8, 4)
        };

        button.Clicked += (s, e) =>
        {
            switch (panelName.ToLower())
            {
                case "sources":
                    _paneHost.ToggleSources();
                    break;
                case "view":
                    _paneHost.ToggleView();
                    break;
                case "chat":
                    _paneHost.ToggleChat();
                    break;
            }
        };
        return button;
    }

    private Button CreateSettingsButton()
    {
        var button = new Button
        {
            Text = "⚙️ Settings",
            Padding = new Thickness(8, 4)
        };

        button.Clicked += async (s, e) =>
        {
            await Navigation.PushAsync(new SettingsPage());
        };
        return button;
    }

    private async Task LoadFileAsync(TreeNodeViewModel node)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Loading file: {node.FullPath}");

            if (node.Plugin == null)
            {
                System.Diagnostics.Debug.WriteLine("[MainPage] No plugin available for file");
                return;
            }

            _currentNode = node;
            _editorPlaceholder.IsVisible = false;

            // Check if we can view this file type
            if (!FileViewerHelper.CanViewFile(node.Name))
            {
                _editor.IsVisible = false;
                _viewerContainer.IsVisible = false;
                _editorPlaceholder.IsVisible = true;
                _editorPlaceholder.Text = $"No viewer available for file type: {System.IO.Path.GetExtension(node.Name)}";
                return;
            }

            // Read file as bytes for document viewers
            var fileExtension = System.IO.Path.GetExtension(node.Name)?.ToLowerInvariant();
            var isTextFile = fileExtension switch
            {
                ".txt" or ".json" or ".xml" or ".cs" or ".xaml" or ".html" or ".css" or ".js" => true,
                _ => false
            };

            if (isTextFile)
            {
                // Use text editor for text files
                var content = node.Plugin.ReadFile(node.FullPath);
                _editor.IsVisible = true;
                _viewerContainer.IsVisible = false;
                _editor.Text = content;

                // Update chat context with the selected file
                _chatView.SetFileContext(node.FullPath, content);

                System.Diagnostics.Debug.WriteLine($"[MainPage] Loaded {content.Length} characters in editor");
            }
            else
            {
                // Use document viewer for binary files
                var fileData = node.Plugin.ReadFileBytes(node.FullPath);
                var viewer = FileViewerHelper.CreateViewerForFile(node.Name, fileData);

                if (viewer != null)
                {
                    _editor.IsVisible = false;
                    _viewerContainer.IsVisible = true;

                    // Set size properties for the viewer
                    viewer.HorizontalOptions = LayoutOptions.Fill;
                    viewer.VerticalOptions = LayoutOptions.Fill;

                    _viewerContainer.Content = viewer;

                    // For non-text files, set a simplified context for chat
                    _chatView.SetFileContext(node.FullPath, $"[Binary file: {node.Name}]");

                    System.Diagnostics.Debug.WriteLine($"[MainPage] Loaded {fileData.Length} bytes in document viewer");
                    System.Diagnostics.Debug.WriteLine($"[MainPage] Viewer type: {viewer.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[MainPage] ViewerContainer.IsVisible: {_viewerContainer.IsVisible}");
                    System.Diagnostics.Debug.WriteLine($"[MainPage] ViewerContainer.Content: {_viewerContainer.Content?.GetType().Name}");
                }
                else
                {
                    // Fallback to text editor if viewer creation failed
                    var content = node.Plugin.ReadFile(node.FullPath);
                    _editor.IsVisible = true;
                    _viewerContainer.IsVisible = false;
                    _editor.Text = content;
                    _chatView.SetFileContext(node.FullPath, content);
                }
            }

            // Ensure View panel is visible to show the loaded file
            if (!_paneHost.IsViewVisible)
            {
                _paneHost.ToggleView(); // Show View panel
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Error loading file: {ex.Message}");
            await DisplayAlert("Error", $"Could not load file: {ex.Message}", "OK");
        }
    }
}
