using System.Diagnostics;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;

namespace PicView.Avalonia.ImageHandling;

/// <summary>
/// Provides image optimization functionality
/// </summary>
public static class ImageOptimizer
{
    /// <summary>
    /// Optimizes the current image in the view model
    /// </summary>
    /// <param name="vm">The main view model</param>
    public static async Task OptimizeImageAsync(MainViewModel vm)
    {
        ArgumentNullException.ThrowIfNull(vm);

        if (!NavigationManager.CanNavigate(vm) || vm.FileInfo == null)
        {
            return;
        }
        
        await Task.Run(() =>
        {
            try
            {
                var optimizer = new ImageMagick.ImageOptimizer
                {
                    OptimalCompression = true
                };
                optimizer.LosslessCompress(vm.FileInfo.FullName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error optimizing image: {ex.Message}");
            }
        });
        
        TitleManager.SetTitle(vm);
    }
}