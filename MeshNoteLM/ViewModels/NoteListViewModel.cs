using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MeshNoteLM.ViewModels
{
    public partial class NoteListViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<NoteViewModel> Notes { get; set; }
    }
}
