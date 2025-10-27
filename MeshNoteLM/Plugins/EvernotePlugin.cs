/* 
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Evernote.EDAM.Type;
using Evernote.EDAM.UserStore;
using Evernote.EDAM.NoteStore;
using Evernote.EDAM.Error;
using MeshNoteLM.Interfaces;
using EvernoteSDK.Advanced;

namespace MeshNoteLM.Plugins;

/// <summary>
/// Implementation of IFileSystemPlugin that provides access to Evernote as a file system
/// File system mapping:
/// - Root: Evernote account
/// - Directories: Notebooks
/// - Files: Notes (stored as .enex or .txt files)
/// - Path format: /NotebookName/NoteName.txt
/// </summary>
public partial class EvernoteFileSystemPlugin : IFileSystemPlugin
{
    private readonly ENNoteStoreClient _noteStore;
    private readonly string _authToken;
    private readonly Dictionary<string, Notebook> _notebookCache;
    private readonly Dictionary<string, Note> _noteCache;


    public EvernoteFileSystemPlugin(ENNoteStoreClient noteStore, string authToken)
    {
        _noteStore = noteStore ?? throw new ArgumentNullException(nameof(noteStore));
        _authToken = authToken ?? throw new ArgumentNullException(nameof(authToken));
        _notebookCache = [];
        _noteCache = [];

        RefreshNotebookCache();
    }

    #region File Operations

    /// <summary>
    /// Checks if a note exists at the specified path
    /// Path format: /NotebookName/NoteName.txt
    /// </summary>
    public bool FileExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var (notebookName, noteName) = EvernoteFileSystemPlugin.ParseNotePath(path);
            if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(noteName))
                return false;

            var notebook = GetNotebook(notebookName);
            if (notebook == null)
                return false;

            var note = FindNoteInNotebook(notebook.Guid, noteName);
            return note != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the content of a note as plain text
    /// </summary>
    public string ReadFile(string path)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        var (notebookName, noteName) = EvernoteFileSystemPlugin.ParseNotePath(path);
        if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(noteName))
            throw new FileNotFoundException($"Invalid note path: {path}");

        var notebook = GetNotebook(notebookName) ?? throw new FileNotFoundException($"Notebook not found: {notebookName}");
        var note = FindNoteInNotebook(notebook.Guid, noteName) ?? throw new FileNotFoundException($"Note not found: {path}");
        try
        {
            // Get the full note content
            var fullNote = _noteStore.getNote(_authToken, note.Guid, true, true, false, false);

            // Convert ENML to plain text (simplified conversion)
            return ConvertEnmlToText(fullNote.Content);
        }
        catch (EDAMUserException ex)
        {
            throw new UnauthorizedAccessException($"Access denied: {ex.ErrorCode}");
        }
        catch (EDAMNotFoundException)
        {
            throw new FileNotFoundException($"Note not found: {path}");
        }
        catch (Exception ex)
        {
            throw new IOException($"Error reading note: {path}", ex);
        }
    }

    /// <summary>
    /// Creates or updates a note with the specified content
    /// </summary>
    public void WriteFile(string path, string contents, bool overwrite = true)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        contents ??= string.Empty;

        var (notebookName, noteName) = EvernoteFileSystemPlugin.ParseNotePath(path);
        if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(noteName))
            throw new ArgumentException($"Invalid note path: {path}");

        var notebook = GetNotebook(notebookName);
        if (notebook == null)
        {
            // Create notebook if it doesn't exist
            CreateNotebook(notebookName);
            notebook = GetNotebook(notebookName);
        }

        var existingNote = FindNoteInNotebook(notebook.Guid, noteName);
        if (existingNote != null && !overwrite)
            throw new IOException($"Note already exists and overwrite is disabled: {path}");

        try
        {
            if (existingNote != null)
            {
                // Update existing note
                existingNote.Content = EvernoteFileSystemPlugin.ConvertTextToEnml(contents);
                existingNote.Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _noteStore.updateNote(_authToken, existingNote);
            }
            else
            {
                // Create new note
                var newNote = new Note
                {
                    Title = noteName,
                    Content = EvernoteFileSystemPlugin.ConvertTextToEnml(contents),
                    NotebookGuid = notebook.Guid,
                    Created = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
                _noteStore.createNote(_authToken, newNote);
            }

            RefreshNoteCache();
        }
        catch (EDAMUserException ex)
        {
            throw new UnauthorizedAccessException($"Access denied: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            throw new IOException($"Error writing note: {path}", ex);
        }
    }

    /// <summary>
    /// Appends content to an existing note, or creates it if it doesn't exist
    /// </summary>
    public void AppendToFile(string path, string contents)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        contents ??= string.Empty;

        if (FileExists(path))
        {
            // Read existing content and append
            string existingContent = ReadFile(path);
            WriteFile(path, existingContent + contents, true);
        }
        else
        {
            // Create new note
            WriteFile(path, contents, true);
        }
    }

    /// <summary>
    /// Deletes the specified note
    /// </summary>
    public void DeleteFile(string path)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        var (notebookName, noteName) = EvernoteFileSystemPlugin.ParseNotePath(path);
        if (string.IsNullOrEmpty(notebookName) || string.IsNullOrEmpty(noteName))
            throw new FileNotFoundException($"Invalid note path: {path}");

        var notebook = GetNotebook(notebookName) ?? throw new FileNotFoundException($"Notebook not found: {notebookName}");
        var note = FindNoteInNotebook(notebook.Guid, noteName) ?? throw new FileNotFoundException($"Note not found: {path}");
        try
        {
            _noteStore.deleteNote(_authToken, note.Guid);
            RefreshNoteCache();
        }
        catch (EDAMUserException ex)
        {
            throw new UnauthorizedAccessException($"Access denied: {ex.ErrorCode}");
        }
        catch (EDAMNotFoundException)
        {
            throw new FileNotFoundException($"Note not found: {path}");
        }
        catch (Exception ex)
        {
            throw new IOException($"Error deleting note: {path}", ex);
        }
    }

    #endregion

    #region Directory Operations

    /// <summary>
    /// Checks if a notebook exists
    /// Path format: /NotebookName
    /// </summary>
    public bool DirectoryExists(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        try
        {
            var notebookName = EvernoteFileSystemPlugin.ParseNotebookPath(path);
            if (string.IsNullOrEmpty(notebookName))
                return path == "/" || path == "\\"; // Root always exists

            return GetNotebook(notebookName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a new notebook
    /// </summary>
    public void CreateDirectory(string path)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        var notebookName = EvernoteFileSystemPlugin.ParseNotebookPath(path);
        if (string.IsNullOrEmpty(notebookName))
            return; // Root already exists

        if (GetNotebook(notebookName) != null)
            return; // Notebook already exists

        CreateNotebook(notebookName);
    }

    /// <summary>
    /// Deletes a notebook and optionally all its notes
    /// </summary>
    public void DeleteDirectory(string path, bool recursive = false)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        var notebookName = EvernoteFileSystemPlugin.ParseNotebookPath(path);
        if (string.IsNullOrEmpty(notebookName))
            throw new ArgumentException("Cannot delete root directory");

        var notebook = GetNotebook(notebookName) ?? throw new DirectoryNotFoundException($"Notebook not found: {notebookName}");
        try
        {
            if (!recursive)
            {
                // Check if notebook has notes
                var notes = GetNotesInNotebook(notebook.Guid);
                if (notes.Count != 0)
                    throw new IOException($"Notebook is not empty. Use recursive=true to delete all notes: {notebookName}");
            }

            // Delete all notes first if recursive
            if (recursive)
            {
                var notes = GetNotesInNotebook(notebook.Guid);
                foreach (var note in notes)
                {
                    _noteStore.deleteNote(_authToken, note.Guid);
                }
            }

            // Delete the notebook
            _noteStore.expungeNotebook(_authToken, notebook.Guid);
            RefreshNotebookCache();
            RefreshNoteCache();
        }
        catch (EDAMUserException ex)
        {
            throw new UnauthorizedAccessException($"Access denied: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            throw new IOException($"Error deleting notebook: {notebookName}", ex);
        }
    }

    #endregion

    #region File & Directory Info

    /// <summary>
    /// Gets all notes in the specified notebook
    /// </summary>
    public IEnumerable<string> GetFiles(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        EvernoteFileSystemPlugin.ValidatePath(directoryPath, nameof(directoryPath));

        var notebookName = EvernoteFileSystemPlugin.ParseNotebookPath(directoryPath);
        if (string.IsNullOrEmpty(notebookName))
        {
            // Return all notes from all notebooks
            var allNotes = new List<string>();
            foreach (var nb in _notebookCache.Values)
            {
                var notes = GetNotesInNotebook(nb.Guid);
                allNotes.AddRange(notes.Select(n => $"/{nb.Name}/{n.Title}.txt"));
            }
            return EvernoteFileSystemPlugin.FilterByPattern(allNotes, searchPattern);
        }

        var notebook = GetNotebook(notebookName) ?? throw new DirectoryNotFoundException($"Notebook not found: {notebookName}");
        var notesInNotebook = GetNotesInNotebook(notebook.Guid);
        var notePaths = notesInNotebook.Select(n => $"/{notebookName}/{n.Title}.txt");

        return EvernoteFileSystemPlugin.FilterByPattern(notePaths, searchPattern);
    }

    /// <summary>
    /// Gets all notebooks (subdirectories)
    /// </summary>
    public IEnumerable<string> GetDirectories(string directoryPath, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        EvernoteFileSystemPlugin.ValidatePath(directoryPath, nameof(directoryPath));

        // Only root directory contains notebooks in this implementation
        var notebookName = EvernoteFileSystemPlugin.ParseNotebookPath(directoryPath);
        if (!string.IsNullOrEmpty(notebookName))
            return []; // Notebooks don't contain sub-notebooks

        RefreshNotebookCache();
        var notebookPaths = _notebookCache.Values.Select(nb => $"/{nb.Name}");

        return EvernoteFileSystemPlugin.FilterByPattern(notebookPaths, searchPattern);
    }

    /// <summary>
    /// Gets the size of a note's content in bytes
    /// </summary>
    public long GetFileSize(string path)
    {
        EvernoteFileSystemPlugin.ValidatePath(path, nameof(path));

        string content = ReadFile(path);
        return Encoding.UTF8.GetByteCount(content);
    }

    #endregion

    #region Helper Methods

    private static void ValidatePath(string path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be null or empty", paramName);
    }

    private static (string? notebookName, string? noteName) ParseNotePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return (null, null);

        path = path.Replace('\\', '/').Trim('/');
        var parts = path.Split('/');

        if (parts.Length != 2)
            return (null, null);

        var noteName = parts[1];
        if (noteName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            noteName = noteName[..^4];

        return (parts[0], noteName);
    }

    private static string ParseNotebookPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        path = path.Replace('\\', '/').Trim('/');
        var parts = path.Split('/');

        return parts.Length > 0 ? parts[0] : null;
    }

    private void RefreshNotebookCache()
    {
        try
        {
            var notebooks = _noteStore.listNotebooks(_authToken);
            _notebookCache.Clear();
            foreach (var notebook in notebooks)
            {
                _notebookCache[notebook.Name] = notebook;
            }
        }
        catch (Exception ex)
        {
            throw new IOException("Error refreshing notebook cache", ex);
        }
    }

    private void RefreshNoteCache()
    {
        _noteCache.Clear();
        // Note cache is populated on-demand for performance
    }

    private Notebook GetNotebook(string name)
    {
        return _notebookCache.TryGetValue(name, out var notebook) ? notebook : null;
    }

    private void CreateNotebook(string name)
    {
        try
        {
            var newNotebook = new Notebook { Name = name };
            var createdNotebook = _noteStore.createNotebook(_authToken, newNotebook);
            _notebookCache[name] = createdNotebook;
        }
        catch (Exception ex)
        {
            throw new IOException($"Error creating notebook: {name}", ex);
        }
    }

    private Note FindNoteInNotebook(string notebookGuid, string noteTitle)
    {
        var notes = GetNotesInNotebook(notebookGuid);
        return notes.FirstOrDefault(n => n.Title == noteTitle);
    }

    private List<Note> GetNotesInNotebook(string notebookGuid)
    {
        try
        {
            var filter = new NoteFilter { NotebookGuid = notebookGuid };
            var noteList = _noteStore.findNotes(_authToken, filter, 0, 10000);
            return noteList.Notes;
        }
        catch (Exception ex)
        {
            throw new IOException($"Error getting notes from notebook: {notebookGuid}", ex);
        }
    }

    private static string ConvertEnmlToText(string enml)
    {
        if (string.IsNullOrEmpty(enml))
            return string.Empty;

        // Simple ENML to text conversion - remove XML tags
        // For production use, consider using a proper HTML/XML parser
        var text = MyRegex().Replace(enml, "");
        return System.Net.WebUtility.HtmlDecode(text);
    }

    private static string ConvertTextToEnml(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\"><en-note></en-note>";

        var encodedText = System.Net.WebUtility.HtmlEncode(text).Replace("\n", "<br/>");
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\"><en-note>{encodedText}</en-note>";
    }

    private static IEnumerable<string> FilterByPattern(IEnumerable<string> items, string pattern)
    {
        if (pattern == "*" || string.IsNullOrEmpty(pattern))
            return items;

        // Simple pattern matching - convert * to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return items.Where(item => regex.IsMatch(Path.GetFileName(item)));
    }

    [System.Text.RegularExpressions.GeneratedRegex("<[^>]+>")]
    private static partial System.Text.RegularExpressions.Regex MyRegex();

    #endregion


}

*/
