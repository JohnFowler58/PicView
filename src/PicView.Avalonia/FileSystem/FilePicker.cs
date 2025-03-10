using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.UI;
using PicView.Avalonia.ViewModels;
using PicView.Core.FileHandling;
using PicView.Core.Localization;

namespace PicView.Avalonia.FileSystem;

public static class FilePicker
{
    private static readonly bool IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static async Task SelectAndLoadFile(MainViewModel vm)
    {
        if (vm is null)
        {
            return;
        }

        var file = await SelectFile().ConfigureAwait(false);
        if (file is null)
        {
            return;
        }

        await NavigationManager.LoadPicFromStringAsync(file, vm).ConfigureAwait(false);
    }

    private static async Task<string?> SelectFile()
    {
        var file = await SelectIStorageFile().ConfigureAwait(false);
        return file?.Path.LocalPath;
    }

    private static async Task<IStorageFile?> SelectIStorageFile()
    {
        try
        {
            var provider = GetStorageProvider();
            if (provider is null) return null;
            
            var options = new FilePickerOpenOptions
            {
                Title = $"{TranslationHelper.Translation.OpenFileDialog} - PicView",
                AllowMultiple = false,
                FileTypeFilter = [
                    AllFileType,
                    FilePickerFileTypes.ImageAll,
                    JpegFileType,
                    PngFileType,
                    GifFileType,
                    BmpFileType,
                    WebpFileType,
                    TiffFileType,
                    AvifFileType,
                    HeicFileType,
                    HeifFileType,
                    SvgFileType,
                    ArchiveFileType]
            };

            var files = await ExecuteOnUIThread(() => provider.OpenFilePickerAsync(options)).ConfigureAwait(false);
            return files?.Count >= 1 ? files[0] : null;
        }
        catch (Exception e)
        {
            #if DEBUG
            Console.WriteLine(e);
            #endif
            await TooltipHelper.ShowTooltipMessageAsync(e).ConfigureAwait(false);
        }

        return null;
    }

    private static FilePickerFileType AllFileType { get; } = new(TranslationHelper.GetTranslation("SupportedFiles"))
    {
        Patterns = SupportedFiles.ConvertFilesToGlobFormat(),
        AppleUniformTypeIdentifiers = ["public.image"],
        MimeTypes = ["image/*"],
    };
    
    private static FilePickerFileType AvifFileType { get; } = new(".avif")
    {
        Patterns = ["*.avif"],
        AppleUniformTypeIdentifiers = ["public.avif"],
        MimeTypes = ["image/avif"],
    };
    
    private static FilePickerFileType TiffFileType { get; } = new(".tiff")
    {
        Patterns = ["*.tiff", "*.tif"],
        AppleUniformTypeIdentifiers = ["public.tiff"],
        MimeTypes = ["image/tiff"],
    };
    
    private static FilePickerFileType WebpFileType { get; } = new(".webp")
    {
        Patterns = ["*.webp"],
        AppleUniformTypeIdentifiers = ["org.webmproject.webp"],
        MimeTypes = ["image/webp"],
    };
    
    private static FilePickerFileType PngFileType { get; } = new(".png")
    {
        Patterns = ["*.png"],
        AppleUniformTypeIdentifiers = ["public.png"],
        MimeTypes = ["image/png"],
    };
    
    private static FilePickerFileType JpegFileType { get; } = new(".jpg")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.jfif"],
        AppleUniformTypeIdentifiers = ["public.jpeg"],
        MimeTypes = ["image/jpeg"],
    };

    private static FilePickerFileType ArchiveFileType { get; } = new(TranslationHelper.GetTranslation("Archives"))
    {
        Patterns = SupportedFiles.ConvertArchivesToGlobFormat(),
        AppleUniformTypeIdentifiers = ["public.archive"],
        MimeTypes = ["application/zip", "application/x-rar-compressed", "application/x-tar", "application/x-7z-compressed"]
    };
    
    private static FilePickerFileType GifFileType { get; } = new(".gif")
    {
        Patterns = ["*.gif"],
        AppleUniformTypeIdentifiers = ["com.compuserve.gif"],
        MimeTypes = ["image/gif"],
    };
    
    private static FilePickerFileType BmpFileType { get; } = new(".bmp")
    {
        Patterns = ["*.bmp"],
        AppleUniformTypeIdentifiers = ["com.microsoft.bmp"],
        MimeTypes = ["image/bmp"],
    };
    
    private static FilePickerFileType SvgFileType { get; } = new(".svg")
    {
        Patterns = ["*.svg"],
        AppleUniformTypeIdentifiers = ["public.svg-image"],
        MimeTypes = ["image/svg+xml"],
    };
    
    private static FilePickerFileType HeicFileType { get; } = new(".heic")
    {
        Patterns = ["*.heic"],
        AppleUniformTypeIdentifiers = ["public.heic"],
        MimeTypes = ["image/heic"],
    };
    
    private static FilePickerFileType HeifFileType { get; } = new(".heif")
    {
        Patterns = ["*.heif"],
        AppleUniformTypeIdentifiers = ["public.heif"],
        MimeTypes = ["image/heif"],
    };

    public static async Task PickAndSaveFileAsAsync(string? fileName, MainViewModel vm)
    {
        var file = await PickFileForSavingAsync(fileName).ConfigureAwait(false);
        if (file is null)
        {
            return;
        }
        
        await FileSaverHelper.SaveFileAsync(fileName, file, vm).ConfigureAwait(false);
    }
    
    public static async Task<string?> PickFileForSavingAsync(string? fileName, string? ext = null)
    {
        try
        {
            var (provider, desktop) = GetProviderAndDesktop();
            if (provider is null || desktop is null) return null;
        
            var suggestedFileName = GetSuggestedFileName(fileName, ext);

            var options = new FilePickerSaveOptions
            {
                Title = $"{TranslationHelper.Translation.OpenFileDialog} - PicView",
                FileTypeChoices = [
                    FilePickerFileTypes.ImageAll,
                    JpegFileType,
                    PngFileType,
                    GifFileType,
                    BmpFileType,
                    WebpFileType,
                    TiffFileType,
                    AvifFileType,
                    HeicFileType,
                    HeifFileType,
                    SvgFileType],
                SuggestedFileName = suggestedFileName,
                SuggestedStartLocation = await desktop.MainWindow.StorageProvider.TryGetFolderFromPathAsync(fileName).ConfigureAwait(false)
            };
            
            var file = await ExecuteOnUIThread(() => provider.SaveFilePickerAsync(options)).ConfigureAwait(false);
            return file?.Path.LocalPath;
        }
        catch (Exception e)
        {
            #if DEBUG
            Console.WriteLine(e);
            #endif
            return null;
        }
    }

    public static async Task<string> SelectDirectory()
    {
        var provider = GetStorageProvider();
        if (provider is null) return string.Empty;

        var options = new FolderPickerOpenOptions
        {
            Title = TranslationHelper.Translation.Folder + " - PicView",
            AllowMultiple = false
        };
        
        var directories = await ExecuteOnUIThread(() => provider.OpenFolderPickerAsync(options)).ConfigureAwait(false);
        
        if (directories is null || directories.Count <= 0)
        {
            return string.Empty;
        }
        
        return directories[0].Path.LocalPath;
    }
    
    // Helper methods to reduce code duplication
    
    private static IStorageProvider? GetStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.StorageProvider is { } provider)
        {
            return provider;
        }
#if DEBUG
        Console.WriteLine("Missing StorageProvider instance.");
#endif
        return null;

    }
    
    private static (IStorageProvider? Provider, IClassicDesktopStyleApplicationLifetime? Desktop) GetProviderAndDesktop()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } provider)
        {
            return (null, null);
        }
        
        return (provider, desktop);
    }
    
    private static string GetSuggestedFileName(string? fileName, string? ext)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Path.GetRandomFileName();
        }
        
        return string.IsNullOrWhiteSpace(ext) 
            ? Path.GetFileName(fileName) 
            : Path.GetFileName(Path.ChangeExtension(fileName, ext));
    }
    
    private static async Task<T> ExecuteOnUIThread<T>(Func<Task<T>> action)
    {
        if (IsMacOS)
        {
            // Use Dispatcher to ensure we're on the UI thread for macOS
            return await Dispatcher.UIThread.InvokeAsync(action).ConfigureAwait(false);
        }
        
        // For other platforms, just execute directly
        return await action().ConfigureAwait(false);
    }
}