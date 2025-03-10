using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Avalonia.WindowBehavior;
using PicView.Core.Http;
#if RELEASE
using PicView.Core.Config;
#endif

namespace PicView.Avalonia.Update;

/// <summary>
///     JSON source generation for UpdateInfo deserialization
/// </summary>
[JsonSourceGenerationOptions(AllowTrailingCommas = true)]
[JsonSerializable(typeof(UpdateInfo))]
public partial class UpdateSourceGenerationContext : JsonSerializerContext;

/// <summary>
///     Handles application update operations
/// </summary>
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public static class UpdateManager
{
    private const string PrimaryUpdateUrl = "https://picview.org/update.json";
    private const string FallbackUpdateUrl = "https://picview.netlify.app/update.json";
    private const string ExecutableName = "PicView.exe";
    private const string UpdateArgument = "update";
    
    /// <summary>
    ///     Installation architecture types
    /// </summary>
    private enum InstalledArchitecture
    {
        X64Portable,
        X64Install,
        Arm64Portable,
        Arm64Install
    }
    
    /// <summary>
    ///     Checks for updates and installs if a newer version is available
    /// </summary>
    /// <param name="vm">The main view model</param>
    public static async Task UpdateCurrentVersion(MainViewModel vm)
    {
        // TODO Add support for other OS
        // TODO add UI
        
        // Handle admin privileges if needed
        if (HandleAdminPrivilegesIfNeeded())
        {
            return; // Application was restarted with admin privileges
        }
        
        // Create temporary directory for update files
        var tempPath = CreateTemporaryDirectory();
        var tempJsonPath = Path.Combine(tempPath, "update.json");

        // Get current version
        var currentVersion = GetCurrentVersion();

        // Download and parse update information
        var updateInfo = await DownloadAndParseUpdateInfo(tempJsonPath);
        if (updateInfo == null)
        {
            return;
        }

        // Handle update based on platform and installation type
        await HandlePlatformSpecificUpdate(vm, updateInfo, currentVersion, tempPath);
    }

    #region Utilities

    /// <summary>
    ///     Logs debug information in debug builds
    /// </summary>
    private static void LogDebug(object message)
    {
#if DEBUG
        if (message is Exception e)
        {
            Console.WriteLine(e);
        }
        else
        {
            Trace.WriteLine(message);
        }
#endif
    }

    #endregion

    #region Admin Privileges

    /// <summary>
    ///     Checks if the application needs to be elevated and restarts it if needed
    /// </summary>
    /// <returns>True if the application was restarted with admin privileges</returns>
    private static bool HandleAdminPrivilegesIfNeeded()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        return IsDirectoryAccessRestricted() && RestartWithAdminPrivileges();
    }

    /// <summary>
    ///     Determines if the current directory has restricted access
    /// </summary>
    private static bool IsDirectoryAccessRestricted()
    {
        var currentDirectory = new DirectoryInfo(Environment.ProcessPath);
        try
        {
            return currentDirectory.GetAccessControl().AreAccessRulesProtected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Restarts the application with admin privileges
    /// </summary>
    private static bool RestartWithAdminPrivileges()
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                Verb = "runas",
                UseShellExecute = true,
                FileName = ExecutableName,
                Arguments = UpdateArgument,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
            }
        };

        try
        {
            process.Start();
            Environment.Exit(0);
            return true;
        }
        catch (Exception e)
        {
            LogDebug(e);
            return false;
        }
    }

    #endregion

    #region Update Info

    /// <summary>
    ///     Creates a temporary directory for update files
    /// </summary>
    private static string CreateTemporaryDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }

    /// <summary>
    ///     Gets the current application version
    /// </summary>
    private static Version? GetCurrentVersion()
    {
#if DEBUG
        // Use a lower version in debug mode to test update functionality
        return new Version("3.0.0.3");
#else
        return VersionHelper.GetAssemblyVersion();
#endif
    }

    /// <summary>
    ///     Downloads and parses the update information
    /// </summary>
    private static async Task<UpdateInfo?> DownloadAndParseUpdateInfo(string tempJsonPath)
    {
        // Try primary URL first, fallback to secondary if needed
        if (await DownloadUpdateJson(PrimaryUpdateUrl, tempJsonPath))
        {
            return await ParseUpdateJson(tempJsonPath);
        }

        if (!await DownloadUpdateJson(FallbackUpdateUrl, tempJsonPath))
        {
            return null;
        }

        return await ParseUpdateJson(tempJsonPath);
    }

    /// <summary>
    ///     Downloads the update JSON file
    /// </summary>
    private static async Task<bool> DownloadUpdateJson(string url, string destinationPath)
    {
        try
        {
            using var downloader = new HttpClientDownloadWithProgress(url, destinationPath);
            await downloader.StartDownloadAsync();
            return true;
        }
        catch (Exception e)
        {
            LogDebug(e);
            return false;
        }
    }

    /// <summary>
    ///     Parses the update JSON file
    /// </summary>
    private static async Task<UpdateInfo?> ParseUpdateJson(string jsonFilePath)
    {
        try
        {
            var jsonString = await File.ReadAllTextAsync(jsonFilePath);
            var updateInfo = JsonSerializer.Deserialize(
                jsonString, typeof(UpdateInfo),
                UpdateSourceGenerationContext.Default) as UpdateInfo;

            if (updateInfo == null)
            {
                await TooltipHelper.ShowTooltipMessageAsync("Update information is missing or corrupted.");
                return null;
            }

            return updateInfo;
        }
        catch (Exception e)
        {
            LogDebug(e);
            await TooltipHelper.ShowTooltipMessageAsync("Failed to parse update information: \n" + e.Message);
            return null;
        }
    }

    #endregion

    #region Platform-specific Updates

    /// <summary>
    ///     Handles updates based on platform and installation type
    /// </summary>
    private static async Task HandlePlatformSpecificUpdate(
        MainViewModel vm,
        UpdateInfo updateInfo,
        Version? currentVersion,
        string tempPath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await HandleWindowsUpdate(vm, updateInfo, currentVersion, tempPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // TODO: Implement macOS update logic
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // TODO: Implement Linux update logic
        }
    }

    /// <summary>
    ///     Handles Windows-specific update logic
    /// </summary>
    private static async Task HandleWindowsUpdate(
        MainViewModel vm,
        UpdateInfo updateInfo,
        Version? currentVersion,
        string tempPath)
    {
        // Determine if application is installed or portable
        var isInstalled = IsApplicationInstalled();

        // Determine architecture
        var architecture = DetermineArchitecture(isInstalled);

        // Check if update is needed
        var remoteVersion = new Version(updateInfo.Version);
        if (remoteVersion <= currentVersion)
        {
            return;
        }

        // Apply update based on architecture and installation type
        await ApplyWindowsUpdate(vm, updateInfo, architecture, tempPath);
    }

    /// <summary>
    ///     Determines the installed architecture type
    /// </summary>
    private static InstalledArchitecture DetermineArchitecture(bool isInstalled)
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => isInstalled ? InstalledArchitecture.X64Install : InstalledArchitecture.X64Portable,
            Architecture.Arm64 => isInstalled
                ? InstalledArchitecture.Arm64Install
                : InstalledArchitecture.Arm64Portable,
            _ => InstalledArchitecture.X64Install
        };
    }

    /// <summary>
    ///     Applies Windows update based on architecture and installation type
    /// </summary>
    private static async Task ApplyWindowsUpdate(
        MainViewModel vm,
        UpdateInfo updateInfo,
        InstalledArchitecture architecture,
        string tempPath)
    {
        switch (architecture)
        {
            case InstalledArchitecture.Arm64Install:
                await InstallWindowsUpdate(vm, updateInfo.Arm64Install, tempPath);
                break;

            case InstalledArchitecture.Arm64Portable:
                await OpenDownloadInBrowser(updateInfo.Arm64Portable);
                break;

            case InstalledArchitecture.X64Install:
                await InstallWindowsUpdate(vm, updateInfo.X64Install, tempPath);
                break;

            case InstalledArchitecture.X64Portable:
                await OpenDownloadInBrowser(updateInfo.X64Portable);
                break;
        }
    }

    /// <summary>
    ///     Downloads and runs the installer for Windows
    /// </summary>
    private static async Task InstallWindowsUpdate(MainViewModel vm, string downloadUrl, string tempPath)
    {
        var fileName = Path.GetFileName(downloadUrl);
        var tempFileDownloadPath = Path.Combine(tempPath, fileName);

        await DownloadUpdateFile(vm, downloadUrl, tempFileDownloadPath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = tempFileDownloadPath
            }
        };

        process.Start();
        await WindowFunctions.WindowClosingBehavior();
    }

    /// <summary>
    ///     Opens a download link in the browser for portable versions
    /// </summary>
    private static async Task OpenDownloadInBrowser(string downloadUrl)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo(downloadUrl)
            {
                UseShellExecute = true,
                Verb = "open"
            }
        };

        process.Start();
        await process.WaitForExitAsync();
    }

    #endregion

    #region Installation Detection

    /// <summary>
    ///     Checks if the application is installed or running as portable
    /// </summary>
    private static bool IsApplicationInstalled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        // Check if executable exists in Program Files
        var x64Path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PicView",
            ExecutableName);

        if (File.Exists(x64Path))
        {
            return true;
        }

        // Check registry for installation
        return CheckRegistryForInstallation();
    }

    /// <summary>
    ///     Checks Windows registry to determine if the application is installed
    /// </summary>
    private static bool CheckRegistryForInstallation()
    {
        const string registryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string registryKey64 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        try
        {
            // Check 32-bit registry path
            if (CheckRegistryKeyForInstallation(Registry.LocalMachine, registryKey))
            {
                return true;
            }

            // Check 64-bit registry path
            return CheckRegistryKeyForInstallation(Registry.LocalMachine, registryKey64);
        }
        catch (Exception e)
        {
            LogDebug($"{nameof(CheckRegistryForInstallation)} exception, \n {e.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Checks a specific registry key for PicView installation
    /// </summary>
    private static bool CheckRegistryKeyForInstallation(RegistryKey baseKey, string keyPath)
    {
        using var key = baseKey.OpenSubKey(keyPath);
        if (key == null)
        {
            return false;
        }

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            using var subKey = key.OpenSubKey(subKeyName);
            var installDir = subKey?.GetValue("InstallLocation")?.ToString();

            if (string.IsNullOrWhiteSpace(installDir))
            {
                continue;
            }

            if (Path.Exists(Path.Combine(installDir, ExecutableName)))
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Download Helpers

    /// <summary>
    ///     Downloads update file with progress reporting
    /// </summary>
    private static async Task DownloadUpdateFile(MainViewModel vm, string downloadUrl, string tempPath)
    {
        vm.PlatformService.StopTaskbarProgress();

        using var downloader = new HttpClientDownloadWithProgress(downloadUrl, tempPath);
        try
        {
            downloader.ProgressChanged += (size, downloaded, percentage) =>
                UpdateDownloadProgress(vm, size, downloaded, percentage);

            await downloader.StartDownloadAsync();
        }
        catch (Exception e)
        {
            LogDebug(e);
            await TooltipHelper.ShowTooltipMessageAsync(e.Message);
        }
        finally
        {
            vm.PlatformService.StopTaskbarProgress();
        }
    }

    /// <summary>
    ///     Updates download progress in the taskbar
    /// </summary>
    private static void UpdateDownloadProgress(
        MainViewModel vm,
        long? totalFileSize,
        long? totalBytesDownloaded,
        double? progressPercentage)
    {
        if (!totalFileSize.HasValue || !totalBytesDownloaded.HasValue || !progressPercentage.HasValue)
        {
            return;
        }

        vm.PlatformService.SetTaskbarProgress((ulong)totalBytesDownloaded.Value, (ulong)totalFileSize.Value);
    }

    #endregion
}