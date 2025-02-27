using Avalonia.Threading;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.Preloading;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.FileHandling;
using PicView.Core.Gallery;
using PicView.Core.ImageDecoding;

namespace PicView.Avalonia.StartUp;

public static class QuickLoad
{
    public static async Task QuickLoadAsync(MainViewModel vm, string file)
    {
        var fileInfo = new FileInfo(file);
        if (!fileInfo.Exists) // If not file, try to load if URL, base64 or directory
        {
            await NavigationManager.LoadPicFromStringAsync(file, vm).ConfigureAwait(false);
            return;
        }

        if (file.IsArchive()) // Handle if file exist and is an archive
        {
            await NavigationManager.LoadPicFromArchiveAsync(file, vm).ConfigureAwait(false);
            return;
        }
        vm.FileInfo = fileInfo;
        
        var imageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
        
        if (imageModel.ImageType is ImageType.AnimatedGif or ImageType.AnimatedWebp)
        {
            vm.ImageViewer.MainImage.InitialAnimatedSource = file;
        }
        vm.ImageSource = imageModel.Image;
        vm.ImageType = imageModel.ImageType;
        vm.ZoomValue = 1;
        vm.PixelWidth = imageModel.PixelWidth;
        vm.PixelHeight = imageModel.PixelHeight;
        PreLoadValue? secondaryPreloadValue = null;
        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            NavigationManager.InitializeImageIterator(vm);
            secondaryPreloadValue = await NavigationManager.GetNextPreLoadValueAsync();
            vm.SecondaryImageSource = secondaryPreloadValue?.ImageModel?.Image;
        }
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            vm.ImageViewer.SetTransform(imageModel.EXIFOrientation);
            if (!Settings.Zoom.ScrollEnabled)
            {
                WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, secondaryPreloadValue?.ImageModel?.PixelWidth ?? 0, secondaryPreloadValue?.ImageModel?.PixelHeight ?? 0, imageModel.Rotation, vm);
            }

            if (Settings.WindowProperties.AutoFit)
            {
                WindowFunctions.CenterWindowOnScreen();
            }
            else if (vm.PixelWidth > vm.ImageViewer.Bounds.Width)
            {
                // Fixes weird bug where the image is not rendered correctly
                // TODO: check if this will still be needed in future Avalonia versions
                if (vm.PixelHeight > vm.ImageViewer.Bounds.Height)
                {
                    vm.ImageViewer.MainBorder.Height = vm.ImageViewer.ImageScrollViewer.Bounds.Height;
                }

                vm.ImageViewer.MainBorder.Width = vm.ImageViewer.ImageScrollViewer.Bounds.Width;

                WindowResizing.SetSize(vm);
            }
        }, DispatcherPriority.Send);

        vm.IsLoading = false;
        
        NavigationManager.InitializeImageIterator(vm);
        vm.GetIndex = NavigationManager.GetNonZeroIndex;

        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            SetTitleHelper.SetSideBySideTitle(vm, imageModel, secondaryPreloadValue?.ImageModel);
            
            // Sometimes the images are not rendered in side by side, this fixes it
            // TODO: Improve and fix side by side and remove this hack 
            Dispatcher.UIThread.Post(() =>
            {
                vm.ImageViewer?.MainImage?.InvalidateVisual();
            });
        }
        else
        {
            if (TiffManager.IsTiff(imageModel.FileInfo.FullName))
            {
                SetTitleHelper.TrySetTiffTitle(imageModel.PixelWidth, imageModel.PixelHeight, NavigationManager.GetCurrentIndex, fileInfo, vm);
            }
            else
            {
                SetTitleHelper.SetTitle(vm, imageModel);
            }
        }
        
        vm.ExifOrientation = imageModel.EXIFOrientation;
        
        if (Settings.Zoom.ScrollEnabled)
        {
            // Bad fix for scrolling
            // TODO: Implement proper startup scrolling fix
            Settings.Zoom.ScrollEnabled = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, secondaryPreloadValue?.ImageModel?.PixelWidth ?? 0, secondaryPreloadValue?.ImageModel?.PixelHeight ?? 0, imageModel.Rotation, vm);
            }, DispatcherPriority.Background);
            Settings.Zoom.ScrollEnabled = true;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, secondaryPreloadValue?.ImageModel?.PixelWidth ?? 0, secondaryPreloadValue?.ImageModel?.PixelHeight ?? 0, imageModel.Rotation, vm);
            }, DispatcherPriority.Send);
        }
        else if (!Settings.WindowProperties.AutoFit)
        {
            if (vm.PixelWidth > vm.ImageViewer.Bounds.Width)
            {
                // Fixes weird bug where the image is not rendered correctly
                // TODO: check if this will still be needed in future Avalonia versions
                vm.ImageSource = imageModel.Image;
                vm.SecondaryImageSource = secondaryPreloadValue?.ImageModel?.Image;
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WindowResizing.SetSize(imageModel.PixelWidth, imageModel.PixelHeight, secondaryPreloadValue?.ImageModel?.PixelWidth ?? 0, secondaryPreloadValue?.ImageModel?.PixelHeight ?? 0, imageModel.Rotation, vm);
                }, DispatcherPriority.Send);
            }
        }
        
        // Add recent files, except when browsing archive
        if (string.IsNullOrWhiteSpace(TempFileHelper.TempFilePath))
        {
            FileHistoryNavigation.Add(fileInfo.FullName);
        }

        NavigationManager.AddToPreloader(NavigationManager.GetCurrentIndex, imageModel);
        
        var tasks = new List<Task>();
        
        if (NavigationManager.GetCount > 1)
        {
            if (Settings.UIProperties.IsTaskbarProgressEnabled)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    vm.PlatformService.SetTaskbarProgress((ulong)NavigationManager.GetCurrentIndex, (ulong)NavigationManager.GetCount);
                });
            }

            tasks.Add(NavigationManager.PreloadAsync());
        }

        if (Settings.Gallery.IsBottomGalleryShown)
        {
            if (vm.IsUIShown)
            {
                vm.GalleryMode = GalleryMode.BottomNoAnimation;
                tasks.Add(GalleryLoad.LoadGallery(vm, fileInfo.DirectoryName));
            }
            else if (Settings.Gallery.ShowBottomGalleryInHiddenUI)
            {
                vm.GalleryMode = GalleryMode.BottomNoAnimation;
                tasks.Add(GalleryLoad.LoadGallery(vm, fileInfo.DirectoryName));
            }
            else if (Settings.WindowProperties.Fullscreen)
            {
                vm.GalleryMode = GalleryMode.BottomNoAnimation;
                tasks.Add(GalleryLoad.LoadGallery(vm, fileInfo.DirectoryName));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
