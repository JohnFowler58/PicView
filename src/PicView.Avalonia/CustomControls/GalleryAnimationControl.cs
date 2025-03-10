using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using PicView.Avalonia.Animations;
using PicView.Avalonia.Gallery;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Calculations;
using PicView.Core.Gallery;
using ReactiveUI;

namespace PicView.Avalonia.CustomControls;

public class GalleryAnimationControl : UserControl
{
    #region Fields and Properties

    private const double FastAnimationSpeed = 0.3;
    private const double MediumAnimationSpeed = 0.5;
    private const double SlowAnimationSpeed = 0.6;
    private const double FullOpacity = 1.0;
    private const double NoOpacity = 0.0;
    private const int ZeroHeight = 0;

    private static readonly Thickness FullGalleryItemMargin = new(25);
    private static readonly Thickness BottomGalleryItemMargin = new(2, 0);

    public static readonly AvaloniaProperty<GalleryMode?> GalleryModeProperty =
        AvaloniaProperty.Register<GalleryAnimationControl, GalleryMode?>(nameof(GalleryMode));

    public GalleryMode GalleryMode
    {
        get => GetValue(GalleryModeProperty) as GalleryMode? ?? GalleryMode.Closed;
        set => SetValue(GalleryModeProperty, value);
    }

    private bool _isAnimating;
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    #endregion

    #region Constructors

    public GalleryAnimationControl()
    {
        Loaded += OnControlLoaded;
    }

    private void OnControlLoaded(object? sender, RoutedEventArgs e)
    {
        AddHandler(PointerPressedEvent, PreviewPointerPressedEvent, RoutingStrategies.Tunnel);

        this.WhenAnyValue(x => x.GalleryMode)
            .WhereNotNull()
            .SelectMany(async galleryMode =>
            {
                try
                {
                    switch (galleryMode)
                    {
                        case GalleryMode.FullToBottom:
                            await FullToBottomAnimation();
                            break;
                        case GalleryMode.FullToClosed:
                            await FullToClosedAnimation();
                            break;
                        case GalleryMode.BottomToFull:
                            await BottomToFullAnimation();
                            break;
                        case GalleryMode.BottomToClosed:
                            await BottomToClosedAnimation();
                            break;
                        case GalleryMode.ClosedToFull:
                            await ClosedToFullAnimation();
                            break;
                        case GalleryMode.ClosedToBottom:
                            await ClosedToBottomAnimation();
                            break;
                        case GalleryMode.Closed:
                            await CloseWithNoAnimation();
                            break;
                        case GalleryMode.BottomNoAnimation:
                            await BottomNoAnimation();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(galleryMode), galleryMode, null);
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception here
                    _isAnimating = false;
                }

                return galleryMode;
            })
            .Subscribe();

        if (Parent is Control parent)
        {
            parent.SizeChanged += ParentSizeChanged;
        }
    }

    #endregion

    #region Animation Methods

    private async Task CloseWithNoAnimation()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsVisible = false;
            UIHelper.GetGalleryView.BlurMask.BlurEnabled = false;
            Height = ZeroHeight;
        });
    }

    private async Task BottomNoAnimation()
    {
        if (ViewModel == null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsVisible = true;
            Opacity = FullOpacity;
            Height = double.NaN;
            ViewModel.GalleryOrientation = Orientation.Horizontal;
        });
    }

    private async Task ClosedToFullAnimation()
    {
        if (ViewModel == null || _isAnimating || Parent is not Control parent)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            GalleryHelper.SetGalleryItemStretch(Settings.Gallery.FullGalleryStretchMode, ViewModel);

            // Setup initial state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsVisible = true;
                Opacity = NoOpacity;
                Height = parent.Bounds.Height;
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = true;
                ViewModel.GalleryItemMargin = FullGalleryItemMargin;
            });

            // Configure gallery
            ViewModel.GalleryOrientation = Orientation.Vertical;
            GalleryStretchMode.DetermineStretchMode(ViewModel);
            ViewModel.IsGalleryCloseIconVisible = true;

            // Animate opacity
            var opacityAnimation = AnimationsHelper.OpacityAnimation(NoOpacity, FullOpacity, MediumAnimationSpeed);
            await opacityAnimation.RunAsync(this);

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Opacity = FullOpacity;
                ViewModel.GalleryVerticalAlignment = VerticalAlignment.Stretch;
            });

            // Wait for animation completion
            await Task.Delay(opacityAnimation.Delay);

            // Center the selected item
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GalleryNavigation.CenterScrollToSelectedItem(ViewModel);
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task FullToClosedAnimation()
    {
        if (ViewModel == null || _isAnimating || Parent is not Control parent)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            // Setup initial state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = parent.Bounds.Height;
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = false;
            });

            // Animate opacity
            var opacityAnimation = AnimationsHelper.OpacityAnimation(FullOpacity, NoOpacity, FastAnimationSpeed);
            ViewModel.GalleryMargin = new Thickness(0);
            await opacityAnimation.RunAsync(this);

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Opacity = NoOpacity;
                IsVisible = false;
                Height = ZeroHeight;
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task ClosedToBottomAnimation()
    {
        if (ViewModel == null || _isAnimating)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            // Setup gallery properties
            GalleryHelper.SetGalleryItemStretch(Settings.Gallery.BottomGalleryStretchMode, ViewModel);

            // Setup initial state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = ZeroHeight;
                IsVisible = true;
                Opacity = FullOpacity;
                WindowResizing.SetSize(ViewModel);
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = false;
                ViewModel.GalleryItemMargin = BottomGalleryItemMargin;
            });

            // Configure gallery
            ViewModel.GalleryOrientation = Orientation.Horizontal;
            GalleryStretchMode.DetermineStretchMode(ViewModel);
            ViewModel.IsGalleryCloseIconVisible = false;
            ViewModel.GalleryVerticalAlignment = VerticalAlignment.Bottom;

            // Animate height
            var to = ViewModel.GalleryHeight;
            var heightAnimation = AnimationsHelper.HeightAnimation(ZeroHeight, to, FastAnimationSpeed);
            await heightAnimation.RunAsync(this);

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = to;
                IsVisible = true;
                GalleryNavigation.CenterScrollToSelectedItem(ViewModel);
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task BottomToClosedAnimation()
    {
        if (ViewModel == null || _isAnimating)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            // Animate closing
            var from = ViewModel.GetBottomGalleryItemHeight + SizeDefaults.ScrollbarSize;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = from;
                Opacity = FullOpacity;
                IsVisible = true;
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = false;
            });

            // Configure gallery
            ViewModel.GalleryOrientation = Orientation.Horizontal;
            ViewModel.IsGalleryCloseIconVisible = false;

            // Animate height
            var heightAnimation = AnimationsHelper.HeightAnimation(from, ZeroHeight, FastAnimationSpeed);
            await heightAnimation.RunAsync(this);

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = ZeroHeight;
                IsVisible = false;
                WindowResizing.SetSize(ViewModel);
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task BottomToFullAnimation()
    {
        if (ViewModel == null || _isAnimating || Parent is not Control parent)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            // Configure gallery
            ViewModel.GalleryOrientation = Orientation.Vertical;
            ViewModel.IsGalleryCloseIconVisible = true;
            GalleryStretchMode.DetermineStretchMode(ViewModel);
            ViewModel.GalleryItemMargin = FullGalleryItemMargin;

            // Animate height
            var from = ViewModel.GalleryHeight;
            var to = parent.Bounds.Height;
            var heightAnimation = AnimationsHelper.HeightAnimation(from, to, MediumAnimationSpeed);
            await heightAnimation.RunAsync(this);

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = to;
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = true;
            });

            ViewModel.GalleryVerticalAlignment = VerticalAlignment.Stretch;

            // Center the selected item
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GalleryNavigation.CenterScrollToSelectedItem(ViewModel);
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task FullToBottomAnimation()
    {
        if (ViewModel == null || _isAnimating || Parent is not Control parent)
        {
            return;
        }

        try
        {
            _isAnimating = true;

            // Configure gallery
            ViewModel.GalleryVerticalAlignment = VerticalAlignment.Bottom;
            ViewModel.IsGalleryCloseIconVisible = false;

            // Animate height
            var from = Bounds.Height;
            var to = ViewModel.GalleryHeight;
            var heightAnimation = AnimationsHelper.HeightAnimation(from, to, SlowAnimationSpeed);
            await heightAnimation.RunAsync(this);

            if (!GalleryLoad.IsLoading)
            {
                GalleryStretchMode.DetermineStretchMode(ViewModel);
            }

            // Apply final state
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Height = parent.Bounds.Height;
                UIHelper.GetGalleryView.BlurMask.BlurEnabled = false;
                ViewModel.GalleryItemMargin = BottomGalleryItemMargin;
                ViewModel.GalleryOrientation = Orientation.Horizontal;
            });

            // Center the selected item
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                GalleryNavigation.CenterScrollToSelectedItem(ViewModel);
            });
        }
        finally
        {
            _isAnimating = false;
        }
    }

    #endregion

    #region Events

    private void ParentSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isAnimating || !GalleryFunctions.IsFullGalleryOpen || sender is not Control parent)
        {
            return;
        }

        Width = parent.Bounds.Width;
        Height = parent.Bounds.Height;
    }

    private void PreviewPointerPressedEvent(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Disable right click selection, to not interfere with context menu
        e.Handled = true;
    }

    #endregion

    #region Cleanup

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        if (Parent is Control parent)
        {
            parent.SizeChanged -= ParentSizeChanged;
        }

        Loaded -= OnControlLoaded;
        RemoveHandler(PointerPressedEvent, PreviewPointerPressedEvent);
    }

    #endregion
}