using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Platform.Storage;
using PicView.Avalonia.Animations;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.FileHandling;
using PicView.Core.Localization;
using PicView.Core.ProcessHandling;

namespace PicView.Avalonia.Clipboard;

/// <summary>
/// Handles clipboard operations related to files
/// </summary>
public static class ClipboardFileOperations
{
    /// <summary>
    /// Duplicates the specified file, either the current file or another one specified by path.
    /// If the current file is being duplicated, the view model will navigate to the duplicated file.
    /// </summary>
    /// <param name="path">Path to the file to duplicate, or null to duplicate the current file.</param>
    /// <param name="vm">The main view model</param>
    public static async Task Duplicate(string path, MainViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        
        try
        {
            vm.IsLoading = true;
            
            if (path == vm.FileInfo?.FullName)
            {
                await DuplicateCurrentFile(vm);
            }
            else
            {
                await DuplicateFile(path);
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"{nameof(ClipboardFileOperations)} {nameof(Duplicate)} {ex.StackTrace}");
#endif
            await TooltipHelper.ShowTooltipMessageAsync(TranslationHelper.Translation.UnexpectedError);
        }
        finally
        {
            vm.IsLoading = false;
        }
    }

    /// <summary>
    /// Duplicates the current file and navigates to it
    /// </summary>
    /// <param name="vm">The main view model</param>
    private static async Task DuplicateCurrentFile(MainViewModel vm)
    {
        if (!NavigationManager.CanNavigate(vm))
        {
            return;
        }

        var oldPath = vm.FileInfo.FullName;
        var duplicatedPath = await FileHelper.DuplicateAndReturnFileNameAsync(oldPath, vm.FileInfo);

        if (string.IsNullOrWhiteSpace(duplicatedPath) || !File.Exists(duplicatedPath))
        {
            return;
        }

        await Task.WhenAll(
            AnimationsHelper.CopyAnimation(), 
            NavigationManager.LoadPicFromFile(duplicatedPath, vm)
        );
    }
    
    /// <summary>
    /// Duplicates the specified file and plays a copy animation when done. The original file is not navigated away from.
    /// </summary>
    /// <param name="path">Path to the file to duplicate</param>
    private static async Task DuplicateFile(string path)
    {
        var duplicatedPath = await FileHelper.DuplicateAndReturnFileNameAsync(path);
        if (!string.IsNullOrWhiteSpace(duplicatedPath))
        {
            await AnimationsHelper.CopyAnimation();
        }
    }

    /// <summary>
    /// Copies a file to the clipboard
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="vm">The main view model</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static Task<bool> CopyFileToClipboard(string? filePath, MainViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(false);
        }

        return ClipboardService.ExecuteClipboardOperation(
            () => Task.Run(() => vm.PlatformService.CopyFile(filePath))
        );
    }

    /// <summary>
    /// Cuts a file to the clipboard (copy + mark for deletion on paste)
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="vm">The main view model</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public static Task<bool> CutFile(string filePath, MainViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Task.FromResult(false);
        }

        return ClipboardService.ExecuteClipboardOperation(
            () => Task.Run(() => vm.PlatformService.CutFile(filePath))
        );
    }
    
    /// <summary>
    /// Handles pasting files from the clipboard
    /// </summary>
    public static async Task PasteFiles(object files, MainViewModel vm)
    {
        try
        {
            if (files is IEnumerable<IStorageItem> items)
            {
                await ProcessStorageItems(items.ToArray(), vm);
            }
            else if (files is IStorageItem singleFile)
            {
                var path = GetLocalPath(singleFile.Path);
                await NavigationManager.LoadPicFromStringAsync(path, vm);
            }
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"{nameof(ClipboardFileOperations)} {nameof(PasteFiles)} {ex.StackTrace}");
#endif
        }
    }
    
    private static async Task ProcessStorageItems(IStorageItem[] storageItems, MainViewModel vm)
    {
        if (storageItems.Length == 0)
        {
            return;
        }

        // Load the first file
        var firstFile = storageItems[0];
        var firstPath = GetLocalPath(firstFile.Path);
        await NavigationManager.LoadPicFromStringAsync(firstPath, vm);

        // Open consecutive files in a new process
        foreach (var file in storageItems.Skip(1))
        {
            var path = GetLocalPath(file.Path);
            ProcessHelper.StartNewProcess(path);
        }
    }
    
    private static string GetLocalPath(Uri uri)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? uri.AbsolutePath
            : uri.LocalPath;
    }
}