namespace MeshNoteLM.Views;

using MeshNoteLM.ViewModels;

public partial class MainPage : ContentPage
{
    public MainPage(SourcesTreeViewModel vm)
    {
        System.Diagnostics.Debug.WriteLine("=== MainPage constructor START ===");
        System.Diagnostics.Debug.WriteLine($"vm: {vm != null}");

        System.Diagnostics.Debug.WriteLine("Calling InitializeComponent");
        InitializeComponent();
        System.Diagnostics.Debug.WriteLine("InitializeComponent completed");

        BindingContext = vm;
        System.Diagnostics.Debug.WriteLine("=== MainPage constructor END ===");
    }
}
