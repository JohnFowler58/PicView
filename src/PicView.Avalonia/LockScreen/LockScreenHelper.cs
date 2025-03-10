using System.Diagnostics;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.Localization;

namespace PicView.Avalonia.LockScreen;

public static class LockScreenHelper
{
    public static async Task SetAsLockScreenTask(string path, MainViewModel vm)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        if (vm.PlatformService is null)
        {
            return;
        }
        
        vm.IsLoading = true;

        try
        {
            var file = await ImageFormatConverter.ConvertToCommonSupportedFormatAsync(path, vm).ConfigureAwait(false);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    Verb = "runas",
                    UseShellExecute = true,
                    FileName = "PicView.exe",
                    Arguments = "lockscreen," + file,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                }
            };
            process.Start();
            await TooltipHelper.ShowTooltipMessageAsync(TranslationHelper.Translation.Applying, true);
            await process.WaitForExitAsync();
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
}
