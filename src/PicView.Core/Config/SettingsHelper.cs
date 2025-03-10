using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PicView.Core.Config;

[JsonSourceGenerationOptions(AllowTrailingCommas = true, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SourceGenerationContext : JsonSerializerContext;

public static class SettingsHelper
{
    private const double CurrentSettingsVersion = 1.3;
    private const string ConfigFileName = "UserSettings.json";
    private const string LocalConfigPath = "Config/" + ConfigFileName;
    private const string RoamingConfigFolder = "Ruben2776/PicView/Config";
    private const string RoamingConfigPath = RoamingConfigFolder + "/" + ConfigFileName;

    public static AppSettings? Settings { get; private set; }

    /// <summary>
    ///     Asynchronously loads the user settings. Loads defaults if not found
    /// </summary>
    /// <returns>True if settings exists and were loaded successfully</returns>
    public static async Task<bool> LoadSettingsAsync()
    {
        try
        {
            var path = GetUserSettingsPath();
            if (!string.IsNullOrEmpty(path))
            {
                return await LoadFromPathAsync(path).ConfigureAwait(false);
            }

            SetDefaults();
            return false;
        }
        catch (Exception ex)
        {
            LogError(nameof(LoadSettingsAsync), ex);
            SetDefaults();
            return false;
        }
    }

    /// <summary>
    ///     Determines the path to the user settings file
    /// </summary>
    /// <returns>Path to the user settings file, or empty string if not found</returns>
    public static string GetUserSettingsPath()
    {
        var roamingPath = GetRoamingSettingsPath();
        if (File.Exists(roamingPath))
        {
            return roamingPath;
        }

        var localPath = GetLocalSettingsPath();
        return File.Exists(localPath) ? localPath : string.Empty;
    }

    /// <summary>
    ///     Gets the path to the roaming settings file
    /// </summary>
    private static string GetRoamingSettingsPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), RoamingConfigPath);
    }

    /// <summary>
    ///     Gets the path to the local settings file
    /// </summary>
    private static string GetLocalSettingsPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, LocalConfigPath);
    }

    /// <summary>
    ///     Sets default settings values
    /// </summary>
    public static void SetDefaults()
    {
        Settings = new AppSettings
        {
            UIProperties = new UIProperties(),
            Gallery = new Gallery(),
            ImageScaling = new ImageScaling(),
            Sorting = new Sorting(),
            Theme = new Theme(),
            WindowProperties = new WindowProperties(),
            Zoom = new Zoom(),
            StartUp = new StartUp(),
            Version = CurrentSettingsVersion
        };
        // Get the default culture from the OS
        Settings.UIProperties.UserLanguage = CultureInfo.CurrentCulture.Name;
    }

    /// <summary>
    ///     Deletes all settings files
    /// </summary>
    public static void DeleteSettingFiles()
    {
        try
        {
            DeleteFileIfExists(GetRoamingSettingsPath());
            DeleteFileIfExists(GetLocalSettingsPath());
        }
        catch (Exception ex)
        {
            LogError(nameof(DeleteSettingFiles), ex);
        }
    }

    /// <summary>
    ///     Deletes a file if it exists
    /// </summary>
    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Loads settings from the specified path
    /// </summary>
    private static async Task<bool> LoadFromPathAsync(string path)
    {
        try
        {
            await ReadSettingsFromPathAsync(path).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            // If primary path fails, try the alternative path
            var alternativePath = Path.GetDirectoryName(path)?.Contains("ApplicationData") == true
                ? GetLocalSettingsPath()
                : GetRoamingSettingsPath();

            if (File.Exists(alternativePath))
            {
                try
                {
                    await ReadSettingsFromPathAsync(alternativePath).ConfigureAwait(false);
                    return true;
                }
                catch (Exception)
                {
                    SetDefaults();
                }
            }
            else
            {
                SetDefaults();
            }

            return false;
        }
    }

    /// <summary>
    ///     Reads settings from the specified path and upgrades them if necessary
    /// </summary>
    private static async Task ReadSettingsFromPathAsync(string path)
    {
        var jsonString = await File.ReadAllTextAsync(path).ConfigureAwait(false);

        if (JsonSerializer.Deserialize(
                jsonString, typeof(AppSettings), SourceGenerationContext.Default) is not AppSettings settings)
        {
            throw new JsonException("Failed to deserialize settings");
        }

        Settings = await UpgradeSettingsIfNeededAsync(settings).ConfigureAwait(false);
    }

    /// <summary>
    ///     Saves settings to disk
    /// </summary>
    public static async Task<bool> SaveSettingsAsync()
    {
        if (Settings == null)
        {
            return false;
        }

        try
        {
            // Try to save to local directory first
            var localPath = GetLocalSettingsPath();
            await SaveSettingsToPathAsync(localPath).ConfigureAwait(false);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // If unauthorized, try saving to roaming app data
            try
            {
                var roamingPath = GetRoamingSettingsPath();
                EnsureDirectoryExists(roamingPath);
                await SaveSettingsToPathAsync(roamingPath).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LogError(nameof(SaveSettingsAsync), ex);
                return false;
            }
        }
        catch (Exception ex)
        {
            LogError(nameof(SaveSettingsAsync), ex);
            return false;
        }
    }

    /// <summary>
    ///     Ensures that the directory for a file path exists
    /// </summary>
    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    ///     Saves settings to the specified path
    /// </summary>
    private static async Task SaveSettingsToPathAsync(string path)
    {
        if (Settings == null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(Settings, typeof(AppSettings), SourceGenerationContext.Default);
        await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
    }

    /// <summary>
    ///     Upgrades settings to the current version if needed
    /// </summary>
    private static async Task<AppSettings> UpgradeSettingsIfNeededAsync(AppSettings settings)
    {
        if (settings.Version >= CurrentSettingsVersion)
        {
            return settings;
        }

        await SynchronizeSettingsAsync(settings).ConfigureAwait(false);
        settings.Version = CurrentSettingsVersion;

        return settings;
    }

    /// <summary>
    ///     Synchronizes settings between different versions
    /// </summary>
    private static async Task SynchronizeSettingsAsync(AppSettings newSettings)
    {
        try
        {
            var localPath = GetLocalSettingsPath();
            if (!File.Exists(localPath))
            {
                return;
            }

            var jsonString = await File.ReadAllTextAsync(localPath).ConfigureAwait(false);
            var existingSettings = JsonSerializer.Deserialize(
                jsonString, typeof(AppSettings), SourceGenerationContext.Default) as AppSettings;

            if (existingSettings == null)
            {
                return;
            }

            // Copy new property values to existing settings when missing
            MergeSettings(existingSettings, newSettings);

            // Save the synchronized settings
            Settings = existingSettings;
            await SaveSettingsToPathAsync(localPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogError(nameof(SynchronizeSettingsAsync), ex);
        }
    }

    /// <summary>
    ///     Merges settings by copying properties from newSettings to existingSettings
    ///     where the property is missing or null in existingSettings
    /// </summary>
    private static void MergeSettings(AppSettings existingSettings, AppSettings newSettings)
    {
        foreach (var property in typeof(AppSettings).GetProperties())
        {
            var existingValue = property.GetValue(existingSettings);

            if (existingValue != null)
            {
                continue;
            }

            var newValue = property.GetValue(newSettings);
            property.SetValue(existingSettings, newValue);
        }
    }

    /// <summary>
    ///     Logs an error message
    /// </summary>
    private static void LogError(string methodName, Exception ex)
    {
#if DEBUG
        Trace.WriteLine($"{nameof(SettingsHelper)}: {methodName} error: {ex.Message}");
        Trace.WriteLine(ex.StackTrace);
#endif
    }
}