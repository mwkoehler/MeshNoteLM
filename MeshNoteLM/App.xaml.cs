using MeshNoteLM.Models;
using MeshNoteLM.Services;
using MeshNoteLM.Views;
using MeshNoteLM.Plugins;
using SQLite;

namespace MeshNoteLM
{
    public partial class App : Application
    {
        readonly ContentPage? _mainPage;

        private readonly PluginManager _pluginManager = null!;
        private readonly ViewModels.SourcesTreeViewModel _sourcesTreeViewModel = null!;

        public App(AppInitializer init, PluginManager pluginManager, ViewModels.SourcesTreeViewModel sourcesTreeViewModel)
        {
            System.Diagnostics.Debug.WriteLine("=== App constructor START ===");
            try
            {
                System.Diagnostics.Debug.WriteLine($"init: {init != null}");
                System.Diagnostics.Debug.WriteLine($"pluginManager: {pluginManager != null}");
                System.Diagnostics.Debug.WriteLine($"sourcesTreeViewModel: {sourcesTreeViewModel != null}");

                System.Diagnostics.Debug.WriteLine("About to call App.InitializeComponent()");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("App.InitializeComponent() completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in App constructor (InitializeComponent): {ex}");
                throw;
            }

            try
            {
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                            System.Diagnostics.Debug.WriteLine($"[UnhandledException] {e.ExceptionObject}");

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] {e.Exception}");
                    e.SetObserved();
                };

                _pluginManager = pluginManager!;
                _sourcesTreeViewModel = sourcesTreeViewModel!;
                System.Diagnostics.Debug.WriteLine("App constructor assignments completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in App constructor (assignments): {ex}");
                throw;
            }

            try
            {
                // Dispatcher.UnhandledException += (s, e) =>
                // {
                //                System.Diagnostics.Debug.WriteLine($"[Dispatcher] {e.Exception}");
                //                e.Handled = false; // true if you want to swallow
                // };

#if ANDROID
                Android.Runtime.AndroidEnvironment.UnhandledExceptionRaiser += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[Android Raiser] {e.Exception}");
                    // e.Handled = true; // only if you want to avoid process kill
                };
#endif

                // Create MainPage using code-only version (XAML has compilation issues on Android)
                System.Diagnostics.Debug.WriteLine("About to create MainPageCodeOnly");
                _mainPage = new Views.MainPageCodeOnly(sourcesTreeViewModel!);
                System.Diagnostics.Debug.WriteLine("MainPageCodeOnly created successfully");

                System.Diagnostics.Debug.WriteLine("About to call StartAsync");
                _ = StartAsync(init!);
                System.Diagnostics.Debug.WriteLine("=== App constructor END ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRASH in App constructor (MainPage/StartAsync): {ex}");
                throw;
            }
        }

        private static async Task StartAsync(AppInitializer init)
        {
            await init.InitializeAsync();          // Ensures tables/migrations
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
            await _pluginManager.LoadPluginsAsync();
            _sourcesTreeViewModel.RefreshTree();
            base.OnStart();
        }

        protected override void OnSleep()
        {
            _pluginManager?.Dispose();
            base.OnSleep();
        }
    }

}
