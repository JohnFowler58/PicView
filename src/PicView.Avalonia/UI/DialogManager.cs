using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using PicView.Avalonia.Crop;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views.UC.PopUps;
using PicView.Avalonia.WindowBehavior;

namespace PicView.Avalonia.UI;

public static class DialogManager
{
    public static bool IsDialogOpen { get; set; }
    
    /// <summary>
    /// Handles close action based on current application state
    /// </summary>
    public static async Task Close(MainViewModel vm)
    {
        // Handle open menus
        if (MenuManager.IsAnyMenuOpen(vm))
        {
            MenuManager.CloseMenus(vm);
            return;
        }

        // Handle cropping mode
        if (CropFunctions.IsCropping)
        {
            CropFunctions.CloseCropControl(vm);
            return;
        }

        // Handle slideshow
        if (Slideshow.IsRunning)
        {
            Slideshow.StopSlideshow(vm);
            return;
        }

        // Handle fullscreen
        if (Settings.WindowProperties.Fullscreen)
        {
            await WindowFunctions.MaximizeRestore();
            return;
        }
        
        // Handle window close
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Settings.UIProperties.ShowConfirmationOnEsc)
            {
                UIHelper.GetMainView?.MainGrid.Children.Add(new CloseDialog());
            }
            else
            {
                desktop.MainWindow?.Close();
            }
        });
    }
}
