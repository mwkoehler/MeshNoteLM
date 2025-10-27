using MeshNoteLM.Interfaces;
using System.Diagnostics;

namespace MeshNoteLM.Services;

/// <summary>
/// Converts Office documents to PDF using Microsoft Graph API
/// </summary>
public class MicrosoftGraphOfficeConverter(IMicrosoftAuthService authService, PdfCacheService cacheService) : IOfficeConverter
{
    private readonly IMicrosoftAuthService _authService = authService;
    private readonly PdfCacheService _cacheService = cacheService;

    public bool IsAvailable => _authService.IsAuthenticated;

    public string UnavailableMessage =>
        "Sign in with Microsoft 365 to view Office documents, or open in external app";

    public async Task<byte[]?> ConvertToPdfAsync(byte[] dataUnused, string fileName)
    {
        if (!IsAvailable)
        {
            Debug.WriteLine("[MicrosoftGraphOfficeConverter] User not authenticated");
            return null;
        }

        // Check cache first
        var cachedPdf = _cacheService.GetCachedPdf(fileName);
        if (cachedPdf != null)
        {
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Using cached PDF for {fileName}");
            return cachedPdf;
        }

        try
        {
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Converting {fileName} to PDF)");

            // Get access token for each request
            var accessToken = await _authService.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(accessToken))
            {
                Debug.WriteLine("[MicrosoftGraphOfficeConverter] Failed to get access token");
                return null;
            }

            // Use HttpClient with Graph API directly (simpler than SDK for this use case)
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // 1. Upload file to OneDrive temp folder
            var tempFileName = $"temp_{Guid.NewGuid()}_{fileName}";
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Uploading as {tempFileName}");

            var uploadUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{tempFileName}:/content";
            var uploadResponse = await httpClient.PutAsync(uploadUrl, new ByteArrayContent([0]));

            if (!uploadResponse.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Upload failed: {uploadResponse.StatusCode}");
                var error = await uploadResponse.Content.ReadAsStringAsync();
                Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Error: {error}");
                return null;
            }

            var uploadResult = await uploadResponse.Content.ReadAsStringAsync();
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Upload response: {uploadResult[..Math.Min(200, uploadResult.Length)]}");

            // Parse the item ID from the response
            var uploadedItem = System.Text.Json.JsonDocument.Parse(uploadResult);
            var itemId = uploadedItem.RootElement.GetProperty("id").GetString();

            if (string.IsNullOrEmpty(itemId))
            {
                Debug.WriteLine("[MicrosoftGraphOfficeConverter] Failed to get item ID");
                return null;
            }

            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] File uploaded, ID: {itemId}");

            try
            {
                // 2. Request PDF conversion - wait a moment for OneDrive to process the file
                await Task.Delay(1000);

                Debug.WriteLine("[MicrosoftGraphOfficeConverter] Requesting PDF conversion");
                var pdfUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{itemId}/content?format=pdf";
                var pdfResponse = await httpClient.GetAsync(pdfUrl);

                if (!pdfResponse.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[MicrosoftGraphOfficeConverter] PDF conversion failed: {pdfResponse.StatusCode}");
                    var error = await pdfResponse.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Error: {error}");
                    return null;
                }

                // 3. Read PDF bytes
                var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();

                Debug.WriteLine($"[MicrosoftGraphOfficeConverter] PDF generated successfully ({pdfBytes.Length} bytes)");

                // Cache the result
                _cacheService.CachePdf(fileName, pdfBytes);

                return pdfBytes;
            }
            finally
            {
                // 4. Always cleanup temp file
                try
                {
                    Debug.WriteLine("[MicrosoftGraphOfficeConverter] Cleaning up temp file");
                    var deleteUrl = $"https://graph.microsoft.com/v1.0/me/drive/items/{itemId}";
                    await httpClient.DeleteAsync(deleteUrl);
                }
                catch (Exception cleanupEx)
                {
                    Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Cleanup failed: {cleanupEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Conversion failed: {ex.Message}");
            Debug.WriteLine($"[MicrosoftGraphOfficeConverter] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Determines if a file is an Office document based on file data and extension
    /// </summary>
    public static bool IsOfficeDocument(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        var extension = GetFileExtension(fileName).ToLowerInvariant();
        return HasOfficeExtension(extension);
    }

    /// <summary>
    /// Checks if a file extension is an Office extension
    /// </summary>
    public static bool HasOfficeExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension))
            return false;

        return extension.ToLowerInvariant() switch
        {
            ".doc" or ".docx" or ".dot" or ".dotx" => true,
            ".xls" or ".xlsx" or ".xlt" or ".xltx" => true,
            ".ppt" or ".pptx" or ".pot" or ".potx" => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the file extension from a file name
    /// </summary>
    public static string GetFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return string.Empty;

        var lastDot = fileName.LastIndexOf('.');
        return lastDot >= 0 ? fileName[lastDot..] : string.Empty;
    }

    /// <summary>
    /// Gets the size of a byte array
    /// </summary>
    public static int GetFileSize(byte[]? fileData) => fileData?.Length ?? 0;

    /// <summary>
    /// Formats file size into human-readable format
    /// </summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} bytes";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes < 1024L * 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F1} TB";
    }
}
