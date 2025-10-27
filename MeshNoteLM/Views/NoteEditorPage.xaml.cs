using MeshNoteLM.ViewModels;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using SQLite;
using MeshNoteLM.Models;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Views;
// NoteEditorPage.xaml.cs
public partial class NoteEditorPage : ContentPage
{
    private readonly NoteViewModel _vm;
    private readonly INoteService _ns;

    public NoteEditorPage(INoteService ns, NoteViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        _ns = ns;
        BindingContext = viewModel;
        NoteText = _vm.Text ?? "";
    }

    public NoteEditorPage() : this(
        Application.Current?.Handler?.MauiContext?.Services!.GetRequiredService<INoteService>()!,
        Application.Current?.Handler?.MauiContext?.Services!.GetRequiredService<NoteViewModel>()!)
    { }

    public string NoteText { get; set; }

    async Task SaveNoteAsync(object sender, EventArgs e)
    {
        await _ns.UpdateAsync(_vm!.Model!);
    }
}
