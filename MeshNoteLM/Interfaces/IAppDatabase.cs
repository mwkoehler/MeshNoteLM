using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshNoteLM.Interfaces
{
    public interface IAppDatabase : IDisposable
    {
        SQLiteAsyncConnection Connection { get; }
        Task InitializeAsync();
        Task<int> ClearAllDataAsync();
    }
}
