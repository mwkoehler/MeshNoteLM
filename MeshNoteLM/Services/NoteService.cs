using MeshNoteLM.Interfaces;
using MeshNoteLM.Models;
// using Microsoft.VisualStudio.Threading;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MeshNoteLM.Services
{
    public class NoteService(IAppDatabase database) : INoteService
    {
        Task<NoteModel?> INoteService.GetAsync(int id) =>
            database.Connection.Table<NoteModel?>().Where(n => n!.Id == id).FirstOrDefaultAsync();

        async Task<List<NoteModel>> INoteService.GetAllAsync()
        {
            var notes = await database.Connection.Table<NoteModel>().ToListAsync();
            return notes.OrderByDescending(m => m.Timestamp).ToList();
        }

        public Task<int> UpdateAsync(NoteModel n) => n.Id != 0 ? database.Connection.UpdateAsync(n) : database.Connection.InsertAsync(n);

        public Task<int> DeleteAsync(NoteModel n) => database.Connection.DeleteAsync(n);
    }
}

