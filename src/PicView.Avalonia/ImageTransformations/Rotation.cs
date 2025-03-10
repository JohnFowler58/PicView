using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views.UC.Menus;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Gallery;

namespace PicView.Avalonia.ImageTransformations;
public static class Rotation
{
    public enum RotationButton
    {
        WindowBorderButton,
        RotateRightButton,
        RotateLeftButton
    }

    public static async Task RotateRight(MainViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => { vm.ImageViewer.Rotate(false); });
    }
    
    public static async Task RotateRight(MainViewModel? vm, RotationButton rotationButton)
    {
        await RotateRight(vm);
        
        // Check if it should move the cursor
        if (!Settings.WindowProperties.AutoFit)
        {
            return;
        }

        await MoveCursorAfterRotation(vm, rotationButton);
    }

    private static async Task MoveCursorAfterRotation(MainViewModel? vm, RotationButton rotationButton)
    {
        // Move cursor when button is clicked
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                Button? button;
                ImageMenu? menu;
                switch (rotationButton)
                {
                    case RotationButton.WindowBorderButton:
                        button = UIHelper.GetTitlebar.GetControl<Button>("RotateRightButton");
                        break;
                    case RotationButton.RotateRightButton:
                        menu = UIHelper.GetMainView.MainGrid.Children.OfType<ImageMenu>().FirstOrDefault();
                        button = menu?.GetControl<Button>("RotateRightButton");
                        break;
                    case RotationButton.RotateLeftButton:
                        menu = UIHelper.GetMainView.MainGrid.Children.OfType<ImageMenu>().FirstOrDefault();
                        button = menu?.GetControl<Button>("RotateLeftButton");
                        break;
                    default:
                        return;
                }

                if (button is null || !button.IsPointerOver)
                {
                    return;
                }

                var p = button.PointToScreen(new Point(10, 15));
                vm.PlatformService?.SetCursorPos(p.X, p.Y);
            }
#if DEBUG
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            #else
            catch (Exception) { }
#endif
        });
    }
            
    public static async Task RotateLeft(MainViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            return;
        }
        await Dispatcher.UIThread.InvokeAsync(() => { vm.ImageViewer.Rotate(true); });
    }

    public static async Task RotateLeft(MainViewModel vm, RotationButton rotationButton)
    {
        await RotateLeft(vm);
        
        // Check if it should move the cursor
        if (!Settings.WindowProperties.AutoFit)
        {
            return;
        }
        await MoveCursorAfterRotation(vm, rotationButton);
    }
    
    public static void Flip(MainViewModel vm)
    {
        if (vm.ScaleX == 1)
        {
            vm.ScaleX = -1;
            vm.GetIsFlippedTranslation = vm.UnFlip;
        }
        else
        {
            vm.ScaleX = 1;
            vm.GetIsFlippedTranslation = vm.Flip;
        }

        Dispatcher.UIThread.Invoke(() => { vm.ImageViewer.Flip(true); });
    }
    
    /// <summary>
    /// Navigates up or rotates the image based on current state
    /// </summary>
    public static async Task NavigateUp(MainViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryNavigation.NavigateGallery(Direction.Up, vm);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => 
        {
            if (vm.IsScrollingEnabled)
            {
                vm.ImageViewer.ImageScrollViewer.LineUp();
            }
            else
            {
                vm.ImageViewer.Rotate(true);
            }
        });
    }

    /// <summary>
    /// Navigates down or rotates the image based on current state
    /// </summary>
    public static async Task NavigateDown(MainViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }

        if (GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryNavigation.NavigateGallery(Direction.Down, vm);
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => 
        {
            if (vm.IsScrollingEnabled)
            {
                vm.ImageViewer.ImageScrollViewer.LineDown();
            }
            else
            {
                vm.ImageViewer.Rotate(false);
            }
        });
    }

    /// <summary>
    /// Centers the window or gallery based on current state
    /// </summary>
    public static void Center(MainViewModel? vm)
    {
        if (vm is null)
        {
            return;
        }
        
        if (GalleryFunctions.IsFullGalleryOpen)
        {
            GalleryFunctions.CenterGallery(vm);
        }
        else
        {
            WindowFunctions.CenterWindowOnScreen();
        }
    }
}
