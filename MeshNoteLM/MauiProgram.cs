using Microsoft.Extensions.Logging;
using MeshNoteLM.Services;
using MeshNoteLM.ViewModels;
using MeshNoteLM.Views;
using MeshNoteLM.Interfaces;
using MeshNoteLM.Plugins;
using CommunityToolkit.Maui;

namespace MeshNoteLM;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        System.Diagnostics.Debug.WriteLine("=== CreateMauiApp START ===");

        AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            System.Diagnostics.Debug.WriteLine("[FirstChance] " + e.Exception);

        System.Diagnostics.Debug.WriteLine("Creating MauiApp builder");
        var builder = MauiApp.CreateBuilder();
        System.Diagnostics.Debug.WriteLine("MauiApp.CreateBuilder completed");

        System.Diagnostics.Debug.WriteLine("Calling UseMauiApp<AppCodeOnly>");
        builder.UseMauiApp<AppCodeOnly>();
        System.Diagnostics.Debug.WriteLine("UseMauiApp completed");

        System.Diagnostics.Debug.WriteLine("Calling UseMauiCommunityToolkit");
        builder.UseMauiCommunityToolkit();
        System.Diagnostics.Debug.WriteLine("UseMauiCommunityToolkit completed");

        System.Diagnostics.Debug.WriteLine("Configuring fonts");
        builder.ConfigureFonts(fonts =>
        {
            fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
        });
        System.Diagnostics.Debug.WriteLine("Fonts configured");

        System.Diagnostics.Debug.WriteLine("Registering PluginManager");
        builder.Services.AddSingleton<PluginManager>();
        System.Diagnostics.Debug.WriteLine("PluginManager registered");

#if DEBUG
        System.Diagnostics.Debug.WriteLine("Adding debug logging");
        builder.Logging.AddDebug();
#endif

        System.Diagnostics.Debug.WriteLine("Registering services");

        // Register file system service first (required by many services)
        builder.Services.AddSingleton<IFileSystemService, MauiFileSystemService>();

        builder.Services.AddSingleton<IAppDatabase, AppDatabase>();
        builder.Services.AddSingleton<INoteService, NoteService>();
        builder.Services.AddSingleton<ISettingsService, SettingsService>();

        // Microsoft Graph services for Office document conversion
        builder.Services.AddSingleton<IMicrosoftAuthService, MicrosoftAuthService>();
        builder.Services.AddSingleton<PdfCacheService>();
        builder.Services.AddSingleton<IOfficeConverter, MicrosoftGraphOfficeConverter>();

        System.Diagnostics.Debug.WriteLine("Registering AppInitializer");
        builder.Services.AddSingleton<AppInitializer>();

        System.Diagnostics.Debug.WriteLine("Registering ViewModels and Pages");
        builder.Services.AddSingleton<SourcesTreeViewModel>();
        builder.Services.AddTransient<NoteViewModel>();
        builder.Services.AddTransient<NoteEditorPage>();
        builder.Services.AddTransient<MainPage>();

        System.Diagnostics.Debug.WriteLine("Building MauiApp");
        MauiApp app;
        try
        {
            app = builder.Build();
            System.Diagnostics.Debug.WriteLine("MauiApp.Build() completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRASH in builder.Build(): {ex}");
            throw;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("Setting AppServices.Services");
            AppServices.Services = app.Services;
            System.Diagnostics.Debug.WriteLine("AppServices.Services set successfully");

            // Inject Office converter into FileViewerHelper
            System.Diagnostics.Debug.WriteLine("Injecting Office converter into FileViewerHelper");
            var officeConverter = app.Services.GetRequiredService<IOfficeConverter>();
            MeshNoteLM.Helpers.FileViewerHelper.SetOfficeConverter(officeConverter);
            System.Diagnostics.Debug.WriteLine("Office converter injected successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CRASH setting AppServices.Services: {ex}");
            throw;
        }

        System.Diagnostics.Debug.WriteLine("=== CreateMauiApp END ===");
        return app;
    }
}


