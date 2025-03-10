using System.Runtime.InteropServices;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;

namespace PicView.Avalonia.Wallpaper;

public static class WallpaperManager
{
    
    public static async Task SetAsWallpaper(string path, WallpaperStyle style, MainViewModel vm)
    {
        if (vm.PlatformService is null)
        {
            return;
        }
        
        vm.IsLoading = true;
        try
        {
            var file = await ImageFormatConverter.ConvertToCommonSupportedFormatAsync(path, vm).ConfigureAwait(false);

            vm.PlatformService?.SetAsWallpaper(file, GetWallpaperStyle(style));
        }
        catch (Exception e)
        {
            await TooltipHelper.ShowTooltipMessageAsync(e.Message, true);
#if DEBUG
            Console.WriteLine(e);   
#endif
        }
        finally
        {
            vm.IsLoading = false;
        }
    }
    
    public static int GetWallpaperStyle(WallpaperStyle style)
    {
        switch (style)
        {
            case WallpaperStyle.Tile:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return -1;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 0;
                }
                break;
            case WallpaperStyle.Center:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return -1;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 1;
                }
                break;
            case WallpaperStyle.Stretch:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return -1;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 2;
                }
                break;
            case WallpaperStyle.Fit:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return -1;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 3;
                }
                break;
            case WallpaperStyle.Fill:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return -1;
                }
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return 4;
                }
                break;
            default:
                return 3;
        }
        return 0;
    }
}
