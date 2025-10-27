using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshNoteLM.Models;

namespace MeshNoteLM.Interfaces
{
    public interface INoteService
    {
        Task<NoteModel?> GetAsync(int id);
        Task<List<NoteModel>> GetAllAsync();
        Task<int> UpdateAsync(NoteModel n);
        Task<int> DeleteAsync(NoteModel n);
    }
}

