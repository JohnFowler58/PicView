using System.Diagnostics;

namespace PicView.Core.Navigation;

/// <summary>
/// Manages the history of recently accessed files.
/// </summary>
public static class FileHistory
{
    private const int MaxHistoryEntries = 15;
    private static readonly List<string> Entries = new(MaxHistoryEntries);
    private static string? _fileLocation;

    /// <summary>
    /// Gets the number of entries in the file history
    /// </summary>
    public static int Count => Entries.Count;

    /// <summary>
    /// Gets all history entries
    /// </summary>
    public static IReadOnlyList<string> AllEntries => Entries.AsReadOnly();

    /// <summary>
    /// Gets or sets the current index position in history
    /// </summary>
    public static int CurrentIndex
    {
        get;
        private set => field = Math.Clamp(value, -1, Entries.Count - 1);
    } = -1;

    /// <summary>
    /// Indicates whether there is a previous entry available in history (older entry)
    /// </summary>
    public static bool HasPrevious => CurrentIndex > 0;

    /// <summary>
    /// Indicates whether there is a next entry available in history (newer entry)
    /// </summary>
    public static bool HasNext => CurrentIndex < Entries.Count - 1 && Entries.Count > 0;
    
    /// <summary>
    /// Gets the current entry at the current index
    /// </summary>
    public static string? CurrentEntry => CurrentIndex >= 0 && CurrentIndex < Entries.Count ? Entries[CurrentIndex] : null;

    /// <summary>
    /// Initializes the file history by loading entries from the history file
    /// </summary>
    public static void Initialize()
    {
        _fileLocation = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config/recent.txt");
        try
        {
            if (!File.Exists(_fileLocation))
            {
                var directory = Path.GetDirectoryName(_fileLocation);
                if (directory != null && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                using var fs = File.Create(_fileLocation);
                fs.Seek(0, SeekOrigin.Begin);
            }
        }
        catch (Exception e)
        {
            try
            {
                _fileLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ruben2776/PicView/Config/recent.txt");
            }
            catch (Exception exception)
            {
#if DEBUG
                Trace.WriteLine($"{nameof(FileHistory)} exception, \n{exception.Message}");
#endif
            }
#if DEBUG
            Trace.WriteLine($"{nameof(FileHistory)} exception, \n{e.Message}");
#endif
        }
        LoadFromFile();
        CurrentIndex = Entries.Count > 0 ? Entries.Count - 1 : -1;  // Set to most recent entry
    }

    /// <summary>
    /// Adds an entry to the history
    /// </summary>
    public static void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        // Check if the entry already exists
        var existingIndex = Entries.IndexOf(path);
        
        if (existingIndex >= 0)
        {
            // If entry already exists, just update current index to point to it
            CurrentIndex = existingIndex;
            return;
        }

        // Trim the list if it will exceed the maximum size
        if (Entries.Count >= MaxHistoryEntries)
        {
            // Remove oldest entry (at beginning)
            Entries.RemoveAt(0);
            // Adjust current index since we removed an item
            if (CurrentIndex > 0)
            {
                CurrentIndex--;
            }
        }

        // Add to the end of the list (newest entry)
        Entries.Add(path);
        
        // Set the current index to the newly added item (last position)
        CurrentIndex = Entries.Count - 1;
    }

    /// <summary>
    /// Gets the next entry in history (newer entry)
    /// </summary>
    /// <returns>The next entry in history, or null if there is no next entry</returns>
    public static string? GetNextEntry()
    {
        if (!HasNext)
            return null;
            
        CurrentIndex++;
        return CurrentEntry;
    }

    /// <summary>
    /// Gets the previous entry in history (older entry)
    /// </summary>
    /// <returns>The previous entry in history, or null if there is no previous entry</returns>
    public static string? GetPreviousEntry()
    {
        if (!HasPrevious)
            return null;
            
        CurrentIndex--;
        return CurrentEntry;
    }

    /// <summary>
    /// Gets an entry at the specified index
    /// </summary>
    public static string? GetEntry(int index)
    {
        if (index < 0 || index >= Entries.Count)
            return null;

        return Entries[index];
    }

    /// <summary>
    /// Gets the first entry in history (oldest)
    /// </summary>
    public static string? GetFirstEntry() => Entries.Count > 0 ? Entries[0] : null;

    /// <summary>
    /// Gets the last entry in history (newest)
    /// </summary>
    public static string? GetLastEntry() => Entries.Count > 0 ? Entries[^1] : null;

    /// <summary>
    /// Tries to find an entry that matches or contains the given string
    /// </summary>
    public static string? GetEntryByString(string searchString)
    {
        if (string.IsNullOrWhiteSpace(searchString))
            return null;
        
        // First try exact match
        var exactMatch = Entries.FirstOrDefault(e => 
            string.Equals(e, searchString, StringComparison.OrdinalIgnoreCase));
        
        if (exactMatch != null)
            return exactMatch;

        // Then try contains
        return Entries.FirstOrDefault(e => 
            e.Contains(searchString, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Clears all history entries
    /// </summary>
    public static void Clear()
    {
        Entries.Clear();
        CurrentIndex = -1;
    }

    /// <summary>
    /// Removes a specific entry from history
    /// </summary>
    public static bool Remove(string path)
    {
        var index = Entries.IndexOf(path);
        if (index < 0)
            return false;
            
        Entries.RemoveAt(index);
        
        // Adjust current index if necessary
        if (index <= CurrentIndex)
        {
            CurrentIndex = Math.Max(-1, CurrentIndex - 1);
        }
        
        return true;
    }

    /// <summary>
    /// Removes an entry at the specified index
    /// </summary>
    public static bool RemoveAt(int index)
    {
        if (index < 0 || index >= Entries.Count)
            return false;

        Entries.RemoveAt(index);
        
        // Adjust current index if necessary
        if (index <= CurrentIndex)
        {
            CurrentIndex = Math.Max(-1, CurrentIndex - 1);
        }
        
        return true;
    }
    
    /// <summary>
    /// Renames a file in the history, replacing the old entry with the new one.
    /// </summary>
    /// <param name="oldName">The old name to be replaced.</param>
    /// <param name="newName">The new name that will replace the old one.</param>
    /// <remarks>
    /// This method is case-insensitive and will replace the first entry that matches the old name.
    /// If no matching entry is found, this method does nothing.
    /// </remarks>
    public static void Rename(string oldName, string newName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                return;
            }

            var entry = GetEntryByString(oldName);
            if (string.IsNullOrWhiteSpace(entry) || !Entries.Contains(entry))
            {
                return;
            }

            var index = Entries.IndexOf(entry);
            Entries[index] = newName;
        }
        catch (Exception e)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(FileHistory)}: {nameof(Rename)} exception,\n{e.Message}");
#endif
        }
    }

    /// <summary>
    /// Saves the history to the history file
    /// </summary>
    public static void SaveToFile()
    {
        try
        {
            if (_fileLocation == null)
                return;
                
            // Create directory if it doesn't exist
            var directory = Path.GetDirectoryName(_fileLocation);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write all entries to file
            File.WriteAllLines(_fileLocation, Entries);
        }
        catch (Exception ex)
        {
#if DEBUG
            // Log error but don't throw - this is not critical functionality
            Debug.WriteLine($"Error saving file history: {ex.Message}");
#endif
        }
    }

    /// <summary>
    /// Loads the history from the history file
    /// </summary>
    private static void LoadFromFile()
    {
        try {
            if (_fileLocation == null || !File.Exists(_fileLocation))
            {
                return;
            }

            var lines = File.ReadAllLines(_fileLocation);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line) && Entries.Count < MaxHistoryEntries)
                {
                    Entries.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            // Log error but don't throw - we can start with an empty history
            Debug.WriteLine($"Error loading file history: {ex.Message}");
#endif
        }
    }
}