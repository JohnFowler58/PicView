using Avalonia;
using Avalonia.Media;
using PicView.Avalonia.ViewModels;

namespace PicView.Avalonia.ColorManagement;

/// <summary>
/// Manages the background appearance for the image viewer.
/// It provides methods to change and set the background color or pattern.
/// </summary>
public static class BackgroundManager
{
    /// <summary>
    /// Defines the available background choices for the image viewer
    /// </summary>
    private enum BackgroundType
    {
        Transparent = 0,
        NoiseTexture = 1,
        Checkerboard = 2,
        CheckerboardAlternative = 3,
        White = 4,
        LightGray = 5,
        MediumGray = 6,
        SemiTransparentDarkGray = 7,
        SemiTransparentDarkerGray = 8,
        NearBlack = 9,
        
        // For cycling purpose
        MaxValue = 9
    }
    
    /// <summary>
    /// Default checkerboard size in pixels
    /// </summary>
    private const int DefaultCheckerboardSize = 30;
    
    /// <summary>
    /// Alternative checkerboard size in pixels
    /// </summary>
    private const int AlternativeCheckerboardSize = 60;

    /// <summary>
    /// Gets the appropriate brush for the current background setting
    /// </summary>
    private static Brush GetBackgroundBrush(BackgroundType backgroundType) => backgroundType switch
    {
        BackgroundType.Transparent => new SolidColorBrush(Colors.Transparent),
        BackgroundType.NoiseTexture => GetNoiseTextureBrush(),
        BackgroundType.Checkerboard => CreateCheckerboardBrush(),
        BackgroundType.CheckerboardAlternative => CreateCheckerboardBrush(
            Color.FromRgb(235, 235, 235), 
            Color.FromRgb(40, 40, 40), 
            AlternativeCheckerboardSize),
        BackgroundType.White => new SolidColorBrush(Colors.White),
        BackgroundType.LightGray => new SolidColorBrush(Color.FromRgb(200, 200, 200)),
        BackgroundType.MediumGray => new SolidColorBrush(Color.FromRgb(155, 155, 155)),
        BackgroundType.SemiTransparentDarkGray => new SolidColorBrush(Color.FromArgb(200, 100, 100, 100)),
        BackgroundType.SemiTransparentDarkerGray => new SolidColorBrush(Color.FromArgb(200, 50, 50, 50)),
        BackgroundType.NearBlack => new SolidColorBrush(Color.FromRgb(5, 5, 5)),
        _ => new SolidColorBrush(Colors.Transparent)
    };

    /// <summary>
    /// Changes the background to the next option in the cycle and updates the view model.
    /// </summary>
    /// <param name="vm">The main view model where the background is updated.</param>
    public static void ChangeBackground(MainViewModel vm)
    {
        if (vm.CurrentView != vm.ImageViewer)
        {
            return;
        }

        // Cycle to the next background choice
        var nextChoice = (Settings.UIProperties.BgColorChoice + 1) % ((int)BackgroundType.MaxValue + 1);
        SetBackground(vm, nextChoice);
    }

    /// <summary>
    /// Sets the background of the view model based on the current background choice.
    /// </summary>
    /// <param name="vm">The main view model where the background is set.</param>
    public static void SetBackground(MainViewModel vm)
    {
        SetBackground(vm, Settings.UIProperties.BgColorChoice);
    }
    
    /// <summary>
    /// Sets the background of the view model to a specific background choice.
    /// </summary>
    /// <param name="vm">The main view model where the background is set.</param>
    /// <param name="choice">The background choice to set.</param>
    public static void SetBackground(MainViewModel vm, int choice)
    {
        Settings.UIProperties.BgColorChoice = choice;
        vm.ImageBackground = GetBackgroundBrush((BackgroundType)choice);
        vm.BackgroundChoice = choice;
    }

    /// <summary>
    /// Retrieves the noise texture brush from the application resources.
    /// </summary>
    /// <returns>A brush containing the noise texture or a transparent brush if unavailable.</returns>
    private static Brush GetNoiseTextureBrush()
    {
        if (Application.Current.TryGetResource("NoisyTexture", Application.Current.RequestedThemeVariant, out var texture) && 
            texture is ImageBrush imageBrush)
        {
            return imageBrush;
        }
        
        return new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// Creates a checkerboard brush with alternative colors and size.
    /// </summary>
    /// <param name="size">The size of the checkerboard squares in pixels.</param>
    /// <returns>A drawing brush representing the alternative checkerboard pattern.</returns>
    public static DrawingBrush CreateCheckerboardBrushAlt(int size = AlternativeCheckerboardSize)
    {
        return CreateCheckerboardBrush(
            Color.FromRgb(235, 235, 235),
            Color.FromRgb(40, 40, 40), 
            size);
    }

    /// <summary>
    /// Creates a checkerboard brush with two alternating colors.
    /// </summary>
    /// <param name="primaryColor">The primary color for the checkerboard squares. Defaults to white.</param>
    /// <param name="secondaryColor">The secondary color for the checkerboard squares. Defaults to a dark gray.</param>
    /// <param name="size">The size of the checkerboard squares in pixels. Defaults to 30.</param>
    /// <returns>A drawing brush representing the checkerboard pattern.</returns>
    public static DrawingBrush CreateCheckerboardBrush(Color primaryColor = default, Color secondaryColor = default,
        int size = DefaultCheckerboardSize)
    {
        // Default colors if not provided
        primaryColor = primaryColor == default ? Colors.White : primaryColor;
        secondaryColor = secondaryColor == default ? Color.Parse("#F81F1F1F") : secondaryColor;

        var drawingGroup = new DrawingGroup();

        // Primary color rectangles
        var primaryGeometry = new GeometryDrawing
        {
            Brush = new SolidColorBrush(primaryColor),
            Geometry = new GeometryGroup
            {
                Children =
                {
                    new RectangleGeometry(new Rect(0, 0, size, size)),
                    new RectangleGeometry(new Rect(size, size, size, size))
                }
            }
        };

        // Secondary color rectangles
        var secondaryGeometry = new GeometryDrawing
        {
            Brush = new SolidColorBrush(secondaryColor),
            Geometry = new GeometryGroup
            {
                Children =
                {
                    new RectangleGeometry(new Rect(size / 2, 0, size / 2, size / 2)),
                    new RectangleGeometry(new Rect(0, size / 2, size / 2, size / 2))
                }
            }
        };

        // Add geometries to the drawing group
        drawingGroup.Children.Add(primaryGeometry);
        drawingGroup.Children.Add(secondaryGeometry);

        return new DrawingBrush
        {
            DestinationRect = new RelativeRect(0, 0, size, size, RelativeUnit.Absolute),
            TileMode = TileMode.Tile,
            Stretch = Stretch.None,
            Drawing = drawingGroup
        };
    }
}