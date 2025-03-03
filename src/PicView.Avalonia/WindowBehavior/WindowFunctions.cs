using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using PicView.Avalonia.Input;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.ArchiveHandling;
using PicView.Core.Calculations;
using PicView.Core.FileHandling;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace PicView.Avalonia.WindowBehavior;

public static class WindowFunctions
{
    public static async Task WindowClosingBehavior()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }
        await WindowClosingBehavior(desktop.MainWindow);
    }
    public static async Task WindowClosingBehavior(Window window)
    {
        WindowResizing.SaveSize(window);

        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Hide();
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(window.Hide);
        }

        var vm = window.DataContext as MainViewModel;
        string lastFile;
        if (NavigationManager.CanNavigate(vm))
        {
            if (!string.IsNullOrEmpty(ArchiveExtraction.LastOpenedArchive))
            {
                lastFile = ArchiveExtraction.LastOpenedArchive;
            }
            else
            {
                lastFile = vm?.FileInfo?.FullName ?? FileHistoryNavigation.GetLastFile();
            }
        }
        else
        {
            var url = vm?.Title.GetURL();
            lastFile = !string.IsNullOrWhiteSpace(url) ? url : FileHistoryNavigation.GetLastFile();
        }

        Settings.StartUp.LastFile = lastFile;
        await SaveSettingsAsync();
        await KeybindingManager.UpdateKeyBindingsFile(); // Save keybindings
        FileDeletionHelper.DeleteTempFiles();
        FileHistoryNavigation.WriteToFile();
        ArchiveExtraction.Cleanup();
        Environment.Exit(0);
    }

    #region Window State

    public static void ShowMinimizedWindow(Window window)
    {
        window.BringIntoView();
        window.WindowState = WindowState.Normal;
        window.Activate();
        window.Focus();
    }

    public static async Task ToggleTopMost(MainViewModel vm)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (Settings.WindowProperties.TopMost)
        {
            vm.IsTopMost = false;
            desktop.MainWindow.Topmost = false;
            Settings.WindowProperties.TopMost = false;
        }
        else
        {
            vm.IsTopMost = true;
            desktop.MainWindow.Topmost = true;
            Settings.WindowProperties.TopMost = true;
        }

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task ToggleAutoFit(MainViewModel vm)
    {
        if (Settings.WindowProperties.AutoFit)
        {
            vm.SizeToContent = SizeToContent.Manual;
            vm.CanResize = true;
            Settings.WindowProperties.AutoFit = false;
            vm.IsAutoFit = false;
        }
        else
        {
            vm.SizeToContent = SizeToContent.WidthAndHeight;
            vm.CanResize = false;
            Settings.WindowProperties.AutoFit = true;
            vm.IsAutoFit = true;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (Settings.WindowProperties.AutoFit)
            {
                if (vm.PixelWidth > UIHelper.GetMainView.Bounds.Width || vm.PixelHeight > UIHelper.GetMainView.Bounds.Height)
                {
                    vm.ImageViewer.MainBorder.Height = double.NaN;
                    vm.ImageViewer.MainBorder.Width = double.NaN;

                    WindowResizing.SetSize(1, 1, 0, 0, 0, vm);
                }
                else
                {
                    WindowResizing.SetSize(vm);
                }
                CenterWindowOnScreen(false);
            }
            else
            {
                WindowResizing.SetSize(vm);
            }
            
            if (Settings.WindowProperties.AutoFit)
            {
                if (vm.PixelWidth > UIHelper.GetMainView.Bounds.Width || vm.PixelHeight > UIHelper.GetMainView.Bounds.Height)
                {
                    Dispatcher.UIThread.Post(() => WindowResizing.SetSize(vm), DispatcherPriority.Render);
                }
            }
        }, DispatcherPriority.Send);

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task AutoFitAndStretch(MainViewModel vm)
    {
        if (Settings.WindowProperties.AutoFit)
        {
            vm.SizeToContent = SizeToContent.Manual;
            vm.CanResize = true;
            Settings.WindowProperties.AutoFit = false;
            Settings.ImageScaling.StretchImage = false;
            vm.IsStretched = false;
            vm.IsAutoFit = false;
        }
        else
        {
            vm.SizeToContent = SizeToContent.WidthAndHeight;
            vm.CanResize = false;
            Settings.WindowProperties.AutoFit = true;
            Settings.ImageScaling.StretchImage = true;
            vm.IsAutoFit = true;
            vm.IsStretched = true;
        }

        WindowResizing.SetSize(vm);
        await Dispatcher.UIThread.InvokeAsync(() => CenterWindowOnScreen(false));
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task NormalWindow(MainViewModel vm)
    {
        vm.SizeToContent = SizeToContent.Manual;
        vm.CanResize = true;
        Settings.WindowProperties.AutoFit = false;
        WindowResizing.SetSize(vm);
        vm.ImageViewer.MainImage.InvalidateVisual();
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task NormalWindowStretch(MainViewModel vm)
    {
        vm.SizeToContent = SizeToContent.Manual;
        vm.CanResize = true;
        Settings.WindowProperties.AutoFit = false;
        Settings.ImageScaling.StretchImage = true;
        vm.IsStretched = true;
        WindowResizing.SetSize(vm);
        vm.ImageViewer.MainImage.InvalidateVisual();
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task Stretch(MainViewModel vm)
    {
        Settings.ImageScaling.StretchImage = true;
        vm.IsStretched = true;
        WindowResizing.SetSize(vm);
        vm.ImageViewer.MainImage.InvalidateVisual();
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task ToggleFullscreen(MainViewModel vm, bool saveSettings = true)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (Settings.WindowProperties.Fullscreen)
        {
            vm.IsFullscreen = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
                desktop.MainWindow.WindowState = WindowState.Normal);
            if (saveSettings)
            {
                Settings.WindowProperties.Fullscreen = false;
            }

            WindowResizing.RestoreSize(desktop.MainWindow);
            Restore(vm, desktop);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                vm.BottomCornerRadius = new CornerRadius(0, 0, 8, 8);
                if (Settings.WindowProperties.AutoFit)
                {
                    CenterWindowOnScreen();
                }
                else
                {
                    InitializeWindowSizeAndPosition(desktop.MainWindow);
                }
            }
        }
        else
        {
            WindowResizing.SaveSize(desktop.MainWindow);
            Fullscreen(vm, desktop);
            if (saveSettings)
            {
                Settings.WindowProperties.Fullscreen = true;
            }
        }

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static async Task MaximizeRestore()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        var vm = desktop.MainWindow.DataContext as MainViewModel;
        // Restore
        if (desktop.MainWindow.WindowState is WindowState.Maximized or WindowState.FullScreen)
        {
            Restore(vm, desktop);
        }
        // Maximize
        else
        {
            if (!Settings.WindowProperties.AutoFit)
            {
                WindowResizing.SaveSize(desktop.MainWindow);
            }

            Maximize();
        }

        await SaveSettingsAsync().ConfigureAwait(false);
    }

    public static void Restore()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop || desktop.MainWindow.DataContext is not MainViewModel vm)
        {
            return;
        }

        Restore(vm, desktop);
    }

    public static void Restore(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        Settings.WindowProperties.Maximized = false;
        Settings.WindowProperties.Fullscreen = false;
        SetMargin();
        vm.IsMaximized = false;
        vm.IsFullscreen = false;
        
        if (Settings.UIProperties.ShowInterface)
        {
            vm.IsTopToolbarShown = true;
            vm.TitlebarHeight = SizeDefaults.TitlebarHeight;
            if (Settings.UIProperties.ShowBottomNavBar)
            {
                vm.IsBottomToolbarShown = true;
                vm.BottombarHeight = SizeDefaults.BottombarHeight;
            }

            vm.IsUIShown = true;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
            desktop.MainWindow.WindowState = WindowState.Normal);
        vm.IsUIShown = Settings.UIProperties.ShowInterface;
        InitializeWindowSizeAndPosition(desktop.MainWindow);
        
        if (Settings.WindowProperties.AutoFit)
        {
            vm.SizeToContent = SizeToContent.WidthAndHeight;
            vm.CanResize = false;
            CenterWindowOnScreen();
        }
        else
        {
            vm.SizeToContent = SizeToContent.Manual;
            vm.CanResize = true;
        }
        WindowResizing.SetSize(vm);
    }

    public static void Maximize()
    {
        // TODO: Fix incorrect size for bottom button bar

        if (Dispatcher.UIThread.CheckAccess())
        {
            Set();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(Set);
        }

        return;

        void Set()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                return;
            }

            var vm = desktop.MainWindow.DataContext as MainViewModel;
            if (Settings.WindowProperties.AutoFit)
            {
                vm.SizeToContent = SizeToContent.Manual;
                vm.CanResize = true;
            }

            desktop.MainWindow.WindowState = WindowState.Maximized;
            Settings.WindowProperties.Maximized = true;
            WindowResizing.SetSize(desktop.MainWindow.DataContext as MainViewModel);
            SetMargin();
            vm.IsMaximized = true;
            vm.IsFullscreen = false;
        }
    }
    
    private static void SetMargin()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (desktop.MainWindow.DataContext is not MainViewModel vm)
        {
            return;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (Settings.WindowProperties.Maximized)
            {
                // Sometimes margin is 0 when it's not supposed to be, so replace with 7. Not sure why.
                var left = desktop.MainWindow.OffScreenMargin.Left is 0 ? 7 : desktop.MainWindow.OffScreenMargin.Left;
                var top = desktop.MainWindow.OffScreenMargin.Top is 0 ? 7 : desktop.MainWindow.OffScreenMargin.Top;
                var right = desktop.MainWindow.OffScreenMargin.Right is 0 ? 7 : desktop.MainWindow.OffScreenMargin.Right;
                var bottom = desktop.MainWindow.OffScreenMargin.Bottom is 0 ? 7 : desktop.MainWindow.OffScreenMargin.Bottom;
                vm.TopScreenMargin = new Thickness(left, top, right, 0);
                vm.BottomScreenMargin = new Thickness(left, 0, right, bottom);
            }
            else
            {
                var noThickness = new Thickness(0);
                vm.TopScreenMargin = noThickness;
                vm.BottomScreenMargin = noThickness;
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            vm.BottomCornerRadius = Settings.WindowProperties.Maximized ? 
                new CornerRadius(0) :
                new CornerRadius(0, 0, 8, 8);
        }
    }

    public static void Fullscreen(MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        vm.SizeToContent = SizeToContent.Manual;
        vm.IsFullscreen = true;
        vm.IsMaximized = false;
        vm.CanResize = false;
        Dispatcher.UIThread.Invoke(() =>
        {
            desktop.MainWindow.WindowState = WindowState.FullScreen;
        });

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            vm.IsTopToolbarShown = false; // Hide interface in fullscreen. Remember to turn back when exiting fullscreen
            vm.IsBottomToolbarShown = false;
            vm.IsUIShown = false;
            Dispatcher.UIThread.Post(() =>
            {
                // Get the screen that the window is currently on
                var window = desktop.MainWindow;
                var screens = desktop.MainWindow.Screens;
                var screen = screens.ScreenFromVisual(window);

                if (screen == null)
                {
                    return; // No screen found (edge case)
                }

                // Get the scaling factor of the screen (DPI scaling)
                var scalingFactor = screen.Scaling;

                // Get the current screen's bounds (in physical pixels, not adjusted for scaling)
                var screenBounds = screen.Bounds;

                // Calculate the actual bounds in logical units (adjusting for scaling)
                var screenWidth = screenBounds.Width / scalingFactor;
                var screenHeight = screenBounds.Height / scalingFactor;

                // Get the size of the window
                var windowSize = window.ClientSize;

                // Calculate the position to center the window on the screen
                var centeredX = screenBounds.X + (screenWidth - windowSize.Width) / 2;
                var centeredY = screenBounds.Y + (screenHeight - windowSize.Height) / 2;

                // Set the window's new position
                window.Position = new PixelPoint((int)centeredX, (int)centeredY);
            });
            // TODO: Add Fullscreen mode for Windows (and maybe for Linux?)
            // macOS fullscreen version already works nicely
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            vm.BottomCornerRadius = new CornerRadius(0);
            WindowResizing.SetSize(vm);
            //var screen = ScreenHelper.ScreenSize;
            //vm.TitleMaxWidth = ImageSizeCalculationHelper.GetTitleMaxWidth(0, screen.WorkingAreaWidth, screen.WorkingAreaHeight, screen.WorkingAreaWidth, screen.WorkingAreaHeight, ImageSizeCalculationHelper.GetInterfaceSize(), screen.WorkingAreaHeight);
            if (Settings.WindowProperties.AutoFit)
            {
                // TODO go to macOS fullscreen mode when auto fit is on
            }
        }

        vm.GalleryWidth = double.NaN;
    }

    public static async Task Minimize()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            desktop.MainWindow.WindowState = WindowState.Minimized);
    }

    public static async Task Close()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            desktop.MainWindow.Close());
    }

    #endregion

    #region Window Size and Position

    public static void CenterWindowOnScreen(bool horizontal = true)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var window = desktop.MainWindow;

            // Get the screen that the window is currently on
            var screens = window.Screens;
            var screen = screens.ScreenFromVisual(window);

            if (screen == null)
            {
                return; // No screen found (edge case)
            }

            // Get the scaling factor of the screen (DPI scaling)
            var scalingFactor = screen.Scaling;

            // Get the current screen's bounds (in physical pixels, not adjusted for scaling)
            var screenBounds = screen.WorkingArea;

            // Calculate the actual bounds in logical units (adjusting for scaling)
            var screenWidth = screenBounds.Width / scalingFactor;
            var screenHeight = screenBounds.Height / scalingFactor;

            // Get the size of the window
            var windowSize = window.ClientSize;

            // Calculate the position to center the window on the screen
            var centeredX = screenBounds.X + (screenWidth - windowSize.Width) / 2;
            var centeredY = screenBounds.Y + (screenHeight - windowSize.Height) / 2;

            // Set the window's new position
            window.Position = horizontal
                ? new PixelPoint((int)centeredX, (int)centeredY)
                : new PixelPoint(window.Position.X, (int)centeredY);
        });
    }

    public static void InitializeWindowSizeAndPosition(Window window)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            window.Position = new PixelPoint((int)Settings.WindowProperties.Left,
                (int)Settings.WindowProperties.Top);
            window.Width = Settings.WindowProperties.Width;
            window.Height = Settings.WindowProperties.Height;
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                window.Position = new PixelPoint((int)Settings.WindowProperties.Left,
                    (int)Settings.WindowProperties.Top);
                window.Width = Settings.WindowProperties.Width;
                window.Height = Settings.WindowProperties.Height;
            });
        }
    }

    #endregion

    #region Window Drag and Behavior

    public static void WindowDragAndDoubleClickBehavior(Window window, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2 && e.GetCurrentPoint(window).Properties.IsLeftButtonPressed)
        {
            _ = MaximizeRestore();
            return;
        }

        var currentScreen = ScreenHelper.ScreenSize;
        window.BeginMoveDrag(e);
        var screen = window.Screens.ScreenFromVisual(window);
        if (screen == null)
        {
            return;
        }
        
        if (screen.WorkingArea.Width == currentScreen.WorkingAreaWidth &&
            screen.WorkingArea.Height == currentScreen.WorkingAreaHeight && screen.Scaling == currentScreen.Scaling)
        {
            return;
        }

        ScreenHelper.UpdateScreenSize(window);
        WindowResizing.SetSize(window.DataContext as MainViewModel);
    }

    public static void WindowDragBehavior(Window window, PointerPressedEventArgs e)
    {
        var currentScreen = ScreenHelper.ScreenSize;
        window.BeginMoveDrag(e);
        var screen = window.Screens.ScreenFromVisual(window);
        if (screen == null)
        {
            return;
        }

        if (screen.WorkingArea.Width == currentScreen.WorkingAreaWidth &&
            screen.WorkingArea.Height == currentScreen.WorkingAreaHeight && screen.Scaling == currentScreen.Scaling)
        {
            return;
        }

        ScreenHelper.UpdateScreenSize(window);
        WindowResizing.SetSize(window.DataContext as MainViewModel);
    }

    #endregion
}