using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using PicView.Avalonia.ColorManagement;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views.UC.Buttons;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Gallery;
using PicView.Core.Localization;

namespace PicView.Avalonia.SettingsManagement;
public static class SettingsUpdater
{
    public static async Task ResetSettings(MainViewModel vm)
    {
        vm.IsLoading = true;

        try
        {
            DeleteSettingFiles();
            SetDefaults();

            ThemeManager.DetermineTheme(Application.Current, false);
        
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                TurnOffUsingTouchpad(vm);
            }
            else
            {
                TurnOffUsingTouchpad(vm);
            }
        
            vm.GetBottomGalleryItemHeight = GalleryDefaults.DefaultBottomGalleryHeight;
            vm.GetFullGalleryItemHeight = GalleryDefaults.DefaultFullGalleryHeight;
        
            if (string.IsNullOrWhiteSpace(Settings.Gallery.BottomGalleryStretchMode))
            {
                Settings.Gallery.BottomGalleryStretchMode = "UniformToFill";
            }

            if (string.IsNullOrWhiteSpace(Settings.Gallery.FullGalleryStretchMode))
            {
                Settings.Gallery.FullGalleryStretchMode = "UniformToFill";
            }
        
            await TurnOffSubdirectories(vm);
        
            TurnOffSideBySide(vm);
            TurnOffScroll(vm);
            TurnOffCtrlZoom(vm);
            TurnOffLooping(vm);
        
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.PlatformService.StopTaskbarProgress();
                if (NavigationManager.CanNavigate(vm))
                {
                    vm.PlatformService.SetTaskbarProgress((ulong)NavigationManager.GetCurrentIndex,
                        (ulong)NavigationManager.GetCount);
                }
                WindowResizing.SetSize(vm);
            });
            
            await SaveSettingsAsync();
        }
        finally
        {
            TitleManager.SetTitle(vm);
            vm.IsLoading = false;
        }
    }

    public static async Task ToggleUsingTouchpad(MainViewModel vm)
    {
        if (Settings.Zoom.IsUsingTouchPad)
        {
            TurnOffUsingTouchpad(vm);
        }
        else
        {
            TurnOnUsingTouchpad(vm);
        }
    
        await SaveSettingsAsync();
    }
    
    public static void TurnOffUsingTouchpad(MainViewModel vm)
    {
        Settings.Zoom.IsUsingTouchPad = false;
        vm.GetIsUsingTouchpadTranslation = TranslationHelper.Translation.UsingMouse;
        vm.IsUsingTouchpad = false;
    }
    
    public static void TurnOnUsingTouchpad(MainViewModel vm)
    {
        Settings.Zoom.IsUsingTouchPad = true;
        vm.GetIsUsingTouchpadTranslation = TranslationHelper.Translation.UsingTouchpad;
        vm.IsUsingTouchpad = true;
    }
    
    public static async Task ToggleSubdirectories(MainViewModel vm)
    {
        if (Settings.Sorting.IncludeSubDirectories)
        {
            await TurnOffSubdirectories(vm).ConfigureAwait(false);
        }
        else
        {
            await TurnOnSubdirectories(vm).ConfigureAwait(false);
        }
        await SaveSettingsAsync();
    }
    
    public static async Task TurnOffSubdirectories(MainViewModel vm)
    {
        vm.IsIncludingSubdirectories = false;
        Settings.Sorting.IncludeSubDirectories = false;
        
        await NavigationManager.ReloadFileListAsync().ConfigureAwait(false);
        TitleManager.SetTitle(vm);
    }
    
    public static async Task TurnOnSubdirectories(MainViewModel vm)
    {
        vm.IsIncludingSubdirectories = true;
        Settings.Sorting.IncludeSubDirectories = true;
        
        await NavigationManager.ReloadFileListAsync().ConfigureAwait(false);
        TitleManager.SetTitle(vm);
    }
    
    public static async Task ToggleTaskbarProgress(MainViewModel vm)
    {
        if (Settings.UIProperties.IsTaskbarProgressEnabled)
        {
            Settings.UIProperties.IsTaskbarProgressEnabled = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                vm.PlatformService.StopTaskbarProgress();
            });
        }
        else
        {
            Settings.UIProperties.IsTaskbarProgressEnabled = true;
            if (NavigationManager.CanNavigate(vm))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.PlatformService.SetTaskbarProgress((ulong)NavigationManager.GetCurrentIndex,
                        (ulong)NavigationManager.GetCount);
                });
            }
        }

        await SaveSettingsAsync();
    }
    
    #region Image settings

    public static async Task ToggleSideBySide(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }
        
        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            TurnOffSideBySide(vm);
        }
        else
        {
            await TurnOnSideBySide(vm);
        }

        await SaveSettingsAsync();
    }

    public static void TurnOffSideBySide(MainViewModel vm)
    {
        Settings.ImageScaling.ShowImageSideBySide = false;
        vm.IsShowingSideBySide = false;
        vm.SecondaryImageSource = null;
        WindowResizing.SetSize(vm);
        TitleManager.SetTitle(vm);
    }
    
    public static async Task TurnOnSideBySide(MainViewModel vm)
    {
        Settings.ImageScaling.ShowImageSideBySide = true;
        vm.IsShowingSideBySide = true;
        if (NavigationManager.CanNavigate(vm))
        {
            var preloadValue = await NavigationManager.GetNextPreLoadValueAsync();
            if (preloadValue is null)
            {
#if DEBUG
                Console.WriteLine($"{nameof(TurnOnSideBySide)} {nameof(preloadValue)} is null");       
#endif
                return;
            }
            vm.SecondaryImageSource = preloadValue.ImageModel.Image;
            var imageModel1 = new ImageModel
            {
                FileInfo = vm.FileInfo,
                PixelWidth = (int)vm.ImageWidth,
                PixelHeight = (int)vm.ImageHeight,
                ImageType = vm.ImageType,
                Image = vm.ImageSource,
                EXIFOrientation = vm.ExifOrientation
            };
            var imageModel2 = new ImageModel
            {
                FileInfo = preloadValue.ImageModel.FileInfo,
                PixelWidth = preloadValue.ImageModel.PixelWidth,
                PixelHeight = preloadValue.ImageModel.PixelHeight,
                ImageType = preloadValue.ImageModel.ImageType,
                Image = preloadValue.ImageModel.Image,
                EXIFOrientation = preloadValue.ImageModel.EXIFOrientation
            };
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WindowResizing.SetSize(vm.ImageWidth, vm.ImageHeight, preloadValue.ImageModel.PixelWidth,
                    preloadValue.ImageModel.PixelHeight, vm.RotationAngle, vm);
                TitleManager.SetSideBySideTitle(vm, imageModel1, imageModel2);
            });
        }
    }
    
    public static async Task ToggleScroll(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }
        
        if (Settings.Zoom.ScrollEnabled)
        {
            TurnOffScroll(vm);
        }
        else
        {
            TurnOnScroll(vm);
        }
        
        WindowResizing.SetSize(vm);
        
        await SaveSettingsAsync();
    }
    
    public static void TurnOffScroll(MainViewModel vm)
    {
        vm.ToggleScrollBarVisibility = ScrollBarVisibility.Disabled;
        vm.GetIsScrollingTranslation = TranslationHelper.Translation.ScrollingDisabled;
        vm.IsScrollingEnabled = false;
        Settings.Zoom.ScrollEnabled = false;
        try
        {
            var clickArrowRight = UIHelper.GetMainView.MainGrid.FindControl<ClickArrowRight>("ClickArrowRight");
            clickArrowRight.Margin = new Thickness(0);
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine($"{nameof(TurnOnScroll)} {e.Message}");
#endif
        }
    }
    
    public static void TurnOnScroll(MainViewModel vm)
    {
        vm.ToggleScrollBarVisibility = ScrollBarVisibility.Visible;
        vm.GetIsScrollingTranslation = TranslationHelper.Translation.ScrollingEnabled;
        vm.IsScrollingEnabled = true;
        Settings.Zoom.ScrollEnabled = true;
        try
        {
            var clickArrowRight = UIHelper.GetMainView.MainGrid.FindControl<ClickArrowRight>("ClickArrowRight");
            clickArrowRight.Margin = new Thickness(0,0,30,0);
        }
        catch (Exception e)
        {
#if DEBUG
            Console.WriteLine($"{nameof(TurnOnScroll)} {e.Message}");
#endif
        }
    }
    
    public static async Task ToggleCtrlZoom(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }
        
        Settings.Zoom.CtrlZoom = !Settings.Zoom.CtrlZoom;
        vm.GetIsCtrlZoomTranslation = Settings.Zoom.CtrlZoom
            ? TranslationHelper.Translation.CtrlToZoom
            : TranslationHelper.Translation.ScrollToZoom;
        
        // Set source for ChangeCtrlZoomImage
        if (!Application.Current.TryGetResource("ScanEyeImage", Application.Current.RequestedThemeVariant, out var scanEyeImage ))
        {
            return;
        }
        if (!Application.Current.TryGetResource("LeftRightArrowsImage", Application.Current.RequestedThemeVariant, out var leftRightArrowsImage ))
        {
            return;
        }
        var isNavigatingWithCtrl = Settings.Zoom.CtrlZoom;
        vm.ChangeCtrlZoomImage = isNavigatingWithCtrl ? leftRightArrowsImage as DrawingImage : scanEyeImage as DrawingImage;
        await SaveSettingsAsync().ConfigureAwait(false);
    }
    
    public static void TurnOffCtrlZoom(MainViewModel vm)
    {
        Settings.Zoom.CtrlZoom = false;
        vm.GetIsCtrlZoomTranslation = TranslationHelper.Translation.ScrollToZoom;
        if (!Application.Current.TryGetResource("ScanEyeImage", Application.Current.RequestedThemeVariant, out var scanEyeImage ))
        {
            return;
        }
        vm.ChangeCtrlZoomImage = scanEyeImage as DrawingImage;
    }
    
    
    
    public static async Task ToggleLooping(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }
        
        var value = !Settings.UIProperties.Looping;
        Settings.UIProperties.Looping = value;
        vm.GetIsLoopingTranslation = value
            ? TranslationHelper.Translation.LoopingEnabled
            : TranslationHelper.Translation.LoopingDisabled;
        vm.IsLooping = value;

        var msg = value
            ? TranslationHelper.Translation.LoopingEnabled
            : TranslationHelper.Translation.LoopingDisabled;
        await TooltipHelper.ShowTooltipMessageAsync(msg);

        await SaveSettingsAsync();
    }
    
    public static void TurnOffLooping(MainViewModel vm)
    {
        Settings.UIProperties.Looping = false;
        vm.GetIsLoopingTranslation = TranslationHelper.Translation.LoopingDisabled;
        vm.IsLooping = false;
    }
    
    #endregion
}
