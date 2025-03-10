using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.Views;
using PicView.Avalonia.Views.UC;

namespace PicView.Avalonia.UI;

/// <summary>
/// Provides UI-related helper methods and properties
/// </summary>
public static class UIHelper
{
    public static MainView? GetMainView { get; private set; }
    public static Control? GetTitlebar { get; private set; }
    public static EditableTitlebar? GetEditableTitlebar { get; private set; }
    public static GalleryAnimationControlView? GetGalleryView { get; private set; }
    public static BottomBar? GetBottomBar { get; private set; }
    public static ToolTipMessage? GetToolTipMessage { get; private set; }

    /// <summary>
    /// Sets up control references from the main desktop application
    /// </summary>
    public static void SetControls(IClassicDesktopStyleApplicationLifetime desktop)
    {
        GetMainView = desktop.MainWindow?.FindControl<MainView>("MainView");
        GetTitlebar = desktop.MainWindow?.FindControl<Control>("Titlebar");
        GetEditableTitlebar = GetTitlebar?.FindControl<EditableTitlebar>("EditableTitlebar");
        GetGalleryView = GetMainView?.MainGrid.GetControl<GalleryAnimationControlView>("GalleryView");
        GetBottomBar = desktop.MainWindow?.FindControl<BottomBar>("BottomBar");
        GetToolTipMessage = GetMainView?.MainGrid.FindControl<ToolTipMessage>("ToolTipMessage");
    }

    #region Navigation buttons

    /// <summary>
    /// Navigates to the next image using the bottom navigation button
    /// </summary>
    public static void NextButtonNavigation(MainViewModel vm)
    {
        SetButtonIntervalAndNavigate(GetBottomBar?.NextButton, true, false, vm);
    }
    
    /// <summary>
    /// Navigates to the previous image using the bottom navigation button
    /// </summary>
    public static void PreviousButtonNavigation(MainViewModel vm)
    {
        SetButtonIntervalAndNavigate(GetBottomBar?.PreviousButton, false, false, vm);
    }
    
    /// <summary>
    /// Navigates to the next image using the arrow button
    /// </summary>
    public static void NextArrowButtonNavigation(MainViewModel vm)
    {
        SetButtonIntervalAndNavigate(GetMainView?.ClickArrowRight?.PolyButton, true, true, vm);
    }
    
    /// <summary>
    /// Navigates to the previous image using the arrow button
    /// </summary>
    public static void PreviousArrowButtonNavigation(MainViewModel vm)
    {
        SetButtonIntervalAndNavigate(GetMainView?.ClickArrowLeft?.PolyButton, false, true, vm);
    }

    private static void SetButtonIntervalAndNavigate(RepeatButton? button, bool isNext, bool isArrow, MainViewModel vm)
    {
        if (button != null)
        {
            button.Interval = (int)TimeSpan.FromSeconds(Settings.UIProperties.NavSpeed).TotalMilliseconds;
        }

        Task.Run(() => NavigationManager.NavigateAndPositionCursor(isNext, isArrow, vm));
    }

    #endregion
}