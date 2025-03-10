using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using PicView.Avalonia.UI;

namespace PicView.Avalonia.Animations;
public static class AnimationsHelper
{
    public static Animation HeightAnimation(double from, double to, double speed)
    {
        return new Animation
        {
            Duration = TimeSpan.FromSeconds(speed),
            Easing = new SplineEasing(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = Layoutable.HeightProperty,
                            Value = from
                        }
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = Layoutable.HeightProperty,
                            Value = to
                        }
                    },
                    Cue = new Cue(1d)
                },
            }
        };
    }

    public static Animation OpacityAnimation(double from, double to, double speed)
    {
        return OpacityAnimation(from, to, TimeSpan.FromSeconds(speed));
    }
    
    public static Animation OpacityAnimation(double from, double to, TimeSpan timeSpan)
    {
        return new Animation
        {
            Duration = timeSpan,
            Easing = new LinearEasing(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = Visual.OpacityProperty,
                            Value = from
                        }
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = Visual.OpacityProperty,
                            Value = to
                        }
                    },
                    Cue = new Cue(1d)
                },
            }
        };
    }
    
    /// <summary>
    /// Displays a brief animation to indicate a clipboard operation occurred
    /// </summary>
    public static async Task CopyAnimation()
    {
        const double speed = 0.2;
        const double opacity = 0.4;
        var startOpacityAnimation = OpacityAnimation(0, opacity, speed);
        var endOpacityAnimation = OpacityAnimation(opacity, 0, speed);
        Rectangle? rectangle = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            rectangle = new Rectangle
            {
                Width = UIHelper.GetMainView.Width,
                Height = UIHelper.GetMainView.Height,
                Opacity = 0,
                Fill = Brushes.Black,
                IsHitTestVisible = false
            };
            UIHelper.GetMainView.MainGrid.Children.Add(rectangle);
        });

        await startOpacityAnimation.RunAsync(rectangle);
        await endOpacityAnimation.RunAsync(rectangle);
        await Task.Delay(200);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            UIHelper.GetMainView.MainGrid.Children.Remove(rectangle);
        });
    }
    
    public static Animation CenteringAnimation(double fromX, double fromY, double toX, double toY, double speed)
    {
        return CenteringAnimation(fromX, fromY, toX, toY, TimeSpan.FromSeconds(speed));
    }
    
    public static Animation CenteringAnimation(double fromX, double fromY, double toX, double toY, TimeSpan duration)
    {
        return new Animation
        {
            Duration = duration,
            Easing = new SplineEasing(), // Using SplineEasing for smooth motion
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = TranslateTransform.XProperty,
                            Value = fromX
                        },
                        new Setter
                        {
                            Property = TranslateTransform.YProperty,
                            Value = fromY
                        }
                    },
                    Cue = new Cue(0d)
                },
                new KeyFrame
                {
                    Setters =
                    {
                        new Setter
                        {
                            Property = TranslateTransform.XProperty,
                            Value = toX
                        },
                        new Setter
                        {
                            Property = TranslateTransform.YProperty,
                            Value = toY
                        }
                    },
                    Cue = new Cue(1d)
                },
            }
        };
    }
}
        
