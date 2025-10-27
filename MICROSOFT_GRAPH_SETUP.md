# Microsoft Graph Setup for Office Document Conversion

This document explains how to set up Microsoft Graph API integration for converting Office documents to PDF.

## Overview

The app uses Microsoft Graph API to convert Office documents (.docx, .xlsx, .pptx) to PDF for inline viewing. This requires:
1. Azure AD app registration
2. User authentication with Microsoft 365 account
3. Internet connection during conversion

## Setup Steps

### 1. Register App in Azure Portal

1. Go to [Azure Portal - App Registrations](https://portal.azure.com/#blade/Microsoft_AAD_RegisteredApps/ApplicationsListBlade)
2. Click **"New registration"**
3. Configure the app:
   - **Name**: `AINotes` (or your preferred name)
   - **Supported account types**: Select **"Accounts in any organizational directory and personal Microsoft accounts"**
   - **Redirect URI**:
     - Platform: **Public client/native (mobile & desktop)**
     - URI: `msalYOUR_CLIENT_ID://auth` (replace after getting Client ID)
4. Click **"Register"**

### 2. Configure API Permissions

1. In your app registration, go to **"API permissions"**
2. Click **"Add a permission"**
3. Select **"Microsoft Graph"**
4. Select **"Delegated permissions"**
5. Add these permissions:
   - `Files.ReadWrite` - Required for uploading/deleting temp files in OneDrive
   - `User.Read` - Required for user profile
6. Click **"Add permissions"**
7. *Optional*: Click **"Grant admin consent"** if you're an admin (simplifies user experience)

### 3. Get Client ID

1. In your app registration, go to **"Overview"**
2. Copy the **"Application (client) ID"**
3. Update the `ClientId` in `Services/MicrosoftAuthService.cs`:

```csharp
private const string ClientId = "YOUR_CLIENT_ID_HERE";  // Replace with your Client ID
```

### 4. Update Redirect URI

1. Now that you have the Client ID, go back to **"Authentication"** in Azure Portal
2. Update the Redirect URI to: `msal{YOUR_CLIENT_ID}://auth`
   - Example: If Client ID is `abc123`, use `msalabc123://auth`
3. Save changes

## How It Works

### Conversion Flow

```
1. User clicks Office document
   ↓
2. App checks if user is authenticated
   ↓
3. If not authenticated → Show "Sign in with Microsoft 365" message
   ↓
4. User signs in (OAuth 2.0 flow)
   ↓
5. App uploads document to OneDrive temp folder
   ↓
6. App requests PDF conversion via Graph API
   ↓
7. App downloads PDF and displays it
   ↓
8. App deletes temp file from OneDrive
```

### Authentication

- Uses **MSAL (Microsoft Authentication Library)**
- **OAuth 2.0** with Azure AD
- Tokens are cached locally
- Silent refresh when tokens expire

### Privacy & Security

- ✅ Files are only temporarily stored in user's own OneDrive
- ✅ Files are immediately deleted after conversion
- ✅ No data is stored on third-party servers
- ✅ App only accesses user's files with explicit permission
- ⚠️ Requires internet connection
- ⚠️ Files briefly exist in Microsoft's cloud during conversion

## Cost

- ✅ **FREE** - Microsoft Graph API does not charge for Office to PDF conversion
- ✅ Included with any Microsoft 365 subscription (Personal, Business, Enterprise)
- ✅ No API metering or usage limits for this feature

## Testing

### Prerequisites
- Microsoft 365 account (Personal, Business, or Enterprise)
- Internet connection
- Office document (.docx, .xlsx, or .pptx)

### Test Steps
1. Build and run the app
2. Navigate to an Office document in the file tree
3. Click the document
4. If not signed in, you'll see: "Sign in with Microsoft 365 to view Office documents"
5. Click Settings → Sign in with Microsoft 365
6. Complete the authentication flow
7. Return to the document - it should now convert to PDF and display inline

## Troubleshooting

### "Sign in with Microsoft 365" message appears
- User needs to sign in first
- Add a "Sign In" button to Settings page (TODO)

### "Conversion failed" error
- Check internet connection
- Verify Azure AD app permissions are granted
- Check Debug logs for detailed error messages

### Authentication fails
- Verify Client ID is correct in `MicrosoftAuthService.cs`
- Verify Redirect URI matches in Azure Portal and code
- Check that API permissions are granted

### Build errors
- Ensure NuGet packages are restored:
  - `Microsoft.Graph` (v5.91.0)
  - `Microsoft.Identity.Client` (v4.69.0)
- Run `dotnet restore`

## Future Enhancements

- [ ] Add "Sign In" button to Settings page
- [ ] Show sign-in status in UI
- [ ] Add "Sign Out" option
- [ ] Cache converted PDFs to avoid re-conversion
- [ ] Support offline conversion (platform-specific)
- [ ] Add progress indicator during conversion
- [ ] Handle large files (>10MB) with chunked upload

## References

- [Microsoft Graph API Documentation](https://learn.microsoft.com/en-us/graph/overview)
- [Convert files to PDF](https://learn.microsoft.com/en-us/graph/api/driveitem-get-content-format)
- [MSAL.NET Documentation](https://learn.microsoft.com/en-us/azure/active-directory/develop/msal-net-overview)
- [Azure AD App Registration](https://learn.microsoft.com/en-us/azure/active-directory/develop/quickstart-register-app)
