using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageMagick;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.Preloading;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Gallery;
using PicView.Core.ImageDecoding;
using PicView.Core.Navigation;

namespace PicView.Avalonia.Navigation;

public static class UpdateImage
{
    #region Update source

    /// <summary>
    ///     Updates the image source in the main view model based on the specified index and preloaded values.
    /// </summary>
    /// <param name="vm">The main view model instance.</param>
    /// <param name="index">The index of the image to update.</param>
    /// <param name="imagePaths">The list of image paths to navigate through.</param>
    /// <param name="preLoadValue">The preloaded value of the current image.</param>
    /// <param name="nextPreloadValue">Optional: The preloaded value of the next image, used for side-by-side display.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task UpdateSource(MainViewModel vm, int index, List<string> imagePaths,
        PreLoadValue? preLoadValue,
        PreLoadValue? nextPreloadValue = null)
    {
        preLoadValue ??= await NavigationManager.GetPreLoadValueAsync(index).ConfigureAwait(false);
        if (preLoadValue is null)
        {
            await ErrorHandling.ReloadAsync(vm).ConfigureAwait(false);
            return;
        }
        if (preLoadValue.ImageModel?.Image is null && index == NavigationManager.GetCurrentIndex)
        {
            var fileInfo = preLoadValue.ImageModel?.FileInfo ?? new FileInfo(imagePaths[index]);
            preLoadValue.ImageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
        }

        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            nextPreloadValue ??= await NavigationManager.GetNextPreLoadValueAsync().ConfigureAwait(false);
            if (nextPreloadValue.ImageModel?.Image is null && index == NavigationManager.GetCurrentIndex)
            {
                var fileInfo = nextPreloadValue.ImageModel?.FileInfo ?? new FileInfo(NavigationManager.GetNextFileName);
                nextPreloadValue.ImageModel = await GetImageModel.GetImageModelAsync(fileInfo).ConfigureAwait(false);
            }
        }

        if (index != NavigationManager.GetCurrentIndex)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (index != NavigationManager.GetCurrentIndex)
            {
                return;
            }

            vm.ImageViewer.SetTransform(preLoadValue.ImageModel.EXIFOrientation);
            if (Settings.ImageScaling.ShowImageSideBySide)
            {
                vm.SecondaryImageSource = nextPreloadValue.ImageModel.Image;
            }

            vm.ImageSource = preLoadValue.ImageModel.Image;
            if (preLoadValue.ImageModel.ImageType is ImageType.AnimatedGif or ImageType.AnimatedWebp)
            {
                vm.ImageViewer.MainImage.InitialAnimatedSource = preLoadValue.ImageModel.FileInfo.FullName;
            }

            vm.ImageType = preLoadValue.ImageModel.ImageType;

            if (!Settings.Zoom.ScrollEnabled)
            {
                SetSize();
            }

            UIHelper.GetToolTipMessage.IsVisible = false;
        }, DispatcherPriority.Send);

        vm.IsLoading = false;

        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            TitleManager.SetSideBySideTitle(vm, preLoadValue.ImageModel, nextPreloadValue?.ImageModel);
        }
        else
        {
            if (TiffManager.IsTiff(preLoadValue.ImageModel.FileInfo.FullName))
            {
                if (TiffManager.IsTiff(preLoadValue.ImageModel.FileInfo.FullName))
                {
                    TitleManager.TrySetTiffTitle(preLoadValue.ImageModel, vm);
                }
                else
                {
                    TitleManager.SetTitle(vm, preLoadValue.ImageModel);
                }
            }
            else
            {
                TitleManager.SetTitle(vm, preLoadValue.ImageModel);
            }
        }
        
        if (Settings.Zoom.ScrollEnabled)
        {
            // Bad fix for scrolling
            // TODO: Implement proper scrolling fix
            Settings.Zoom.ScrollEnabled = false;
            await Dispatcher.UIThread.InvokeAsync(SetSize);
            Settings.Zoom.ScrollEnabled = true;
            await Dispatcher.UIThread.InvokeAsync(SetSize, DispatcherPriority.Send);
        }

        if (Settings.WindowProperties.KeepCentered)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { WindowFunctions.CenterWindowOnScreen(); });
        }

        if (vm.SelectedGalleryItemIndex != index)
        {
            vm.SelectedGalleryItemIndex = index;
            if (Settings.Gallery.IsBottomGalleryShown)
            {
                GalleryNavigation.CenterScrollToSelectedItem(vm);
            }
        }

        SetStats(vm, index, preLoadValue.ImageModel);
        
        return;

        void SetSize()
        {
            WindowResizing.SetSize(preLoadValue.ImageModel.PixelWidth, preLoadValue.ImageModel.PixelHeight,
                nextPreloadValue?.ImageModel?.PixelWidth ?? 0, nextPreloadValue?.ImageModel?.PixelHeight ?? 0,
                preLoadValue.ImageModel.Rotation, vm);
        }

    }

    #endregion

    #region TIFF

    /// <summary>
    ///     Sets the image displayed in the view to the given TIFF image based on the given navigation info.
    /// </summary>
    /// <param name="tiffNavigationInfo">The navigation info for the TIFF image.</param>
    /// <param name="index">The index of the image to display.</param>
    /// <param name="fileInfo">The FileInfo object representing the file containing the image.</param>
    /// <param name="vm">The main view model instance.</param>
    public static void SetTiffImage(TiffManager.TiffNavigationInfo tiffNavigationInfo, int index, FileInfo fileInfo,
        MainViewModel vm)
    {
        ExecuteTiffImage(tiffNavigationInfo, index, fileInfo, vm).GetAwaiter().GetResult();
    }
    
    
    /// <inheritdoc cref="SetTiffImage(TiffManager.TiffNavigationInfo,int,FileInfo,MainViewModel)" />
    public static async Task SetTiffImageAsync(TiffManager.TiffNavigationInfo tiffNavigationInfo, int index, FileInfo fileInfo,
        MainViewModel vm)
    {
        await ExecuteTiffImage(tiffNavigationInfo, index, fileInfo, vm);
    }
    
    /// <inheritdoc cref="SetTiffImage(TiffManager.TiffNavigationInfo,int,FileInfo,MainViewModel)" />
    private static async Task ExecuteTiffImage(TiffManager.TiffNavigationInfo tiffNavigationInfo, int index, FileInfo fileInfo,
        MainViewModel vm)
    {
        var source = await Task.Run( () => tiffNavigationInfo.Pages[tiffNavigationInfo.CurrentPage].ToWriteableBitmap()).ConfigureAwait(false);
        vm.ImageSource = source;
        vm.SecondaryImageSource = null;
        vm.ImageType = ImageType.Bitmap;
        var width = source?.PixelSize.Width ?? 0;
        var height = source?.PixelSize.Height ?? 0;
        
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (vm.CurrentView != vm.ImageViewer)
            {
                vm.CurrentView = vm.ImageViewer;
            }
            
            WindowResizing.SetSize(width, height, 0, 0, 0, vm);
            
            if (vm.RotationAngle != 0)
            {
                vm.ImageViewer.Rotate(vm.RotationAngle);
            }
        }, DispatcherPriority.Render);
        
        TitleManager.SetTiffTitle(tiffNavigationInfo, width, height, index, fileInfo, vm);

        var imageModel = new ImageModel
        {
            EXIFOrientation = EXIFHelper.GetImageOrientation(fileInfo),
            ImageType = ImageType.Bitmap,
            FileInfo = fileInfo,
            Image = source,
            PixelWidth = width,
            PixelHeight = height
        };
        SetStats(vm, index, imageModel);
    }

    #endregion

    #region Single Image

    /// <summary>
    ///     Sets the given image as the single image displayed in the view.
    /// </summary>
    /// <param name="source">The source of the image to display.</param>
    /// <param name="imageType"></param>
    /// <param name="name">The name of the image.</param>
    /// <param name="vm">The main view model instance.</param>
    public static void SetSingleImage(object source, ImageType imageType, string name, MainViewModel vm)
    {
        SetSingleImageAsync(source, imageType, name, vm).GetAwaiter().GetResult();
    }

    /// <inheritdoc cref="SetSingleImage" />
    public static async Task SetSingleImageAsync(object source, ImageType imageType, string name, MainViewModel vm)
    {
        await ExecuteSetSingleImageAsync(
            source,
            imageType,
            name,
            vm,
            async (action, priority) => { await Dispatcher.UIThread.InvokeAsync(action, priority); });
    }

    /// <summary>
    ///     Internal method that sets a single image as the source of the image viewer.
    /// </summary>
    /// <param name="source">The source of the image to display.</param>
    /// <param name="imageType">The type of the image.</param>
    /// <param name="name">The name of the image.</param>
    /// <param name="vm">The main view model instance.</param>
    /// <param name="dispatchAction">A function that dispatches an action to the UI thread.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private static async Task ExecuteSetSingleImageAsync(
        object source,
        ImageType imageType,
        string name,
        MainViewModel vm,
        Func<Action, DispatcherPriority, Task> dispatchAction)
    {
        await dispatchAction(() =>
        {
            if (vm.CurrentView != vm.ImageViewer)
            {
                vm.CurrentView = vm.ImageViewer;
            }
        }, DispatcherPriority.Render);

        
        int width, height;
        if (imageType is ImageType.Svg)
        {
            var path = source as string;
            using var magickImage = new MagickImage();
            magickImage.Ping(path);
            vm.ImageSource = source;
            vm.ImageType = ImageType.Svg;
            width = (int)magickImage.Width;
            height = (int)magickImage.Height;
        }
        else
        {
            var bitmap = source as Bitmap;
            vm.ImageSource = source;
            vm.ImageType = imageType == ImageType.Invalid ? ImageType.Bitmap : imageType;
            width = bitmap?.PixelSize.Width ?? 0;
            height = bitmap?.PixelSize.Height ?? 0;
        }

        vm.FileInfo = null;

        await dispatchAction(() => { WindowResizing.SetSize(width, height, 0, 0, 0, vm); }, DispatcherPriority.Send);

        var singeImageWindowTitles = ImageTitleFormatter.GenerateTitleForSingleImage(width, height, name, 1);
        vm.WindowTitle = singeImageWindowTitles.TitleWithAppName;
        vm.Title = singeImageWindowTitles.BaseTitle;
        vm.TitleTooltip = singeImageWindowTitles.BaseTitle;

        vm.PlatformService.StopTaskbarProgress();

        vm.PixelWidth = width;
        vm.PixelHeight = height;

        if (Settings.Gallery.IsBottomGalleryShown)
        {
            vm.GalleryMode = GalleryMode.Closed;
            vm.GalleryMargin = new Thickness(0);
        }

        await dispatchAction(() => { UIHelper.GetGalleryView.IsVisible = false; }, DispatcherPriority.Render);
        await NavigationManager.DisposeImageIteratorAsync();
    }

    #endregion

    #region Set stats

    public static void SetStats(MainViewModel vm, int index, ImageModel imageModel)
    {
        vm.PixelWidth = imageModel.PixelWidth;
        vm.PixelHeight = imageModel.PixelHeight;
        vm.GetIndex = index + 1;
        vm.ExifOrientation = imageModel.EXIFOrientation;
        vm.FileInfo = imageModel.FileInfo;
        vm.ZoomValue = 1;

        if (Settings.ImageScaling.ShowImageSideBySide)
        {
            // Fixes incorrect rendering in the side by side view
            // TODO: Improve and fix side by side and remove this hack 
            Dispatcher.UIThread.Post(() => { vm.ImageViewer?.MainImage?.InvalidateVisual(); });
        }

        // Reset effects
        vm.EffectConfig = null;
    }

    #endregion
}