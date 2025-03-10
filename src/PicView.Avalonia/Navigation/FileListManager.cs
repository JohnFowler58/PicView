using Avalonia.Media;
using Avalonia.Threading;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.Interfaces;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.FileHandling;

namespace PicView.Avalonia.Navigation;

public static class FileListManager
{
    private static CancellationTokenSource? _cancellationTokenSource;
    
    public static List<string> SortIEnumerable(IEnumerable<string> files, IPlatformSpecificService? platformService)
    {
        var sortFilesBy = FileListHelper.GetSortOrder();

        switch (sortFilesBy)
        {
            default:
            case FileListHelper.SortFilesBy.Name: // Alphanumeric sort
                var list = files.ToList();
                if (Settings.Sorting.Ascending)
                {
                    list.Sort(platformService.CompareStrings);
                }
                else
                {
                    list.Sort((x, y) => platformService.CompareStrings(y, x));
                }

                return list;

            case FileListHelper.SortFilesBy.FileSize: // Sort by file size
                var fileInfoList = files.Select(f => new FileInfo(f)).ToList();
                var sortedBySize = Settings.Sorting.Ascending
                    ? fileInfoList.OrderBy(f => f.Length)
                    : fileInfoList.OrderByDescending(f => f.Length);
                return sortedBySize.Select(f => f.FullName).ToList();

            case FileListHelper.SortFilesBy.Extension: // Sort by file extension
                var sortedByExtension = Settings.Sorting.Ascending
                    ? files.OrderBy(Path.GetExtension)
                    : files.OrderByDescending(Path.GetExtension);
                return sortedByExtension.ToList();

            case FileListHelper.SortFilesBy.CreationTime: // Sort by file creation time
                var sortedByCreationTime = Settings.Sorting.Ascending
                    ? files.OrderBy(f => new FileInfo(f).CreationTime)
                    : files.OrderByDescending(f => new FileInfo(f).CreationTime);
                return sortedByCreationTime.ToList();

            case FileListHelper.SortFilesBy.LastAccessTime: // Sort by file last access time
                var sortedByLastAccessTime = Settings.Sorting.Ascending
                    ? files.OrderBy(f => new FileInfo(f).LastAccessTime)
                    : files.OrderByDescending(f => new FileInfo(f).LastAccessTime);
                return sortedByLastAccessTime.ToList();

            case FileListHelper.SortFilesBy.LastWriteTime: // Sort by file last write time
                var sortedByLastWriteTime = Settings.Sorting.Ascending
                    ? files.OrderBy(f => new FileInfo(f).LastWriteTime)
                    : files.OrderByDescending(f => new FileInfo(f).LastWriteTime);
                return sortedByLastWriteTime.ToList();

            case FileListHelper.SortFilesBy.Random: // Sort files randomly
                return files.OrderBy(f => Guid.NewGuid()).ToList();
        }
    }
    
    public static async Task UpdateFileList(IPlatformSpecificService? platformSpecificService, MainViewModel vm, FileListHelper.SortFilesBy sortFilesBy)
    {
        Settings.Sorting.SortPreference = (int)sortFilesBy;
        if (!NavigationManager.CanNavigate(vm))
        {
            return;
        }

        await UpdateFileList(platformSpecificService, vm);
    }

    public static async Task UpdateFileList(IPlatformSpecificService? platformSpecificService, MainViewModel vm, bool ascending)
    {
        Settings.Sorting.Ascending = ascending;
        if (!NavigationManager.CanNavigate(vm))
        {
            return;
        }
        await UpdateFileList(platformSpecificService, vm);
    }

    private static async Task UpdateFileList(IPlatformSpecificService? platformSpecificService, MainViewModel vm)
    {
        if (_cancellationTokenSource is not null)
        {
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var success = await Task.Run(() =>
        {
            try
            {
                var files = platformSpecificService.GetFiles(vm.FileInfo);
                if (files is not { Count: > 0 })
                {
                    return false;
                }

                NavigationManager.UpdateFileListAndIndex(files, files.IndexOf(vm.FileInfo.FullName));
                TitleManager.SetTitle(vm);
                return true;
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine($"{nameof(UpdateFileList)} exception:\n{e.Message}");
#endif
                return false;
            }

        }, _cancellationTokenSource.Token);
        if (!success)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Fixes the text alignment
            // TODO: Find a better solution
            UIHelper.GetEditableTitlebar.TextBlock.TextAlignment = TextAlignment.Left;
            UIHelper.GetEditableTitlebar.TextBlock.TextAlignment = TextAlignment.Center;
        });

        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            await GalleryLoad.ReloadGalleryAsync(vm, vm.FileInfo.DirectoryName);
        }
    }
}