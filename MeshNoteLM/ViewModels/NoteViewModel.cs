using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Graphics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MeshNoteLM.Models;
// using Microsoft.VisualStudio.PlatformUI;
using MeshNoteLM.Helpers;
using ObservableObject = CommunityToolkit.Mvvm.ComponentModel.ObservableObject;
using MeshNoteLM.Interfaces;

namespace MeshNoteLM.ViewModels;

public partial class NoteViewModel : ObservableObject
{
    private readonly INoteService? _noteService;

    public NoteModel? Model { get; internal set; }

    [ObservableProperty]
    public partial string? Text { get; set; }

    public IRelayCommand SaveNoteCommand { get; set;  }

    public NoteViewModel()
    {
        _noteService = ServiceHelper.GetService<INoteService>();
        SaveNoteCommand = new RelayCommand(SaveNote);
    }

    public NoteViewModel(NoteModel noteModel)
    {
        Model = noteModel;
        _noteService = ServiceHelper.GetService<INoteService>();
        SaveNoteCommand = new RelayCommand(SaveNote);
    }

    private void SaveNote()
    {
        if (Model == null) return;
        _ = _noteService!.UpdateAsync(Model);
    }
}
