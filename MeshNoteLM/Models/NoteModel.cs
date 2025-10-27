using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Collections.ObjectModel;
using SQLite;

namespace MeshNoteLM.Models
{
    public class NoteModel
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Text { get; set; }
        public string? Timestamp { get; internal set; }
    }
}


