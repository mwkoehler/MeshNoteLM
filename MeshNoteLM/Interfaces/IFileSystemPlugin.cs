using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeshNoteLM.Interfaces
{
    public interface IFileSystemPlugin : IPlugin
    {
        // File system methods
        bool FileExists(string path);
        string ReadFile(string path);
        byte[] ReadFileBytes(string path);
        void WriteFile(string path, string contents, bool overwrite = true);
        void AppendToFile(string path, string contents);
        void DeleteFile(string path);

        // Directory Operations
        bool DirectoryExists(string path);
        void CreateDirectory(string path);
        void DeleteDirectory(string path, bool recursive = false);

        // File & Directory Info
        IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories);
        IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories);
        IEnumerable<string> GetChildren(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.AllDirectories);
        long GetFileSize(string path);
    }
}
