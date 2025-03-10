using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using PicView.Avalonia.Animations;

namespace PicView.Avalonia.Clipboard;

/// <summary>
/// Base service for clipboard operations
/// </summary>
public static class ClipboardService
{
    /// <summary>
    /// Gets the clipboard instance from the current application
    /// </summary>
    /// <returns>The clipboard instance or null if not available</returns>
    public static IClipboard? GetClipboard()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow?.Clipboard;
        }
        return null;
    }
    
    /// <summary>
    /// Executes a clipboard operation with standard error handling and animation
    /// </summary>
    /// <param name="operation">The clipboard operation to perform</param>
    /// <param name="showAnimation">Whether to show the copy animation</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    public static async Task<bool> ExecuteClipboardOperation(Func<Task<bool>> operation, bool showAnimation = true)
    {
        try
        {
            var success = await operation();
            
            if (success && showAnimation)
            {
                await AnimationsHelper.CopyAnimation();
            }
            
            return success;
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"{nameof(ClipboardService)} operation failed: {ex.Message}\n{ex.StackTrace}");
#endif
            return false;
        }
    }
}