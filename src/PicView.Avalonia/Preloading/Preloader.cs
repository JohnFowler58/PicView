using System.Collections.Concurrent;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using PicView.Avalonia.ImageHandling;
using static System.GC;

namespace PicView.Avalonia.Preloading;

/// <summary>
///     The PreLoader class is responsible for preloading images asynchronously and caching them.
/// </summary>
public class PreLoader : IAsyncDisposable
{
#if DEBUG

    // ReSharper disable once ConvertToConstant.Local
    private static readonly bool ShowAddRemove = true;
#endif

    /// <summary>
    ///     Indicates whether the preloader is currently running.
    /// </summary>
    private int _isRunningFlag; // 0 = idle, 1 = running

    private readonly PreLoaderConfig _config = new();

    private readonly Lock _lock = new();

    private readonly ConcurrentDictionary<int, PreLoadValue> _preLoadList = new();

    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    ///     Gets the maximum count of preloaded images.
    /// </summary>
    public static int MaxCount => PreLoaderConfig.MaxCount;

    #region Add

    /// <summary>
    ///     Adds an image to the preload list asynchronously.
    /// </summary>
    /// <param name="index">The index of the image in the list.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>True if the image was added successfully; otherwise, false.</returns>
    public async Task<bool> AddAsync(int index, List<string> list)
    {
        if (list == null || index < 0 || index >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(AddAsync)} invalid parameters: \n{index}");
#endif
            return false;
        }

        if (_preLoadList.ContainsKey(index))
        {
            return false;
        }

        var imageModel = new ImageModel();
        var preLoadValue = new PreLoadValue(imageModel)
        {
            IsLoading = true,
        };

        if (!_preLoadList.TryAdd(index, preLoadValue))
        {
            return false;
        }
        
        try
        {
            var fileInfo = imageModel.FileInfo = new FileInfo(list[index]);
            imageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
            preLoadValue.ImageModel = imageModel;
#if DEBUG
            if (ShowAddRemove)
            {
                Trace.WriteLine($"{imageModel?.FileInfo?.Name} added at {index}");
            }
#endif
            return true;
        }
        catch (Exception ex)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(AddAsync)} exception: \n{ex}");
#endif
            return false;
        }
        finally
        {
            preLoadValue.IsLoading = false;
        }
    }

    public bool Add(int index, List<string> list, ImageModel imageModel)
    {
        if (list == null || index < 0 || index >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Add)} invalid parameters: \n{index}");
#endif
            return false;
        }

        if (imageModel is null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Add)} invalid ImageModel");
#endif
            return false;
        }

        var preLoadValue = new PreLoadValue(imageModel);
        return _preLoadList.TryAdd(index, preLoadValue);
    }

    #endregion

    #region Refresh and resynchronize

    public void RefreshFileInfo(int index, FileInfo fileInfo, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(RefreshFileInfo)} list null \n{index}");
#endif
            return;
        }

        if (index < 0 || index >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(RefreshFileInfo)} invalid index: \n{index}");
#endif
            return;
        }

        var isExisting = _preLoadList.TryGetValue(index, out var value);
        if (!isExisting)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(RefreshFileInfo)} index not found: \n{index}");
#endif
            return;
        }

        value.ImageModel.FileInfo = fileInfo;
    }

    /// <summary>
    ///     Resynchronizes the preload list with the given list of image paths.
    /// </summary>
    /// <param name="list">The list of image paths.</param>
    /// <remarks>
    ///     Call it after the file watcher detects changes, or the list is resorted
    /// </remarks>
    public void Resynchronize(List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Resynchronize)} list is null");
#endif
            return;
        }
        
        if (list.Count == 0 || _preLoadList.IsEmpty)
        {
            return;
        }
        
        _cancellationTokenSource?.Cancel();

        // Create a reverse lookup from file path to current index
        var reverseLookup = new Dictionary<string, int>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            reverseLookup[list[i]] = i;
        }

        // Snapshot of current keys to avoid modification during iteration
        var keys = _preLoadList.Keys.ToArray();

        foreach (var oldIndex in keys)
        {
            if (!_preLoadList.TryGetValue(oldIndex, out var preLoadValue))
            {
                continue;
            }

            var filePath = preLoadValue.ImageModel?.FileInfo?.FullName;
            if (string.IsNullOrEmpty(filePath))
            {
                Remove(oldIndex, list);
                continue;
            }

            if (!reverseLookup.TryGetValue(filePath, out var newIndex))
            {
                // File no longer exists in the list
                Remove(oldIndex, list);
                continue;
            }

            if (newIndex == oldIndex)
            {
                // Index is unchanged, no action needed
                continue;
            }

            if (newIndex < 0 || newIndex >= list.Count)
            {
                // Invalid new index, remove the entry
                Remove(oldIndex, list);
                continue;
            }

            // Attempt to move the entry to the new index
            if (_preLoadList.TryRemove(oldIndex, out var removedValue))
            {
                if (!_preLoadList.TryAdd(newIndex, removedValue))
                {
#if DEBUG
                    if (ShowAddRemove)
                    {
                        Trace.WriteLine($"Failed to resynchronize {filePath} to index {newIndex}");
                    }
#endif
                }
#if DEBUG
                else if (ShowAddRemove)
                {
                    Trace.WriteLine($"Resynchronized {filePath} from index {oldIndex} to {newIndex}");
                }
#endif
            }
        }
    }

    #endregion

    #region Get

    /// <summary>
    ///     Gets the preloaded value for a specific key.
    /// </summary>
    /// <param name="key">The key of the preloaded value.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>The preloaded value if it exists; otherwise, null.</returns>
    public PreLoadValue? Get(int key, List<string> list)
    {
        if (list != null && key >= 0 && key < list.Count)
        {
            return Contains(key, list) ? _preLoadList[key] : null;
        }
#if DEBUG
        Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Get)} invalid parameters: \n{key}");
#endif
        return null;
    }

    /// <summary>
    ///     Gets the preloaded value for a specific file name. Should only be used when resynchronizing.
    /// </summary>
    /// <param name="fileName">The full path of the image file to retrieve the preloaded value for.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>The preloaded value if it exists; otherwise, null.</returns>
    public PreLoadValue? Get(string fileName, List<string> list) =>
        Get(_preLoadList.Values.ToList().FindIndex(x => x.ImageModel.FileInfo.FullName == fileName), list);


    /// <summary>
    ///     Gets the preloaded value for a specific key asynchronously.
    /// </summary>
    /// <param name="key">The key of the preloaded value.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>The preloaded value if it exists; otherwise, null.</returns>
    public async Task<PreLoadValue?> GetAsync(int key, List<string> list)
    {
        if (list == null || key < 0 || key >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(GetAsync)} invalid parameters: \n{key}");
#endif
            return null;
        }

        if (Contains(key, list))
        {
            return _preLoadList[key];
        }

        await AddAsync(key, list);
        return Get(key, list);
    }

    /// <summary>
    ///     Gets the preloaded value for a specific file name asynchronously. Should only be used when resynchronizing.
    /// </summary>
    /// <param name="fileName">The full path of the image file to retrieve the preloaded value for.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>The preloaded value if it exists; otherwise, null.</returns>
    public async Task<PreLoadValue?> GetAsync(string fileName, List<string> list) =>
        await GetAsync(_preLoadList.Values.ToList().FindIndex(x => x.ImageModel?.FileInfo?.FullName == fileName),
            list);

    #endregion

    #region Contains

    /// <summary>
    ///     Checks if a specific key exists in the preload list.
    /// </summary>
    /// <param name="key">The key to check.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>True if the key exists; otherwise, false.</returns>
    public bool Contains(int key, List<string> list)
    {
        return list != null && key >= 0 && key < list.Count && _preLoadList.ContainsKey(key);
    }

    /// <summary>
    ///     Checks if an image with the specified file name exists in the preload list.
    /// </summary>
    /// <param name="fileName">The full path of the image file to check for existence.</param>
    /// <returns>True if the image exists in the preload list; otherwise, false.</returns>
    public bool Contains(string fileName) =>
        _preLoadList.Values.ToList().FindIndex(x => x.ImageModel.FileInfo.FullName == fileName) != -1;

    #endregion

    #region Remove and clear

    /// <summary>
    ///     Removes a specific key from the preload list.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>True if the key was removed; otherwise, false.</returns>
    public bool Remove(int key, List<string> list)
    {
        if (list == null || key < 0 || key >= list.Count)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Remove)} invalid parameters: \n{key}");
#endif
            return false;
        }

        if (!Contains(key, list))
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(Remove)} key does not exist: \n{key}");
#endif
            return false;
        }

        if (!_preLoadList.TryGetValue(key, out var item))
        {
            return false;
        }

        try
        {
            if (item.ImageModel is not null)
            {
                item.ImageModel.FileInfo = null;
            }

            var removed = _preLoadList.TryRemove(key, out _);
#if DEBUG
            if (removed && ShowAddRemove)
            {
                Trace.WriteLine($"{Path.GetFileName(list[key])} removed at {list.IndexOf(list[key])}");
            }
#endif
            return removed;
        }
        catch (Exception e)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(Remove)} exception:\n{e.Message}");
#endif
        }
        finally
        {
            lock (_lock)
            {
                if (item.ImageModel?.Image is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Removes an image from the preload list.
    /// </summary>
    /// <param name="fileName">The full path of the image to remove.</param>
    /// <param name="list">The list of image paths.</param>
    /// <returns>True if the image was successfully removed; otherwise, false.</returns>
    public bool Remove(string fileName, List<string> list)
    {
        var index = _preLoadList.Values.ToList().FindIndex(x => x.ImageModel.FileInfo.FullName == fileName);
        return Remove(index, list);
    }

    /// <summary>
    ///     Clears all preloaded images from the cache.
    /// </summary>
    public void Clear()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        foreach (var item in _preLoadList.Values)
        {
            if (item.ImageModel?.Image is Bitmap img)
            {
                img.Dispose();
            }
        }

        _preLoadList.Clear();
        
#if DEBUG
        if (ShowAddRemove)
        {
            Trace.WriteLine("Preloader cleared");
        }
#endif
    }

    /// <summary>
    ///     Clears all preloaded images from the cache asynchronously.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method is used to clear the preloaded images when the image list changes.
    ///     </para>
    ///     <para>
    ///         It is an asynchronous version of the <see cref="Clear" /> method.
    ///     </para>
    /// </remarks>
    public async Task ClearAsync()
    {
        try
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource?.CancelAsync();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }
        }
        catch (Exception e)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(ClearAsync)} exception: \n{e.StackTrace}");
#endif
        }

        Clear();
    }

    #endregion

    #region Preload

    /// <summary>
    ///     Preloads images asynchronously.
    /// </summary>
    /// <param name="currentIndex">The current index of the image.</param>
    /// <param name="reverse">Indicates whether to preload in reverse order.</param>
    /// <param name="list">The list of image paths.</param>
    public async Task PreLoadAsync(int currentIndex, bool reverse, List<string> list)
    {
        if (list == null)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoader)}.{nameof(PreLoadAsync)} list null \n{currentIndex}");
#endif
            return;
        }

        if (Interlocked.CompareExchange(ref _isRunningFlag, 1, 0) != 0)
        {
            return; // Already running
        }

#if DEBUG
        if (ShowAddRemove)
        {
            Trace.WriteLine($"\nPreLoading started at {currentIndex}\n");
        }
#endif

        _cancellationTokenSource ??= new CancellationTokenSource();

        try
        {
            await PreLoadInternalAsync(currentIndex, reverse, list, _cancellationTokenSource.Token)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
#if DEBUG
            Trace.WriteLine($"{nameof(PreLoadAsync)} exception:\n{exception.Message}");
#endif
        }
        finally
        {
            Interlocked.Exchange(ref _isRunningFlag, 0);
        }
    }

    private async Task PreLoadInternalAsync(int currentIndex, bool reverse, List<string> list,
        CancellationToken token)
    {
        var count = list.Count;

        int nextStartingIndex, prevStartingIndex;
        if (reverse)
        {
            nextStartingIndex = (currentIndex - 1 + count) % count;
            prevStartingIndex = currentIndex + 1;
        }
        else
        {
            nextStartingIndex = (currentIndex + 1) % count;
            prevStartingIndex = currentIndex - 1;
        }

        var isPrettyMuchEmpty = _preLoadList.Count <= 1;
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _config.MaxParallelism,
            CancellationToken = token
        };

        var additions = new List<int>();

        if (reverse)
        {
            await LoopAsync(options, false);
            await LoopAsync(options, true);
        }
        else
        {
            await LoopAsync(options, true);
            await LoopAsync(options, false);
        }

        if (!isPrettyMuchEmpty)
        {
            RemoveLoop();
        }

        return;

        async Task LoopAsync(ParallelOptions parallelOptions, bool positive)
        {
            await Parallel.ForAsync(0, PreLoaderConfig.PositiveIterations, parallelOptions, async (i, _) =>
            {
                token.ThrowIfCancellationRequested();
                var index = positive ? (nextStartingIndex + i) % count : (prevStartingIndex - i + count) % count;
                if (await AddAsync(index, list))
                {
                    additions.Add(index);
                }
            });
        }

        void RemoveLoop()
        {
            // Remove items outside the preload range
            if (list.Count <= PreLoaderConfig.MaxCount + PreLoaderConfig.NegativeIterations ||
                _preLoadList.Count <= PreLoaderConfig.MaxCount)
            {
                return;
            }

            var keysToRemove = _preLoadList.Keys
                .OrderByDescending(k => Math.Abs(k - currentIndex))
                .Take(_preLoadList.Count - PreLoaderConfig.MaxCount);

            foreach (var key in keysToRemove)
            {
                if (!additions.Contains(key))
                {
                    Remove(key, list);
                }
            }
        }
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Clear();
        }

        _disposed = true;
        
#if DEBUG
        if (ShowAddRemove)
        {
            Trace.WriteLine("Preloader disposed");
        }
#endif
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _cancellationTokenSource?.CancelAsync();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        await ClearAsync().ConfigureAwait(false);
        Dispose(false);
        SuppressFinalize(this);
    }

    ~PreLoader()
    {
        Dispose(false);
    }

    #endregion
}