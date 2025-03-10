using Avalonia.Layout;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using PicView.Avalonia.ImageHandling;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views.UC;
using PicView.Core.Gallery;
using PicView.Core.Localization;

namespace PicView.Avalonia.Gallery;

public static class GalleryFunctions
{
    public static bool RenameGalleryItem(int oldIndex, int newIndex, string newFileLocation, string newName, MainViewModel? vm)
    {
        var mainView = UIHelper.GetMainView;

        var galleryListBox = mainView.GalleryView.GalleryListBox;
        if (galleryListBox == null)
        {
            return false;
        }

        if (galleryListBox.Items.Count <= oldIndex)
        {
            return false;
        }

        if (galleryListBox.Items.Count < 0 || oldIndex >= galleryListBox.ItemCount)
        {
            return false;
        }

        if (galleryListBox.Items.Count <= 0 || oldIndex >= galleryListBox.Items.Count)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return Rename();
        }
        Dispatcher.UIThread.InvokeAsync(Rename);

        if (vm != null)
        {
            vm.SelectedGalleryItemIndex = NavigationManager.GetCurrentIndex;
        }

        return true;

        bool Rename()
        {
            if (galleryListBox.Items[oldIndex] is not GalleryItem galleryItem)
            {
                return false;
            }
            galleryItem.FileName.Text = newName;
            galleryItem.FileLocation.Text = newFileLocation;
            galleryListBox.Items.RemoveAt(oldIndex);
            galleryListBox.Items.Insert(newIndex, galleryItem);
            return true;
        }
    }
    
    public static bool RemoveGalleryItem(int index, MainViewModel? vm)
    {
        var mainView = UIHelper.GetMainView;

        var galleryListBox = mainView.GalleryView.GalleryListBox;
        if (galleryListBox == null)
        {
            return false;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Removal();
        }
        else
        {
            Dispatcher.UIThread.InvokeAsync(Removal);
        }

        if (vm != null)
        {
            vm.SelectedGalleryItemIndex = NavigationManager.GetCurrentIndex;
        }

        return true;

        void Removal()
        {
            var removalIndex = galleryListBox.Items.Count > index ? index : galleryListBox.Items.Count - 1;
            if (removalIndex <= -1)
            {
                return;
            }
            if (galleryListBox.Items[removalIndex] is not GalleryItem galleryItem)
            {
                return;
            }
            galleryListBox.Items.Remove(galleryItem);
            if (galleryItem.GalleryImage.Source is IDisposable galleryImage)
            {
                galleryImage.Dispose();
            }
        }
    }

    public static async Task<bool> AddGalleryItem(int index, FileInfo fileInfo, MainViewModel? vm)
    {
        var mainView = UIHelper.GetMainView;

        var galleryListBox = mainView.GalleryView.GalleryListBox;
        if (galleryListBox == null)
        {
            return false;
        }

        GalleryItem? galleryItem;
        var thumb = await GetThumbnails.GetThumbAsync(fileInfo.FullName, (uint)vm.GetGalleryItemHeight, fileInfo);
        var galleryThumbInfo = GalleryThumbInfo.GalleryThumbHolder.GetThumbData(fileInfo);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                galleryItem = new GalleryItem
                {
                    FileLocation =
                    {
                        Text = galleryThumbInfo.FileLocation
                    },
                    FileDate =
                    {
                        Text = galleryThumbInfo.FileDate
                    },
                    FileSize =
                    {
                        Text = galleryThumbInfo.FileSize
                    },
                    FileName =
                    {
                        Text = galleryThumbInfo.FileName
                    }
                };
                galleryItem.PointerPressed += async (_, _) =>
                {
                    if (IsFullGalleryOpen)
                    {
                        ToggleGallery(vm);
                    }

                    await NavigationManager.Navigate(fileInfo.FullName, vm).ConfigureAwait(false);
                };
                if (galleryListBox.Items.Count > index)
                {
                    galleryListBox.Items.Insert(index, galleryItem);
                }
                else
                {
                    galleryListBox.Items.Add(galleryItem);
                }
                
                var isSvg = fileInfo.Extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
                            fileInfo.Extension.Equals(".svgz", StringComparison.OrdinalIgnoreCase);
                if (isSvg)
                {
                    galleryItem.GalleryImage.Source = new SvgImage
                        { Source = SvgSource.Load(fileInfo.FullName) };
                }
                else if (thumb is not null)
                {
                    galleryItem.GalleryImage.Source = thumb;
                }
            }, DispatcherPriority.Render);
            return true;
        }
        catch (Exception exception)
        {
#if DEBUG
            Console.WriteLine(exception);
#endif
        }

        return false;
    }

    public static void Clear()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ClearItems();
        }
        else
        {
            Dispatcher.UIThread.Post(ClearItems);
        }

        return;

        void ClearItems()
        {
            try
            {
                var mainView = UIHelper.GetMainView;

                var galleryListBox = mainView?.GalleryView.GalleryListBox;
                if (galleryListBox == null)
                {
                    return;
                }

                for (var i = 0; i < galleryListBox.ItemCount; i++)
                {
                    if (galleryListBox.Items[i] is not GalleryItem galleryItem)
                    {
                        continue;
                    }

                    if (galleryItem.GalleryImage.Source is IDisposable galleryImage)
                    {
                        galleryImage.Dispose();
                    }

                    galleryListBox.Items.Remove(galleryItem);
                }

                galleryListBox.Items.Clear();
#if DEBUG
                Console.WriteLine("Gallery items cleared");
#endif
            }
            catch (Exception e)
            {
#if DEBUG
                Console.WriteLine(e);
#endif
            }
        }
    }

    public static void CenterGallery(MainViewModel vm)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            Center();
        }
        else
        {
            Dispatcher.UIThread.Post(Center);
        }
        
        return;

        void Center()
        {
            var mainView = UIHelper.GetMainView;

            var galleryListBox = mainView.GalleryView.GalleryListBox;
            if (vm.SelectedGalleryItemIndex < 0 || vm.SelectedGalleryItemIndex >= galleryListBox.Items.Count)
            {
                return;
            }
            if (galleryListBox.Items[vm.SelectedGalleryItemIndex] is GalleryItem centerItem)
            {
                galleryListBox.ScrollToCenterOfItem(centerItem);
            }
        }
    }

    #region Gallery toggle

    public static bool IsFullGalleryOpen { get; private set; }
    public static bool IsBottomGalleryOpen { get; private set; }

    public static void ToggleGallery(MainViewModel vm)
    {
        if (vm is null || !NavigationManager.CanNavigate(vm))
        {
            return;
        }

        MenuManager.CloseMenus(vm);
        if (Settings.Gallery.IsBottomGalleryShown)
        {
            // Showing bottom gallery is enabled
            IsBottomGalleryOpen = true;
            if (IsFullGalleryOpen)
            {
                // Switch to bottom gallery
                IsFullGalleryOpen = false;
                vm.GalleryMode = GalleryMode.FullToBottom;
                vm.GetGalleryItemHeight = vm.GetBottomGalleryItemHeight;
            }
            else
            {
                // Switch to full gallery
                IsFullGalleryOpen = true;
                vm.GalleryMode = GalleryMode.BottomToFull;
                vm.GetGalleryItemHeight = vm.GetFullGalleryItemHeight;
            }
        }
        else
        {
            IsBottomGalleryOpen = false;
            if (IsFullGalleryOpen)
            {
                // close full gallery
                IsFullGalleryOpen = false;
                vm.GalleryMode = GalleryMode.FullToClosed;
            }
            else
            {
                // open full gallery
                IsFullGalleryOpen = true;
                vm.GalleryMode = GalleryMode.ClosedToFull;
                vm.GetGalleryItemHeight = vm.GetFullGalleryItemHeight;
            }
        }

        
        _ = Task.Run(() => GalleryLoad.LoadGallery(vm, NavigationManager.GetInitialFileInfo?.DirectoryName));
    }

    public static void OpenCloseBottomGallery(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        MenuManager.CloseMenus(vm);

        if (Settings.Gallery.IsBottomGalleryShown)
        {
            vm.GalleryMode = GalleryMode.BottomToClosed;
            vm.GetIsShowingBottomGalleryTranslation = TranslationHelper.Translation.ShowBottomGallery;
            Settings.Gallery.IsBottomGalleryShown = false;
            IsFullGalleryOpen = false;
            IsBottomGalleryOpen = false;
            return;
        }

        IsBottomGalleryOpen = true;
        IsFullGalleryOpen = false;
        Settings.Gallery.IsBottomGalleryShown = true;
        if (NavigationManager.CanNavigate(vm))
        {
            vm.GalleryMode = GalleryMode.ClosedToBottom;
        }

        vm.GetIsShowingBottomGalleryTranslation = TranslationHelper.Translation.HideBottomGallery;
        if (!NavigationManager.CanNavigate(vm))
        {
            return;
        }

        Task.Run(() => GalleryLoad.LoadGallery(vm, NavigationManager.GetInitialFileInfo?.DirectoryName));
    }

    public static void OpenBottomGallery(MainViewModel vm)
    {
        IsBottomGalleryOpen = true;
        vm.GalleryMode = GalleryMode.ClosedToBottom;
        vm.GalleryVerticalAlignment = VerticalAlignment.Bottom;
    }

    public static void CloseGallery(MainViewModel vm)
    {
        if (IsFullGalleryOpen)
        {
            ToggleGallery(vm);
        }
        else
        {
            OpenCloseBottomGallery(vm);
        }
    }

    #endregion


}