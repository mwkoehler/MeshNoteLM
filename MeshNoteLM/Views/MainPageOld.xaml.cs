/*
 * 
using System.Data.SqlTypes;
using Microsoft.Maui.Controls;
using System.Data.SqlClient;
using System.Collections.ObjectModel;
using MeshNoteLM.ViewModels;
using MeshNoteLM.Models;
using SQLite;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.Views;

public partial class MainPage : ContentPage
{
    private readonly INoteService? _ns;
    private readonly ObservableCollection<NoteViewModel> _notes = [];


    public MainPage(INoteService ns)
    {
        try
        {
            _ns = ns;
            InitializeComponent();
        }
        catch (Exception)
        {
            throw;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadNotesAsync();
    }

    private async Task LoadNotesAsync()
    {
        await _ns!.GetAllAsync().ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                var notes = t.Result;
                _notes.Clear();
                foreach (var note in notes)
                {
                    _notes.Add(new NoteViewModel(note));
                }
            }
            else
            {
                // Handle error
                System.Diagnostics.Debug.WriteLine($"Error loading notes: {t.Exception}");
            }
        }, TaskScheduler.Current);
    }

    async Task AddNoteAsync(object sender, EventArgs e)
    {
        var newNote = new NoteModel { Id = 0, Timestamp = "TODO", Text = "New Note" };
        await _ns!.UpdateAsync(newNote);
        _notes.Add(new NoteViewModel(newNote));
    }

    async Task DeleteNoteAsync(object sender, EventArgs e)
    {
        if (sender is SwipeItem swipe && swipe.BindingContext is NoteViewModel vm)
        {
            if (vm is null || vm.Model!.Id == 0)
                return;
            _ = await _ns!.DeleteAsync(vm!.Model);
            _notes.Remove(vm);
        }
    }

    async Task OpenNoteAsync(object sender, EventArgs e)
    {
        if (sender is Grid grid && grid.BindingContext is NoteViewModel note)
        {
            await Navigation.PushAsync(new NoteEditorPage(_ns!, note));
        }
    }

    private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
    {

    }

    private void NotesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {

    }

    private void ImageButton_Clicked(object sender, EventArgs e)
    {

    }
}
*/
