using MeshNoteLM.Models;
using MeshNoteLM.Services;
using MeshNoteLM.Views;
using MeshNoteLM.Plugins;

namespace MeshNoteLM
{
    public class AppCodeOnly : Application
    {
        readonly ContentPage? _mainPage;

        private readonly PluginManager _pluginManager = null!;
        private readonly ViewModels.SourcesTreeViewModel _sourcesTreeViewModel = null!;

        public AppCodeOnly(AppInitializer init, PluginManager pluginManager, ViewModels.SourcesTreeViewModel sourcesTreeViewModel)
        {
            System.Diagnostics.Debug.WriteLine("=== AppCodeOnly constructor START ===");
            try
            {
                System.Diagnostics.Debug.WriteLine($"init: {init != null}");
                System.Diagnostics.Debug.WriteLine($"pluginManager: {pluginManager != null}");
                System.Diagnostics.Debug.WriteLine($"sourcesTreeViewModel: {sourcesTreeViewModel != null}");

                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                            System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.ExceptionObject}");

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
                    e.SetObserved();
                };

                _pluginManager = pluginManager!;
                _sourcesTreeViewModel = sourcesTreeViewModel!;
                System.Diagnostics.Debug.WriteLine("AppCodeOnly constructor assignments completed");

#if ANDROID
                Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Android Raiser] {e.Exception}");
                };
#endif

                // Create MainPage using code-only version (XAML has compilation issues on Android)
                System.Diagnostics.Debug.WriteLine("About to create MainPageCodeOnly");
                _mainPage = new Views.MainPageCodeOnly(sourcesTreeViewModel!);
                System.Diagnostics.Debug.WriteLine("MainPageCodeOnly created successfully");

                System.Diagnostics.Debug.WriteLine("About to call StartAsync");
                _ = StartAsync(init!);
                System.Diagnostics.Debug.WriteLine("=== AppCodeOnly constructor END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in AppCodeOnly constructor: {ex}");
                throw;
            }
        }

        private static async Task StartAsync(AppInitializer init)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("StartAsync: Initializing database");
                await init.InitializeAsync();
                System.Diagnostics.Debug.WriteLine("StartAsync: Database initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in StartAsync: {ex}");
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell
            {
                CurrentItem = new ShellContent
                {
                    Content = _mainPage
                }
            });
        }

        protected override async void OnStart()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== OnStart BEGIN ===");
                await _pluginManager.LoadPluginsAsync();
                _sourcesTreeViewModel.RefreshTree();
                System.Diagnostics.Debug.WriteLine("=== OnStart END ===");
                base.OnStart();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in OnStart: {ex}");
            }
        }

        protected override void OnSleep()
        {
            _pluginManager?.Dispose();
            base.OnSleep();
        }
    }
}
