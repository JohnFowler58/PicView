using System.Diagnostics;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views.UC.PopUps;
using PicView.Core.FileHandling;
using PicView.Core.Localization;

namespace PicView.Avalonia.FileSystem;

public static class FileManager
{
    /// <summary>
    /// Deletes the current file, either permanently or by moving to recycle bin
    /// </summary>
    public static async Task DeleteFile(bool recycle, MainViewModel vm)
    {
        if (vm.FileInfo is null)
        {
            return;
        }
        
        try
        {
            string? errorMsg = null;
            
            if (!recycle)
            {
                var prompt = TranslationHelper.GetTranslation("DeleteFilePermanently");
                var deleteDialog = new DeleteDialog(prompt, vm.FileInfo.FullName);
                UIHelper.GetMainView.MainGrid.Children.Add(deleteDialog);
                // Dialog handles the deletion
            }
            else
            {
                errorMsg = await Task.FromResult(FileDeletionHelper.DeleteFileWithErrorMsg(vm.FileInfo.FullName, recycle));
            }
    
            if (!string.IsNullOrEmpty(errorMsg))
            {
                await TooltipHelper.ShowTooltipMessageAsync(errorMsg, true);
            }
        }
        catch (Exception ex)
        {
            await LogAndShowError(ex, nameof(DeleteFile));
        }
    }
    
    /// <summary>
    /// Shows properties dialog for the specified file
    /// </summary>
    public static async Task ShowFileProperties(string path, MainViewModel vm)
    {
        if (!ValidateParameters(path, vm.PlatformService))
        {
            return;
        }
        
        try
        {
            await Task.Run(() => vm.PlatformService!.ShowFileProperties(path));
        }
        catch (Exception ex)
        {
            await LogAndShowError(ex, nameof(ShowFileProperties));
        }
    }
    
    /// <summary>
    /// Prints the specified image file
    /// </summary>
    public static async Task Print(string? path, MainViewModel vm)
    {
        if (!ValidateParameters(path, vm.PlatformService))
        {
            return;
        }
        
        try
        {
            vm.IsLoading = true;
            await ExecutePlatformServiceOperationAsync(path!, vm, 
                (platformService, file) => platformService.Print(file));
        }
        catch (Exception ex)
        {
            await LogAndShowError(ex, nameof(Print));
        }
        finally
        {
            vm.IsLoading = false;
        }
    }
    
    /// <summary>
    /// Opens the file location in file explorer
    /// </summary>
    public static async Task LocateOnDisk(string path, MainViewModel vm)
    {
        if (!ValidateParameters(path, vm.PlatformService))
        {
            return;
        }
        try
        {
            await Task.Run(() => vm.PlatformService!.LocateOnDisk(path));
        }
        catch (Exception ex)
        {
            await LogAndShowError(ex, nameof(LocateOnDisk));
        }
    }
    
    /// <summary>
    /// Shows the dialog to open the file with another application
    /// </summary>
    public static async Task OpenWith(string path, MainViewModel vm)
    {
        if (!ValidateParameters(path, vm.PlatformService))
        {
            return;
        }
        try
        {
            await Task.Run(() => vm.PlatformService!.OpenWith(path));
        }
        catch (Exception ex)
        {
            await LogAndShowError(ex, nameof(LocateOnDisk));
        }
    }

    #region Private Helper Methods
    
    /// <summary>
    /// Validates common parameters for file operations
    /// </summary>
    private static bool ValidateParameters(string? path, object? platformService)
    {
        return !string.IsNullOrWhiteSpace(path) && platformService != null;
    }
    
    /// <summary>
    /// Helper method to handle common platform service operations that might require file conversion
    /// </summary>
    private static async Task ExecutePlatformServiceOperationAsync(string path, MainViewModel vm, 
        Action<dynamic, string> platformServiceAction)
    {
        var file = await ImageFormatConverter.ConvertToCommonSupportedFormatAsync(path, vm)
            .ConfigureAwait(false);
            
        if (string.IsNullOrWhiteSpace(file))
        {
            await TooltipHelper.ShowTooltipMessageAsync(TranslationHelper.Translation.UnexpectedError);
            return;
        }

        await Task.Run(() => platformServiceAction(vm.PlatformService!, file));
    }
    
    /// <summary>
    /// Logs errors and shows appropriate error messages
    /// </summary>
    private static async Task LogAndShowError(Exception ex, string methodName)
    {
#if DEBUG
        Debug.WriteLine($"{nameof(FileManager)}.{methodName}: {ex.Message}");
        Debug.WriteLine($"Stack trace: {ex.StackTrace}");
#endif
        await TooltipHelper.ShowTooltipMessageAsync(ex.Message);
    }
    
    #endregion
}