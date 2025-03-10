using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using PicView.Avalonia.Clipboard;
using PicView.Avalonia.Crop;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.Input;
using PicView.Avalonia.Preloading;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.ArchiveHandling;
using PicView.Core.FileHandling;
using PicView.Core.Gallery;
using PicView.Core.Http;
using PicView.Core.ImageDecoding;
using PicView.Core.Localization;
using PicView.Core.Navigation;

namespace PicView.Avalonia.Navigation;

/// <summary>
///     Manages image navigation within the application.
/// </summary>
public static class NavigationManager
{
    private static CancellationTokenSource? _cancellationTokenSource;

    public static TiffManager.TiffNavigationInfo? TiffNavigationInfo { get; private set; }

    // Should be updated to handle multiple iterators, in the future when adding tab support
    private static ImageIterator? _imageIterator;

    #region Navigation

    /// <summary>
    ///     Determines whether navigation is possible based on the current state of the <see cref="MainViewModel" />.
    /// </summary>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>True if navigation is possible, otherwise false.</returns>
    public static bool CanNavigate(MainViewModel vm)
    {
        return _imageIterator?.ImagePaths is not null &&
               _imageIterator.ImagePaths.Count > 0 && !CropFunctions.IsCropping &&
               !DialogManager.IsDialogOpen && vm is { IsEditableTitlebarOpen: false, FileInfo: not null };
        // TODO: should probably turn this into CanExecute observable for ReactiveUI
    }

    /// <summary>
    ///     Navigates to the next or previous image based on the <paramref name="next" /> parameter.
    /// </summary>
    /// <param name="next">True to navigate to the next image, false for the previous image.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Navigate(bool next, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            await ScrollGallery(next);
            return;
        }
        
        if (_imageIterator.CurrentIndex < 0 || _imageIterator.CurrentIndex >= _imageIterator.ImagePaths.Count)
        {
            ErrorHandling.ShowStartUpMenu(vm);
            return;
        }

        var navigateTo = next ? NavigateTo.Next : NavigateTo.Previous;
        var nextIteration = _imageIterator.GetIteration(_imageIterator.CurrentIndex, navigateTo);
        var currentFileName = _imageIterator.ImagePaths[_imageIterator.CurrentIndex];
        if (TiffManager.IsTiff(currentFileName))
        {
            await TiffNavigation(vm, currentFileName, nextIteration).ConfigureAwait(false);
        }
        else
        {
            await CheckCancellationAndStartIterateToIndex(nextIteration).ConfigureAwait(false);
        }
    }

    private static async Task TiffNavigation(MainViewModel vm, string currentFileName, int nextIteration)
    {
        if (TiffNavigationInfo is null && !_imageIterator.IsReversed)
        {
            var tiffPages = await Task.FromResult(TiffManager.LoadTiffPages(currentFileName)).ConfigureAwait(false);
            if (tiffPages.Count < 1)
            {
                await CheckCancellationAndStartIterateToIndex(nextIteration).ConfigureAwait(false);
                return;
            }

            TiffNavigationInfo = new TiffManager.TiffNavigationInfo
            {
                CurrentPage = 0,
                PageCount = tiffPages.Count,
                Pages = tiffPages
            };
        }

        if (TiffNavigationInfo is null)
        {
            await CheckCancellationAndStartIterateToIndex(nextIteration).ConfigureAwait(false);
        }
        else
        {
            if (_imageIterator.IsReversed)
            {
                if (TiffNavigationInfo.CurrentPage - 1 < 0)
                {
                    await ExitTiffNavigationAndNavigate().ConfigureAwait(false);
                    return;
                }

                TiffNavigationInfo.CurrentPage -= 1;
            }
            else
            {
                TiffNavigationInfo.CurrentPage += 1;
            }

            if (TiffNavigationInfo.CurrentPage >= TiffNavigationInfo.PageCount || TiffNavigationInfo.CurrentPage < 0)
            {
                await ExitTiffNavigationAndNavigate().ConfigureAwait(false);
            }
            else
            {
                await UpdateImage.SetTiffImageAsync(TiffNavigationInfo, _imageIterator.CurrentIndex, vm.FileInfo, vm);
            }
        }
        return;
        
        async Task ExitTiffNavigationAndNavigate()
        {
            await CheckCancellationAndStartIterateToIndex(nextIteration).ConfigureAwait(false);
            TiffNavigationInfo?.Dispose();
            TiffNavigationInfo = null;
        }
    }
    
    private static async Task<bool> CheckIfTiffAndUpdate(MainViewModel vm, FileInfo fileInfo, int index)
    {
        if (!TiffManager.IsTiff(fileInfo))
        {
            return false;
        }
        
        var tiffPages = await Task.FromResult(TiffManager.LoadTiffPages(fileInfo.FullName)).ConfigureAwait(false);
        if (tiffPages.Count < 1)
        {
            return false;
        }

        TiffNavigationInfo = new TiffManager.TiffNavigationInfo
        {
            CurrentPage = 0,
            PageCount = tiffPages.Count,
            Pages = tiffPages
        };
        await UpdateImage.SetTiffImageAsync(TiffNavigationInfo, index, fileInfo, vm);
        return true;
    }

    public static async Task Navigate(int index, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        await CheckCancellationAndStartIterateToIndex(index).ConfigureAwait(false);
    }
    
    public static async Task Navigate(string fileName, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }
        
        var index = _imageIterator.ImagePaths.IndexOf(fileName);

        await CheckCancellationAndStartIterateToIndex(index).ConfigureAwait(false);
    }

    private static async Task NavigateIncrements(MainViewModel vm, bool next, bool is10, bool is100)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        var currentIndex = _imageIterator.CurrentIndex;
        var direction = next ? NavigateTo.Next : NavigateTo.Previous;
        var index = _imageIterator.GetIteration(currentIndex, direction, false, is10, is100);

        await CheckCancellationAndStartIterateToIndex(index).ConfigureAwait(false);
    }

    public static Task Next10(MainViewModel vm) => NavigateIncrements(vm, true, true, false);
    public static Task Next100(MainViewModel vm) => NavigateIncrements(vm, true, false, true);
    public static Task Prev10(MainViewModel vm) => NavigateIncrements(vm, false, true, false);
    public static Task Prev100(MainViewModel vm) => NavigateIncrements(vm, false, false, true);

    /// <summary>
    ///     Navigates to the first or last image in the collection.
    /// </summary>
    /// <param name="last">True to navigate to the last image, false to navigate to the first image.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task NavigateFirstOrLast(bool last, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryNavigation.NavigateGallery(last, vm);
        }
        else
        {
            if (_cancellationTokenSource is not null)
            {
                await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            await _imageIterator.NextIteration(last ? NavigateTo.Last : NavigateTo.First, _cancellationTokenSource)
                .ConfigureAwait(false);
            await ScrollToEndIfNecessary(last);
        }
    }

    /// <summary>
    ///     Iterates to the next or previous image based on the <paramref name="next" /> parameter.
    /// </summary>
    /// <param name="next">True to iterate to the next image, false for the previous image.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Iterate(bool next, MainViewModel vm)
    {
        if (GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryNavigation.NavigateGallery(next ? Direction.Right : Direction.Left, vm);
        }
        else
        {
            await Navigate(next, vm);
        }
    }

    /// <summary>
    ///     Navigates and moves the cursor to the corresponding button.
    /// </summary>
    /// <param name="next">True to navigate to the next image, false for the previous image.</param>
    /// <param name="arrow">True to move cursor to the arrow, false for the button.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task NavigateAndPositionCursor(bool next, bool arrow, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            await ScrollGallery(next);
        }
        else
        {
            await Navigate(next, vm);
            await MoveCursorOnButtonClick(next, arrow, vm);
        }
    }

    /// <summary>
    ///     Navigates to the next or previous folder and loads the first image in that folder.
    /// </summary>
    /// <param name="next">True to navigate to the next folder, false for the previous folder.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task GoToNextFolder(bool next, MainViewModel vm)
    {
        if (!CanNavigate(vm))
        {
            return;
        }

        TitleManager.SetLoadingTitle(vm);
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        var fileList = await GetNextFolderFileList(next, vm).ConfigureAwait(false);

        if (fileList is null)
        {
            TitleManager.SetTitle(vm);
        }
        else
        {
            vm.PlatformService.StopTaskbarProgress();
            await LoadWithoutImageIterator(new FileInfo(fileList[0]), vm, fileList);
            if (vm.Title == TranslationHelper.Translation.Loading)
            {
                TitleManager.SetTitle(vm);
            }
        }
    }

    #endregion

    #region Load pictures from string, file or url

    /// <summary>
    ///     Loads a picture from a given string source, which can be a file path, directory path, or URL.
    /// </summary>
    /// <param name="source">The string source to load the picture from.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadPicFromStringAsync(string source, MainViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(source) || vm is null)
        {
            return;
        }

        MenuManager.CloseMenus(vm);
        vm.IsLoading = true;
        TitleManager.SetLoadingTitle(vm);

        // Starting in new task makes it more responsive and works better
        await Task.Run(async () =>
        {
            var check = ErrorHelper.CheckIfLoadableString(source);

            if (check == null)
            {
                await ErrorHandling.ReloadAsync(vm).ConfigureAwait(false);
                vm.IsLoading = false;
                ArchiveExtraction.Cleanup();
                return;
            }

            switch (check.Value.Type)
            {
                case ErrorHelper.LoadAbleFileType.File:
                    vm.CurrentView = vm.ImageViewer;
                    await LoadPicFromFile(check.Value.Data, vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    ArchiveExtraction.Cleanup();
                    return;
                case ErrorHelper.LoadAbleFileType.Directory:
                    vm.CurrentView = vm.ImageViewer;
                    await LoadPicFromDirectoryAsync(check.Value.Data, vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    ArchiveExtraction.Cleanup();
                    return;
                case ErrorHelper.LoadAbleFileType.Web:
                    vm.CurrentView = vm.ImageViewer;
                    await LoadPicFromUrlAsync(check.Value.Data, vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    ArchiveExtraction.Cleanup();
                    return;
                case ErrorHelper.LoadAbleFileType.Base64:
                    vm.CurrentView = vm.ImageViewer;
                    await LoadPicFromBase64Async(check.Value.Data, vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    ArchiveExtraction.Cleanup();
                    return;
                case ErrorHelper.LoadAbleFileType.Zip:
                    vm.CurrentView = vm.ImageViewer;
                    await LoadPicFromArchiveAsync(check.Value.Data, vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    return;
                default:
                    await ErrorHandling.ReloadAsync(vm).ConfigureAwait(false);
                    vm.IsLoading = false;
                    ArchiveExtraction.Cleanup();
                    return;
            }
        });
    }

    /// <summary>
    ///     Loads a picture from a given file.
    /// </summary>
    /// <param name="fileName">The file name of the picture to load.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <param name="fileInfo">Optional: FileInfo object for the file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadPicFromFile(string fileName, MainViewModel vm, FileInfo? fileInfo = null)
    {
        if (vm is null)
        {
            return;
        }

        fileInfo ??= new FileInfo(fileName);
        if (!fileInfo.Exists)
        {
            return;
        }

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }

        _cancellationTokenSource = new CancellationTokenSource();

        if (_imageIterator is not null)
        {
            if (fileInfo.DirectoryName == _imageIterator.InitialFileInfo.DirectoryName)
            {
                var index = _imageIterator.ImagePaths.IndexOf(fileInfo.FullName);
                if (index != -1)
                {
                    await _imageIterator.IterateToIndex(index, _cancellationTokenSource).ConfigureAwait(false);
                    await CheckIfTiffAndUpdate(vm, fileInfo, index);
                    if (Settings.Gallery.IsBottomGalleryShown && GetCount > 0)
                    {
                        vm.GalleryMode = GalleryMode.ClosedToBottom;
                    }
                }
                else
                {
                    await LoadWithoutImageIterator(fileInfo, vm);
                }
            }
            else
            {
                await LoadWithoutImageIterator(fileInfo, vm);
            }
        }
        else
        {
            if (Settings.UIProperties.IsTaskbarProgressEnabled)
            {
                vm.PlatformService.StopTaskbarProgress();
            }

            await LoadWithoutImageIterator(fileInfo, vm);
        }
    }

    /// <summary>
    ///     Asynchronously loads a picture from a specified archive file.
    /// </summary>
    /// <param name="path">The path to the archive file containing the picture(s) to load.</param>
    /// <param name="vm">The main view model instance used to manage UI state and operations.</param>
    /// <returns>
    ///     A task representing the asynchronous operation. This task completes when the picture is loaded
    ///     from the archive or when an error occurs during the extraction or loading process.
    /// </returns>
    public static async Task LoadPicFromArchiveAsync(string path, MainViewModel vm)
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }

        vm.IsLoading = true;
        TitleManager.SetLoadingTitle(vm);

        var extraction = await ArchiveExtraction
            .ExtractArchiveAsync(path, vm.PlatformService.ExtractWithLocalSoftwareAsync).ConfigureAwait(false);
        if (!extraction)
        {
            await ErrorHandling.ReloadAsync(vm);
            return;
        }

        if (Directory.Exists(ArchiveExtraction.TempZipDirectory))
        {
            var dirInfo = new DirectoryInfo(ArchiveExtraction.TempZipDirectory);
            if (dirInfo.EnumerateDirectories().Any())
            {
                var firstDir = dirInfo.EnumerateDirectories().First();
                var firstFile = firstDir.EnumerateFiles().First();
                await LoadPicFromFile(firstFile.FullName, vm, firstFile).ConfigureAwait(false);
            }
            else
            {
                await LoadPicFromDirectoryAsync(ArchiveExtraction.TempZipDirectory, vm).ConfigureAwait(false);
            }

            MainKeyboardShortcuts.ClearKeyDownModifiers(); // Fix possible modifier key state issue
        }
        else
        {
            await _imageIterator.DisposeAsync();
            await ErrorHandling.ReloadAsync(vm);
        }
    }

    /// <summary>
    ///     Loads a picture from a given URL.
    /// </summary>
    /// <param name="url">The URL of the picture to load.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadPicFromUrlAsync(string url, MainViewModel vm)
    {
        var tasks = new List<Task>();
        if (_cancellationTokenSource is not null)
        {
            tasks.Add(_cancellationTokenSource.CancelAsync());
        }

        string destination;

        try
        {
            vm.PlatformService.StopTaskbarProgress();

            var httpDownload = HttpManager.GetDownloadClient(url);
            using var client = httpDownload.Client;
            client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
            {
                if (totalFileSize is null || totalBytesDownloaded is null || progressPercentage is null)
                {
                    return;
                }

                var displayProgress = HttpManager.GetProgressDisplay(totalFileSize, totalBytesDownloaded,
                    progressPercentage);
                vm.Title = displayProgress;
                vm.TitleTooltip = displayProgress;
                vm.WindowTitle = displayProgress;
                if (Settings.UIProperties.IsTaskbarProgressEnabled)
                {
                    vm.PlatformService.SetTaskbarProgress((ulong)totalBytesDownloaded, (ulong)totalFileSize);
                }
            };
            tasks.Add(client.StartDownloadAsync());
            if (_imageIterator is not null)
            {
                tasks.Add(_imageIterator.DisposeAsync().AsTask());
            }
            
            await Task.WhenAll(tasks).ConfigureAwait(false);
            destination = httpDownload.DownloadPath;
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine("LoadPicFromUrlAsync exception = \n" + e.Message);
#endif
            await Task.WhenAll(
                TooltipHelper.ShowTooltipMessageAsync(e.Message, true),
                ErrorHandling.ReloadAsync(vm)).ConfigureAwait(false);
            return;
        }

        var fileInfo = new FileInfo(destination);
        if (!fileInfo.Exists)
        {
            await ErrorHandling.ReloadAsync(vm);
            return;
        }

        var imageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
        await UpdateImage.SetSingleImageAsync(imageModel.Image, imageModel.ImageType, url, vm);

        vm.IsLoading = false;
        vm.FileInfo = fileInfo;
        vm.ExifOrientation = imageModel.EXIFOrientation;
        FileHistory.Add(url);

        await DisposeImageIteratorAsync();
        
    }

    /// <summary>
    ///     Loads a picture from a Base64-encoded string.
    /// </summary>
    /// <param name="base64">The Base64-encoded string representing the picture.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadPicFromBase64Async(string base64, MainViewModel vm)
    {
        TitleManager.SetLoadingTitle(vm);
        vm.IsLoading = true;
        vm.ImageSource = null;
        vm.FileInfo = null;
        
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync();
        }
        await DisposeImageIteratorAsync();

        await Task.Run(async () =>
        {
            // TODO: Handle base64 if it's SVG image
            try
            {
                var magickImage = ImageDecoder.Base64ToMagickImage(base64);
                magickImage.Format = MagickFormat.Png;
                await using var memoryStream = new MemoryStream();
                await magickImage.WriteAsync(memoryStream);
                memoryStream.Position = 0;
                var bitmap = new Bitmap(memoryStream);
                var imageModel = new ImageModel
                {
                    Image = bitmap,
                    PixelWidth = bitmap?.PixelSize.Width ?? 0,
                    PixelHeight = bitmap?.PixelSize.Height ?? 0,
                    ImageType = ImageType.Bitmap
                };
                await UpdateImage.SetSingleImageAsync(imageModel.Image, imageModel.ImageType,
                    TranslationHelper.Translation.Base64Image, vm);
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine("LoadPicFromBase64Async exception = \n" + e.Message);
#endif
                if (vm.FileInfo is not null && vm.FileInfo.Exists)
                {
                    await LoadPicFromFile(vm.FileInfo.FullName, vm, vm.FileInfo);
                }
                else
                {
                    await _imageIterator.DisposeAsync();
                    await ErrorHandling.ReloadAsync(vm);
                }
            }
        });
        vm.IsLoading = false;
    }

    /// <summary>
    ///     Loads a picture from a directory.
    /// </summary>
    /// <param name="file">The path to the directory containing the picture.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <param name="fileInfo">Optional: FileInfo object for the directory.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadPicFromDirectoryAsync(string file, MainViewModel vm, FileInfo? fileInfo = null)
    {
        vm.IsLoading = true;
        TitleManager.SetLoadingTitle(vm);

        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }

        _cancellationTokenSource = new CancellationTokenSource();

        if (Settings.UIProperties.IsTaskbarProgressEnabled)
        {
            vm.PlatformService.StopTaskbarProgress();
        }

        fileInfo ??= new FileInfo(file);

        var newFileList = await Task.Run(() =>
        {
            var fileList = vm.PlatformService.GetFiles(fileInfo);
            if (fileList.Count > 0)
            {
                return fileList;
            }

            // Attempt to reload with subdirectories and reset the setting
            if (Settings.Sorting.IncludeSubDirectories)
            {
                return null;
            }

            Settings.Sorting.IncludeSubDirectories = true;
            fileList = vm.PlatformService.GetFiles(fileInfo);
            if (fileList.Count <= 0)
            {
                return null;
            }

            Settings.Sorting.IncludeSubDirectories = false;
            return fileList;
        });

        if (newFileList is null)
        {
            await ErrorHandling.ReloadAsync(vm).ConfigureAwait(false);
            return;
        }

        var firstFileInfo = new FileInfo(newFileList[0]);
        await LoadWithoutImageIterator(firstFileInfo, vm, newFileList);
    }

    #endregion

    #region Helpers

    #region ImageIterator

    public static void InitializeImageIterator(MainViewModel vm)
    {
        _imageIterator ??= new ImageIterator(vm.FileInfo, vm);
    }
    
    public static async Task DisposeImageIteratorAsync()
    {
        if (_imageIterator is null)
        {
            return;
        }
        await _imageIterator.ClearAsync();
        _imageIterator.ImagePaths.Clear();
        await _imageIterator.DisposeAsync();
    }
    
    public static bool IsCollectionEmpty => _imageIterator?.ImagePaths is null || _imageIterator?.ImagePaths?.Count < 0;
    public static List<string>? GetCollection => _imageIterator?.ImagePaths;
    
    public static void UpdateFileListAndIndex(List<string> fileList, int index) => _imageIterator?.UpdateFileListAndIndex(fileList, index);
    
    public static int? GetFileNameIndex(string fileName) =>
        IsCollectionEmpty ? null : _imageIterator.ImagePaths.IndexOf(fileName);

    /// <summary>
    ///     Returns the file name at a given index in the image collection.
    /// </summary>
    /// <param name="index">The index of the file to retrieve.</param>
    /// <returns>The file name at the given index.</returns>
    public static string? GetFileNameAt(int index)
    {
        if (IsCollectionEmpty)
        {
            return null;
        }

        if (index < 0 || index >= _imageIterator.ImagePaths.Count)
        {
            return null;
        }

        return _imageIterator.ImagePaths[index];
    }
    
    /// <summary>
    ///     Gets the current file name.
    /// </summary>
    public static string? GetCurrentFileName => GetFileNameAt(_imageIterator?.CurrentIndex ?? -1);
    
    /// <summary>
    ///     Gets the next file name.
    /// </summary>
    public static string? GetNextFileName => GetFileNameAt(_imageIterator?.NextIndex ?? -1);

    public static int GetCurrentIndex => _imageIterator?.CurrentIndex ?? -1;
    
    public static int GetNextIndex => _imageIterator?.NextIndex ?? -1;
    
    public static int GetNonZeroIndex => _imageIterator?.GetNonZeroIndex ?? -1;
    
    public static int GetCount => _imageIterator?.GetCount ?? -1;
    
    public static FileInfo? GetInitialFileInfo => _imageIterator?.InitialFileInfo;
    
    public static PreLoadValue? GetPreLoadValue(int index) => _imageIterator?.GetPreLoadValue(index) ?? null;
    public static async Task<PreLoadValue?> GetPreLoadValueAsync(int index) => await _imageIterator?.GetPreLoadValueAsync(index) ?? null;
    public static async Task<PreLoadValue?> GetPreLoadValueAsync(string fileName) => await _imageIterator?.GetPreLoadValueAsync(GetFileNameIndex(fileName) ?? GetCurrentIndex) ?? null;
    public static PreLoadValue? GetCurrentPreLoadValue() => _imageIterator?.GetCurrentPreLoadValue() ?? null;
    public static async Task<PreLoadValue?> GetCurrentPreLoadValueAsync() => await _imageIterator?.GetCurrentPreLoadValueAsync() ?? null;
    public static PreLoadValue? GetNextPreLoadValue() => _imageIterator?.GetNextPreLoadValue() ?? null;
    public static async Task<PreLoadValue?> GetNextPreLoadValueAsync() => await _imageIterator?.GetNextPreLoadValueAsync() ?? null;
    
    public static async Task ReloadFileListAsync() => await _imageIterator?.ReloadFileListAsync();
    
    public static void AddToPreloader(int index, ImageModel imageModel) => _imageIterator?.Add(index, imageModel);
    public static async Task PreloadAsync() => await _imageIterator?.PreloadAsync();

    #endregion

    #region Reload

    public static async Task QuickReload()
    {
        if (_imageIterator is null)
        {
            return;
        }
        await _imageIterator.QuickReload();
    }
    
    public static async Task FullReload(MainViewModel vm)
    {
        if (vm.ImageSource is null)
        {
            return;
        }
        
        if (_imageIterator is null)
        {
            var url = vm.Title.GetURL();
            if (!string.IsNullOrEmpty(url))
            {
                await LoadPicFromUrlAsync(url, vm).ConfigureAwait(false);
            }
            else 
            {
                await ClipboardImageOperations.PasteClipboardImage(vm);
            }
            return;
        }

        var index = _imageIterator.CurrentIndex;
        await _imageIterator.DisposeAsync().ConfigureAwait(false);
        _imageIterator = new ImageIterator(vm.FileInfo, vm);
        await Navigate(index, vm).ConfigureAwait(false);
    }

    #endregion
    
    /// <summary>
    ///     Checks if the previous iteration has been cancelled and starts the iteration at the given index in a new task.
    /// </summary>
    /// <param name="index">The index to iterate to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task CheckCancellationAndStartIterateToIndex(int index)
    {
        await Task.Run(() =>
        {
            if (_cancellationTokenSource is not null)
            {
                _ = _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _ = _imageIterator.NextIteration(index, _cancellationTokenSource).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <summary>
    ///     Gets the list of files in the next or previous folder.
    /// </summary>
    /// <param name="next">True to get the next folder, false for the previous folder.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation that returns a list of file paths.</returns>
    private static async Task<List<string>?> GetNextFolderFileList(bool next, MainViewModel vm)
    {
        return await Task.Run(() =>
        {
            var indexChange = next ? 1 : -1;
            var currentFolder = Path.GetDirectoryName(_imageIterator?.ImagePaths[_imageIterator.CurrentIndex]);
            var parentFolder = Path.GetDirectoryName(currentFolder);
            var directories = Directory.GetDirectories(parentFolder, "*", SearchOption.TopDirectoryOnly);
            var directoryIndex = Array.IndexOf(directories, currentFolder);
            if (Settings.UIProperties.Looping)
            {
                directoryIndex = (directoryIndex + indexChange + directories.Length) % directories.Length;
            }
            else
            {
                directoryIndex += indexChange;
                if (directoryIndex < 0 || directoryIndex >= directories.Length)
                {
                    return null;
                }
            }

            for (var i = directoryIndex; i < directories.Length; i++)
            {
                var fileInfo = new FileInfo(directories[i]);
                var fileList = vm.PlatformService.GetFiles(fileInfo);
                if (fileList is { Count: > 0 })
                {
                    return fileList;
                }
            }

            return null;
        }).ConfigureAwait(false);
    }


    /// <summary>
    ///     Loads a picture from a given file, reloads the ImageIterator and loads the corresponding gallery from the file's
    ///     directory.
    /// </summary>
    /// <param name="fileInfo">The FileInfo object representing the file to load.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <param name="files">
    ///     Optional: The list of file paths to load. If null, the list is loaded from the given file's
    ///     directory.
    /// </param>
    /// <param name="index">Optional: The index at which to start the navigation. Defaults to 0.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task LoadWithoutImageIterator(FileInfo fileInfo, MainViewModel vm, List<string>? files = null,
        int index = 0)
    {
        var imageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
        ImageModel? nextImageModel = null;
        vm.ImageSource = imageModel.Image;
        vm.ImageType = imageModel.ImageType;
        if (!Settings.ImageScaling.ShowImageSideBySide)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, 0, 0, imageModel.Rotation,
                    vm);
            });
        }

        await DisposeImageIteratorAsync();
        
        if (files is null)
        {
            _imageIterator = new ImageIterator(fileInfo, vm);
            index = _imageIterator.CurrentIndex;
        }
        else
        {
            _imageIterator = new ImageIterator(fileInfo, files, index, vm);
        }

        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            nextImageModel = (await _imageIterator.GetNextPreLoadValueAsync()).ImageModel;
            vm.SecondaryImageSource = nextImageModel.Image;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, nextImageModel.PixelWidth,
                    nextImageModel.PixelHeight, imageModel.Rotation, vm);
            });
            
            TitleManager.SetSideBySideTitle(vm, imageModel, nextImageModel);
            UpdateImage.SetStats(vm, index, imageModel);
            
            // Fixes incorrect rendering in the side by side view
            // TODO: Improve and fix side by side and remove this hack 
            Dispatcher.UIThread.Post(() => { vm.ImageViewer?.MainImage?.InvalidateVisual(); });
        }
        else
        {
            var isTiffUpdated = await CheckIfTiffAndUpdate(vm, fileInfo, index); 
            if (!isTiffUpdated)
            {
                if (Settings.ImageScaling.ShowImageSideBySide)
                {
                    TitleManager.SetSideBySideTitle(vm, imageModel, nextImageModel);
                }
                else
                {
                    TitleManager.SetTitle(vm, imageModel);
                }
        
                UpdateImage.SetStats(vm, index, imageModel);
            }
        }

        vm.IsLoading = false;
        FileHistory.Add(_imageIterator.ImagePaths[index]);
        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            FileHistory.Add(_imageIterator.ImagePaths[_imageIterator.GetIteration(index, NavigateTo.Next)]);
        }
        await CheckAndReloadGallery(fileInfo, vm);
    }

    /// <summary>
    ///     Checks and reloads the gallery if necessary based on the provided file info.
    /// </summary>
    /// <param name="fileInfo">The file info to check.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task CheckAndReloadGallery(FileInfo fileInfo, MainViewModel vm)
    {
        if (Settings.Gallery.IsBottomGalleryShown || GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryFunctions.Clear();

            // Check if the bottom gallery should be shown
            if (!GalleryFunctions.IsFullGalleryOpen)
            {
                if (vm.GalleryMode is GalleryMode.BottomToClosed or GalleryMode.FullToClosed or GalleryMode.Closed)
                {
                    // Trigger animation to show it
                    vm.GalleryMode = GalleryMode.ClosedToBottom;
                }
            }

            await GalleryLoad.ReloadGalleryAsync(vm, fileInfo.DirectoryName);
        }
    }

    /// <summary>
    ///     Scrolls the gallery to the next or previous page.
    /// </summary>
    /// <param name="next">True to scroll to the next page, false for the previous page.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ScrollGallery(bool next)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (next)
            {
                UIHelper.GetGalleryView.GalleryListBox.PageRight();
            }
            else
            {
                UIHelper.GetGalleryView.GalleryListBox.PageLeft();
            }
        });
    }

    /// <summary>
    ///     Scrolls to the end of the gallery if the <paramref name="last" /> parameter is true.
    /// </summary>
    /// <param name="last">True to scroll to the end of the gallery.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ScrollToEndIfNecessary(bool last)
    {
        if (last && Settings.Gallery.IsBottomGalleryShown)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { UIHelper.GetGalleryView.GalleryListBox.ScrollToEnd(); });
        }
    }

    /// <summary>
    ///     Moves the cursor on the navigation button.
    /// </summary>
    /// <param name="next">True to move the cursor to the next button, false for the previous button.</param>
    /// <param name="arrow">True to move the cursor on the arrow, false to move the cursor on the button.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task MoveCursorOnButtonClick(bool next, bool arrow, MainViewModel vm)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var buttonName = GetNavigationButtonName(next, arrow);
            var control = GetButtonControl(buttonName, arrow);
            var point = GetClickPoint(next, arrow);
            var p = control.PointToScreen(point);
            vm.PlatformService?.SetCursorPos(p.X, p.Y);
        });
    }

    /// <summary>
    ///     Gets the name of the navigation button based on input parameters.
    /// </summary>
    /// <param name="next">True for the next button, false for the previous button.</param>
    /// <param name="arrow">True if the navigation uses arrow keys.</param>
    /// <returns>The name of the navigation button.</returns>
    private static string GetNavigationButtonName(bool next, bool arrow)
    {
        return arrow
            ? next ? "ClickArrowRight" : "ClickArrowLeft"
            : next
                ? "NextButton"
                : "PreviousButton";
    }

    /// <summary>
    ///     Gets the control associated with the specified button name.
    /// </summary>
    /// <param name="buttonName">The name of the button.</param>
    /// <param name="arrow">True if the control is an arrow button.</param>
    /// <returns>The control associated with the button.</returns>
    private static Control GetButtonControl(string buttonName, bool arrow)
    {
        return arrow
            ? UIHelper.GetMainView.GetControl<UserControl>(buttonName)
            : UIHelper.GetBottomBar.GetControl<Button>(buttonName);
    }

    /// <summary>
    ///     Gets the point to click on the button based on the input parameters.
    /// </summary>
    /// <param name="next">True for the next button, false for the previous button.</param>
    /// <param name="arrow">True if the navigation uses arrow keys.</param>
    /// <returns>The point to click on the button.</returns>
    private static Point GetClickPoint(bool next, bool arrow)
    {
        return arrow
            ? next ? new Point(65, 95) : new Point(15, 95)
            : new Point(50, 10);
    }

    #endregion
}