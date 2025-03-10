using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using PicView.Avalonia.Converters;
using PicView.Avalonia.Crop;
using PicView.Avalonia.DragAndDrop;
using PicView.Avalonia.Input;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Extensions;
using PicView.Core.Navigation;

namespace PicView.Avalonia.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();

        Loaded += delegate
        {
            AddHandler(DragDrop.DragEnterEvent, DragEnter);
            AddHandler(DragDrop.DragLeaveEvent, DragLeave);
            AddHandler(DragDrop.DropEvent, Drop);

            GotFocus += CloseTitlebarIfOpen;
            LostFocus += HandleLostFocus;
            PointerPressed += PointerPressedBehavior;

            MainContextMenu.Opened += OnMainContextMenuOpened;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // TODO Implement setting as wallpaper for macOS
                WallpaperMenuItem.IsEnabled = false;
                
                MaximizeMenuItem.IsVisible = false;
            }
            
            if (DataContext is not MainViewModel vm)
            {
                return;
            }
            HideInterfaceLogic.AddHoverButtonEvents(AltButtonsPanel, vm);
            PointerWheelChanged += async (_, e) => await vm.ImageViewer.PreviewOnPointerWheelChanged(this, e);
        };
    }

    private void PointerPressedBehavior(object? sender, PointerPressedEventArgs e)
    {
        CloseTitlebarIfOpen(sender, e);
        if (MainKeyboardShortcuts.ShiftDown && !CropFunctions.IsCropping)
        {
            var hostWindow = (Window)VisualRoot!;
            WindowFunctions.WindowDragBehavior(hostWindow, e);
        }
        
        DragAndDropHelper.RemoveDragDropView();
    }
    
    private void CloseTitlebarIfOpen(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        if (!vm.IsEditableTitlebarOpen)
        {
            return;
        }

        vm.IsEditableTitlebarOpen = false;
        MainKeyboardShortcuts.IsKeysEnabled = true;
        Focus();
    }
    
    private static void HandleLostFocus(object? sender, EventArgs e)
    {
        DragAndDropHelper.RemoveDragDropView();
    }

    private void OnMainContextMenuOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        CropMenuItem.IsEnabled = CropFunctions.DetermineIfShouldBeEnabled(vm);
        ConversionHelper.DetermineIfOptimizeImageShouldBeEnabled(vm);

        // Set source for ChangeCtrlZoomImage
        if (!Application.Current.TryGetResource("ScanEyeImage", Application.Current.RequestedThemeVariant, out var scanEyeImage))
        {
            return;
        }
        if (!Application.Current.TryGetResource("LeftRightArrowsImage", Application.Current.RequestedThemeVariant, out var leftRightArrowsImage))
        {
            return;
        }
        var isNavigatingWithCtrl = Settings.Zoom.CtrlZoom;
        vm.ChangeCtrlZoomImage = isNavigatingWithCtrl ? leftRightArrowsImage as DrawingImage : scanEyeImage as DrawingImage;

        // Update file history menu items
        UpdateFileHistoryMenuItems(vm);
    }

    private void UpdateFileHistoryMenuItems(MainViewModel vm)
    {
        // Clear existing items 
        RecentFilesCM.Items.Clear();
        var currentFilePath = NavigationManager.GetCurrentFileName;
            
        // Add menu items for each history entry
        for (var i = 0; i < FileHistory.Count; i++)
        {
            var fileLocation = FileHistory.GetEntry(i);
            if (string.IsNullOrEmpty(fileLocation))
                continue;
                
            var isSelected = fileLocation == currentFilePath;
            var filename = Path.GetFileNameWithoutExtension(fileLocation);
            var header = filename.Length > 60 ? filename.Shorten(60) : filename;
            
            var item = new MenuItem
            {
                Header = header
            };
            if (isSelected)
            {
                item.Classes.Add("active");
            }
            
            var filePath = fileLocation; // Local copy for the closure
            item.Click += async delegate
            {
                await NavigationManager.LoadPicFromStringAsync(filePath, vm).ConfigureAwait(false);
            };
            
            ToolTip.SetTip(item, fileLocation);
            
            RecentFilesCM.Items.Add(item);
        }
        
        // TODO add clear history translations
        // Add a separator and "Clear history" option if there are items
        // if (FileHistory.Count <= 0)
        // {
        //     return;
        // }
        //
        // RecentFilesCM.Items.Add(new Separator());
        //     
        // var clearItem = new MenuItem { Header = TranslationHelper.GetTranslation("ClearHistory") };
        // clearItem.Click += delegate
        // {
        //     FileHistory.Clear();
        //     FileHistory.SaveToFile();
        //     RecentFilesCM.Items.Clear();
        // };
        //     
        // RecentFilesCM.Items.Add(clearItem);
    }

    private async Task Drop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }
        await DragAndDropHelper.Drop(e, vm);
    }
    
    private async Task DragEnter(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        await DragAndDropHelper.DragEnter(e, vm, this);
    }
    
    private void DragLeave(object? sender, DragEventArgs e)
    {
        DragAndDropHelper.DragLeave(e, this);
    }
}