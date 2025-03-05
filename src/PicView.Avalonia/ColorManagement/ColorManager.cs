using Avalonia;
using Avalonia.Media;

namespace PicView.Avalonia.ColorManagement;

/// <summary>
/// Manages accent colors based on the selected color theme.
/// </summary>
public static class ColorManager
{
    // Define color theme constants for better readability
    private const int Blue = 0;
    private const int Pink = 2;
    private const int Orange = 3;
    private const int Green = 4;
    private const int Red = 5;
    private const int Teal = 6;
    private const int Aqua = 7;
    private const int Golden = 8;
    private const int Purple = 9;
    private const int Cyan = 10;
    private const int Magenta = 11;
    private const int Lime = 12;
    
    // Color definitions for each theme
    private static readonly Dictionary<int, ThemeColors> ThemeColorMap = new()
    {
        [Blue] = new ThemeColors(
            logoLight: Color.FromRgb(225, 210, 80),
            logoDark: Color.FromRgb(255, 240, 90),
            primary: Color.FromRgb(26, 140, 240),
            secondary: Color.FromArgb(242, 66, 163, 249)
        ),
        [Pink] = new ThemeColors(
            logoLight: Color.FromRgb(250, 180, 38),
            logoDark: Color.FromRgb(255, 237, 38),
            primary: Color.FromRgb(255, 53, 197),
            secondary: Color.FromArgb(230, 255, 98, 210)
        ),
        [Orange] = new ThemeColors(
            logoLight: Color.FromRgb(248, 175, 60),
            logoDark: Color.FromRgb(248, 175, 60),
            primary: Color.FromRgb(219, 91, 61),
            secondary: Color.FromArgb(242, 245, 121, 57)
        ),
        [Green] = new ThemeColors(
            logoLight: Color.FromRgb(175, 157, 38),
            logoDark: Color.FromRgb(209, 237, 93),
            primary: Color.FromRgb(34, 203, 151),
            secondary: Color.FromArgb(242, 80, 248, 196)
        ),
        [Red] = new ThemeColors(
            logoLight: Color.FromRgb(250, 192, 92),
            logoDark: Color.FromRgb(250, 192, 92),
            primary: Color.FromRgb(249, 17, 16),
            secondary: Color.FromArgb(242, 249, 61, 60)
        ),
        [Teal] = new ThemeColors(
            logoLight: Color.FromRgb(254, 172, 150),
            logoDark: Color.FromRgb(254, 172, 150),
            primary: Color.FromRgb(68, 161, 160),
            secondary: Color.FromArgb(242, 31, 174, 152)
        ),
        [Aqua] = new ThemeColors(
            logoLight: Color.FromRgb(228, 209, 17),
            logoDark: Color.FromRgb(228, 209, 17),
            primary: Color.FromRgb(54, 230, 204),
            secondary: Color.FromArgb(242, 121, 253, 233)
        ),
        [Golden] = new ThemeColors(
            logoLight: Color.FromRgb(226, 180, 224),
            logoDark: Color.FromRgb(255, 253, 42),
            primary: Color.FromRgb(254, 169, 85),
            secondary: Color.FromArgb(242, 249, 187, 125)
        ),
        [Purple] = new ThemeColors(
            logoLight: Color.FromRgb(226, 141, 223),
            logoDark: Color.FromRgb(237, 184, 135),
            primary: Color.FromRgb(151, 56, 235),
            secondary: Color.FromArgb(242, 194, 95, 255)
        ),
        [Cyan] = new ThemeColors(
            logoLight: Color.FromRgb(215, 200, 70),
            logoDark: Color.FromRgb(255, 253, 66),
            primary: Color.FromRgb(27, 161, 226),
            secondary: Color.FromArgb(242, 89, 186, 233)
        ),
        [Magenta] = new ThemeColors(
            logoLight: Color.FromRgb(226, 141, 223),
            logoDark: Color.FromRgb(255, 237, 38),
            primary: Color.FromRgb(230, 139, 238),
            secondary: Color.FromArgb(242, 255, 108, 212)
        ),
        [Lime] = new ThemeColors(
            logoLight: Color.FromRgb(255, 253, 42),
            logoDark: Color.FromRgb(255, 253, 42),
            primary: Color.FromRgb(32, 231, 107),
            secondary: Color.FromArgb(242, 97, 240, 151)
        )
    };

    /// <summary>
    /// Gets the logo accent color based on the current color theme.
    /// </summary>
    public static Color LogoAccentColor => GetThemeColors().GetLogoColor(Settings.Theme.Dark);

    /// <summary>
    /// Gets the secondary accent color based on the current color theme.
    /// A brighter shade of the primary accent color.
    /// </summary>
    public static Color SecondaryAccentColor => GetThemeColors().Secondary;

    /// <summary>
    /// Gets the primary accent color based on the current color theme.
    /// </summary>
    public static Color PrimaryAccentColor => GetThemeColors().Primary;

    /// <summary>
    /// Gets the color set for the current theme
    /// </summary>
    private static ThemeColors GetThemeColors()
    {
        var themeIndex = Settings.Theme.ColorTheme;
        
        if (ThemeColorMap.TryGetValue(themeIndex, out var colors))
        {
            return colors;
        }

        if (themeIndex is 1)
        {
            return ThemeColorMap[0];
        }
        
        throw new ArgumentOutOfRangeException(nameof(Settings.Theme.ColorTheme), 
            $"Color theme index {themeIndex} is not supported");
    }

    /// <summary>
    /// Updates the accent colors in the application resources based on the selected color theme.
    /// </summary>
    /// <param name="colorTheme">The color theme index to apply.</param>
    public static void UpdateAccentColors(int colorTheme)
    {
        Settings.Theme.ColorTheme = colorTheme;

        var primaryBrush = new SolidColorBrush(PrimaryAccentColor);
        var secondaryBrush = new SolidColorBrush(SecondaryAccentColor);
        var logoAccentBrush = new SolidColorBrush(LogoAccentColor);
        
        if (Settings.Theme.GlassTheme)
        {
            ThemeManager.GlassThemeUpdates();
        }

        // Update application resources with the new brushes
        UpdateResourceIfExists("AccentColor", primaryBrush);
        UpdateResourceIfExists("SecondaryAccentColor", secondaryBrush);
        UpdateResourceIfExists("LogoAccentColor", logoAccentBrush);
    }
    
    /// <summary>
    /// Updates a resource if it exists in the application resources
    /// </summary>
    private static void UpdateResourceIfExists(string resourceKey, object value)
    {
        if (Application.Current.TryGetResource(resourceKey, Application.Current.RequestedThemeVariant, out _))
        {
            Application.Current.Resources[resourceKey] = value;
        }
    }
    
    /// <summary>
    /// Represents a set of colors for a theme
    /// </summary>
    private readonly struct ThemeColors(Color logoLight, Color logoDark, Color primary, Color secondary)
    {
        private Color LogoLight { get; } = logoLight;
        private Color LogoDark { get; } = logoDark;
        public Color Primary { get; } = primary;
        public Color Secondary { get; } = secondary;

        /// <summary>
        /// Gets the appropriate logo color based on dark mode setting
        /// </summary>
        public Color GetLogoColor(bool isDarkMode) => isDarkMode ? LogoDark : LogoLight;
    }
}