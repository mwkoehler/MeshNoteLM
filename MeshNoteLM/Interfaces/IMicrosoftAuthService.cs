namespace MeshNoteLM.Interfaces;

/// <summary>
/// Interface for Microsoft authentication service
/// </summary>
public interface IMicrosoftAuthService
{
    /// <summary>
    /// Gets whether the user is currently authenticated
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Signs in the user interactively
    /// </summary>
    /// <returns>True if sign-in was successful</returns>
    Task<bool> SignInAsync();

    /// <summary>
    /// Signs out the current user
    /// </summary>
    Task SignOutAsync();

    /// <summary>
    /// Gets an access token for Microsoft Graph API
    /// </summary>
    /// <returns>Access token, or null if not authenticated</returns>
    Task<string?> GetAccessTokenAsync();

    /// <summary>
    /// Gets the current user's display name
    /// </summary>
    string? UserDisplayName { get; }
}
