using Microsoft.Maui.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using MeshNoteLM.Plugins;
using MeshNoteLM.Interfaces;
using MeshNoteLM.Services;

namespace MeshNoteLM.Helpers
{
    public static class FileViewerHelper
    {
        // Office converter - will be injected via setter
        private static IOfficeConverter? _officeConverter;

        /// <summary>
        /// Sets the Office converter instance (called from DI setup)
        /// </summary>
        public static void SetOfficeConverter(IOfficeConverter converter)
        {
            _officeConverter = converter;
        }

        /// <summary>
        /// Checks if a path is a Google Docs URL or if the file is a Google Workspace document
        /// </summary>
        public static bool IsGoogleDocsFile(string path, GoogleDrivePlugin? drivePlugin = null)
        {
            // Check if it's a Google Docs URL using pure logic
            if (FileTypeDetector.IsGoogleDocsUrl(path))
            {
                return true;
            }

            // If a Google Drive plugin is provided, check if the file is a Workspace document
            if (drivePlugin != null)
            {
                try
                {
                    return drivePlugin.IsGoogleWorkspaceFile(path);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        public static bool CanViewFile(string fileName)
        {
            // Delegate to pure logic class
            return FileTypeDetector.CanViewFile(fileName);
        }

        public static View? CreateViewerForFile(string fileName, byte[] fileData)
        {
            try
            {
                // Use pure logic to determine viewer type
                var viewerType = FileTypeDetector.GetViewerType(fileName);

                return viewerType switch
                {
                    FileTypeDetector.ViewerType.MSOffice => CreateOfficeDocumentViewer(fileData, fileName),
                    FileTypeDetector.ViewerType.OpenOffice => CreateOpenOfficeViewer(fileData),
                    FileTypeDetector.ViewerType.Pdf => CreatePdfViewer(fileData),
                    FileTypeDetector.ViewerType.Markdown => CreateMarkdownViewer(fileData),
                    FileTypeDetector.ViewerType.Text => null, // Use default editor
                    _ => new Label { Text = $"No viewer available for {Path.GetExtension(fileName)} files" }
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading file viewer: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        private static View CreateOfficeDocumentViewer(byte[] fileData, string fileName)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Creating Office document viewer for {fileName}");

                // If converter is available, try to convert to PDF
                if (_officeConverter != null && _officeConverter.IsAvailable)
                {
                    System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Office converter is available, attempting conversion");

                    // Create a container for the eventual PDF viewer
                    var container = new ContentView
                    {
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill,
                        Content = new ActivityIndicator
                        {
                            IsRunning = true,
                            VerticalOptions = LayoutOptions.Center,
                            HorizontalOptions = LayoutOptions.Center
                        }
                    };

                    // Convert asynchronously
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var pdfBytes = await _officeConverter.ConvertToPdfAsync(fileData, fileName);
                            if (pdfBytes != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Conversion successful, creating PDF viewer");
                                var pdfViewer = CreatePdfViewer(pdfBytes);
                                container.Content = pdfViewer;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Conversion failed");
                                container.Content = CreateOfficeViewerFallback(fileData, "Conversion failed. " + (_officeConverter?.UnavailableMessage ?? ""));
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Conversion error: {ex.Message}");
                            container.Content = CreateOfficeViewerFallback(fileData, $"Error: {ex.Message}");
                        }
                    });

                    return container;
                }
                else
                {
                    // Converter not available - show sign-in message
                    System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Office converter not available");
                    var message = _officeConverter?.UnavailableMessage ?? "Sign in with Microsoft 365 to view Office documents";
                    return CreateOfficeViewerFallback(fileData, message);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating Office viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading Office document: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        private static VerticalStackLayout CreateOfficeViewerFallback(byte[] fileData, string message)
        {
            var layout = new VerticalStackLayout
            {
                Spacing = 16,
                Padding = new Thickness(20),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label
                    {
                        Text = "Office Document",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = message,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 10)
                    },
                    new Label
                    {
                        Text = $"Document size: {fileData.Length:N0} bytes",
                        FontSize = 12,
                        TextColor = Colors.Gray,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 20)
                    }
                }
            };

            // Add sign-in button if user is not authenticated
            if (_officeConverter != null && !_officeConverter.IsAvailable)
            {
                var signInButton = new Button
                {
                    Text = "Sign in with Microsoft 365",
                    HorizontalOptions = LayoutOptions.Center,
                    BackgroundColor = Color.FromArgb("#0078D4"), // Microsoft blue
                    TextColor = Colors.White,
                    Padding = new Thickness(20, 10),
                    CornerRadius = 4
                };

                signInButton.Clicked += async (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Sign-in button clicked");

                    // Get auth service and sign in
                    var authService = AppServices.Services?.GetService<IMicrosoftAuthService>();
                    if (authService != null)
                    {
                        signInButton.IsEnabled = false;
                        signInButton.Text = "Signing in...";

                        var success = await authService.SignInAsync();

                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Sign-in successful");
                            if (Application.Current!.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
                            {
                                await Application.Current.Windows[0].Page!.DisplayAlert(
                                    "Success",
                                    $"Signed in as {authService.UserDisplayName}\n\nPlease click the document again to convert and view it.",
                                    "OK"
                                );
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[FileViewerHelper] Sign-in failed");
                            signInButton.IsEnabled = true;
                            signInButton.Text = "Sign in with Microsoft 365";

                            if (Application.Current!.Windows.Count > 0 && Application.Current.Windows[0].Page != null)
                            {
                                await Application.Current.Windows[0].Page!.DisplayAlert(
                                    "Sign-in Failed",
                                    "Could not sign in to Microsoft 365. Please check your internet connection and try again.",
                                    "OK"
                                );
                            }
                        }
                    }
                };

                layout.Children.Add(signInButton);
            }

            return layout;
        }

        private static View CreateOpenOfficeViewer(byte[] fileData)
        {
            try
            {
                // Create the simplified OpenOffice viewer using the unified API
                var viewer = new MauiViewer.OpenOffice.Controls.OpenOfficeDocumentViewer();

                // Load the document asynchronously on the UI thread (required for UI controls)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        await viewer.LoadAsync(fileData, "document");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error loading OpenOffice document: {ex}");
                    }
                });

                return viewer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating OpenOffice viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading OpenOffice document: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        private static View CreatePdfViewer(byte[] fileData)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Creating PDF viewer for {fileData.Length} bytes");

                // Create the simplified PDF viewer using the unified API
                var viewer = new MauiViewer.PdfViewer.Controls.PdfDocumentViewer
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    HeightRequest = 600,  // Explicit height for testing
                    WidthRequest = 800    // Explicit width for testing
                };

                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] PDF viewer created, starting async load");

                // Load the document asynchronously on the UI thread (required for UI controls)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Starting PDF LoadAsync");
                        await viewer.LoadAsync(fileData, "document.pdf");
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] PDF LoadAsync completed successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error loading PDF: {ex}");
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Stack trace: {ex.StackTrace}");
                    }
                });

                return viewer;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating PDF viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading PDF: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        private static View CreateMarkdownViewer(byte[] fileData)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Creating Markdown viewer for {fileData.Length} bytes");

                // WORKAROUND: Use a simple WebView directly with MarkdownRenderer
                // The MarkdownDocumentViewer/MarkdownView has layout issues on Windows
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Using direct WebView approach");

                var webView = new WebView
                {
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill,
                    HeightRequest = 600,
                    MinimumHeightRequest = 400,
                    BackgroundColor = Colors.White
                };

                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] WebView created, converting markdown to HTML");

                // Render markdown on the UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        // Convert byte[] to string
                        var markdown = System.Text.Encoding.UTF8.GetString(fileData);
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Markdown text length: {markdown.Length}");

                        // Use Markdig to convert markdown to HTML (simple conversion)
                        var htmlBody = Markdig.Markdown.ToHtml(markdown);

                        // Wrap in a complete HTML document with basic styling
                        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif; padding: 20px; line-height: 1.6; }}
        h1 {{ border-bottom: 2px solid #eaecef; padding-bottom: 8px; }}
        h2 {{ border-bottom: 1px solid #eaecef; padding-bottom: 6px; }}
        code {{ background-color: #f6f8fa; padding: 2px 6px; border-radius: 3px; font-family: 'Courier New', monospace; }}
        pre {{ background-color: #f6f8fa; padding: 16px; border-radius: 6px; overflow-x: auto; }}
        blockquote {{ border-left: 4px solid #dfe2e5; padding-left: 16px; color: #6a737d; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #dfe2e5; padding: 8px; }}
        th {{ background-color: #f6f8fa; }}
    </style>
</head>
<body>
{htmlBody}
</body>
</html>";

                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] HTML generated, length: {html.Length}");
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] HTML preview: {html[..Math.Min(200, html.Length)]}");

                        // Set the HTML on the WebView
                        var htmlSource = new HtmlWebViewSource { Html = html };
                        webView.Source = htmlSource;

                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] HTML set on WebView, should now display");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error rendering markdown: {ex}");
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Stack trace: {ex.StackTrace}");

                        // Show error in the WebView
                        webView.Source = new HtmlWebViewSource
                        {
                            Html = $"<html><body><h1>Error rendering markdown</h1><pre>{ex.Message}</pre></body></html>"
                        };
                    }
                });

                return webView;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating Markdown viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading Markdown: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        /// <summary>
        /// Creates a Google Docs viewer for a file ID or URL
        /// </summary>
        public static View CreateGoogleDocsViewer(string fileIdOrUrl)
        {
            try
            {
                // Note: This will require MauiViewer.GoogleDocs package to be added
                // For now, return a placeholder
                return new Label
                {
                    Text = "Google Docs Viewer\n\n" +
                           "Google Docs viewer integration pending.\n" +
                           "MauiViewer.GoogleDocs package will be added soon.\n\n" +
                           $"Document: {fileIdOrUrl}",
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center
                };

                // TODO: Uncomment when MauiViewer.GoogleDocs package is added
                /*
                var viewer = new MauiViewer.GoogleDocs.Controls.GoogleDocsDocumentViewer();
                viewer.AccessToken = accessToken;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (fileIdOrUrl.Contains("docs.google.com"))
                        {
                            await viewer.LoadFromUrlAsync(fileIdOrUrl);
                        }
                        else
                        {
                            await viewer.LoadFromFileIdAsync(fileIdOrUrl, accessToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error loading Google Docs: {ex}");
                    }
                });

                return viewer;
                */
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating Google Docs viewer: {ex}");
                return new Label
                {
                    Text = $"Error loading Google Docs: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }

        /// <summary>
        /// Creates a viewer for a Google Drive file, detecting if it's a Workspace document
        /// </summary>
        public static View CreateViewerForGoogleDriveFile(string path, GoogleDrivePlugin drivePlugin)
        {
            try
            {
                // Check if it's a Google Workspace document
                if (drivePlugin.IsGoogleWorkspaceFile(path))
                {
                    var fileInfo = drivePlugin.GetFileInfo(path);
                    if (fileInfo != null)
                    {
                        return CreateGoogleDocsViewer(fileInfo.FileId);
                    }
                }

                // Otherwise, treat as a regular file
                var fileData = drivePlugin.ReadFileBytes(path);
                var fileName = Path.GetFileName(path);
                return CreateViewerForFile(fileName, fileData) ?? new Label
                {
                    Text = $"No viewer available for this file type",
                    TextColor = Colors.Gray
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileViewerHelper] Error creating viewer for Google Drive file: {ex}");
                return new Label
                {
                    Text = $"Error loading file: {ex.Message}",
                    TextColor = Colors.Red
                };
            }
        }
    }
}
