using Avalonia;
using Avalonia.Threading;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.Calculations;
using PicView.Core.Gallery;
using PicView.Core.Navigation;
using StartUpMenu = PicView.Avalonia.Views.StartUpMenu;

namespace PicView.Avalonia.Navigation;

public static class ErrorHandling
{
    public static void ShowStartUpMenu(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Start();
        }
        else
        {
            Dispatcher.UIThread.Post(Start);
        }

        return;
        void Start()
        {
            if (vm.CurrentView is not StartUpMenu)
            {
                var startUpMenu = new StartUpMenu();
                if (Settings.WindowProperties.AutoFit)
                {
                    startUpMenu.Width = SizeDefaults.WindowMinSize;
                    startUpMenu.Height = SizeDefaults.WindowMinSize;
                    if (Settings.Gallery.IsBottomGalleryShown)
                    {
                        vm.GalleryWidth = SizeDefaults.WindowMinSize;
                    }
                }
                vm.CurrentView = startUpMenu;
            }
            else
            {
                TitleManager.SetNoImageTitle(vm);
            }

            vm.GalleryMode = GalleryMode.Closed;
            GalleryFunctions.Clear();
            MenuManager.CloseMenus(vm);
            vm.GalleryMargin = new Thickness(0, 0, 0, 0);
            vm.GetIndex = 0;
            vm.PlatformService.StopTaskbarProgress();
            vm.IsLoading = false;

            _ = NavigationManager.DisposeImageIteratorAsync();
        }
    }

    public static async Task ReloadAsync(MainViewModel vm)
    {
        vm.IsLoading = true;
        
        if (vm.ImageSource is null)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ShowStartUpMenu(vm);
            });
            return;
        }
        
        if (!NavigationManager.CanNavigate(vm))
        {
            await NavigationManager.LoadPicFromStringAsync(FileHistory.GetLastEntry(), vm).ConfigureAwait(false);
            return;
        }
        
        if (vm.ImageSource is null || !NavigationManager.CanNavigate(vm))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ShowStartUpMenu(vm);
            });
            return;
        }

        try
        {
            await NavigationManager.FullReload(vm);
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine(e);
#endif
            await Dispatcher.UIThread.InvokeAsync(() => { ShowStartUpMenu(vm); });
        }
        finally
        {
            vm.IsLoading = false;
        }
    }
    
    public static async Task ReloadImageAsync(MainViewModel vm)
    {
        await NavigationManager.FullReload(vm);
    }
}
