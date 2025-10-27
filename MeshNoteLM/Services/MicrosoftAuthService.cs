using MeshNoteLM.Interfaces;
using Microsoft.Identity.Client;
using System.Diagnostics;

namespace MeshNoteLM.Services;

/// <summary>
/// Microsoft authentication service using MSAL (Microsoft Authentication Library)
/// </summary>
public class MicrosoftAuthService : IMicrosoftAuthService
{
    // TODO: Replace with your Azure AD app registration Client ID
    // Register your app at: https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade
    private const string ClientId = "YOUR_CLIENT_ID_HERE";

    private static readonly string[] Scopes = new[]
    {
        "Files.ReadWrite",  // Required for OneDrive file operations
        "User.Read"         // Required for user profile
    };

    private IPublicClientApplication? _publicClientApp;
    private IAccount? _currentAccount;
    private string? _userDisplayName;

    public bool IsAuthenticated => _currentAccount != null;

    public string? UserDisplayName => _userDisplayName;

    public async Task<bool> SignInAsync()
    {
        try
        {
            Debug.WriteLine("[MicrosoftAuthService] Starting sign-in...");

            // Initialize MSAL if needed
            if (_publicClientApp == null)
            {
                _publicClientApp = PublicClientApplicationBuilder
                    .Create(ClientId)
                    .WithRedirectUri($"msal{ClientId}://auth")  // Standard MSAL redirect URI
                    .WithAuthority(AzureCloudInstance.AzurePublic, "common")
                    .Build();
            }

            // Try to get token silently first (from cache)
            var accounts = await _publicClientApp.GetAccountsAsync();
            var firstAccount = accounts.FirstOrDefault();

            AuthenticationResult? result = null;

            if (firstAccount != null)
            {
                try
                {
                    result = await _publicClientApp
                        .AcquireTokenSilent(Scopes, firstAccount)
                        .ExecuteAsync();

                    Debug.WriteLine("[MicrosoftAuthService] Silent sign-in successful");
                }
                catch (MsalUiRequiredException)
                {
                    Debug.WriteLine("[MicrosoftAuthService] Silent sign-in failed, prompting user");
                    // Fall through to interactive sign-in
                }
            }

            // If silent sign-in failed, prompt user
            if (result == null)
            {
                result = await _publicClientApp
                    .AcquireTokenInteractive(Scopes)
                    .WithPrompt(Prompt.SelectAccount)
                    .ExecuteAsync();

                Debug.WriteLine("[MicrosoftAuthService] Interactive sign-in successful");
            }

            _currentAccount = result.Account;
            _userDisplayName = result.Account.Username;

            Debug.WriteLine($"[MicrosoftAuthService] Signed in as: {_userDisplayName}");
            return true;
        }
        catch (MsalException ex)
        {
            Debug.WriteLine($"[MicrosoftAuthService] MSAL error: {ex.Message}");
            Debug.WriteLine($"[MicrosoftAuthService] Error code: {ex.ErrorCode}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftAuthService] Sign-in failed: {ex.Message}");
            return false;
        }
    }

    public async Task SignOutAsync()
    {
        try
        {
            if (_publicClientApp != null && _currentAccount != null)
            {
                await _publicClientApp.RemoveAsync(_currentAccount);
                Debug.WriteLine("[MicrosoftAuthService] Signed out successfully");
            }

            _currentAccount = null;
            _userDisplayName = null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftAuthService] Sign-out failed: {ex.Message}");
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        if (_publicClientApp == null || _currentAccount == null)
        {
            Debug.WriteLine("[MicrosoftAuthService] Not authenticated, cannot get token");
            return null;
        }

        try
        {
            var result = await _publicClientApp
                .AcquireTokenSilent(Scopes, _currentAccount)
                .ExecuteAsync();

            return result.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            Debug.WriteLine("[MicrosoftAuthService] Token expired, need to re-authenticate");

            // Try interactive sign-in
            if (await SignInAsync())
            {
                return await GetAccessTokenAsync();
            }

            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MicrosoftAuthService] Failed to get token: {ex.Message}");
            return null;
        }
    }
}
