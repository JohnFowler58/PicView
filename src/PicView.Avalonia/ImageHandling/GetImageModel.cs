using Avalonia.Media.Imaging;
using ImageMagick;
using PicView.Core.ImageDecoding;

namespace PicView.Avalonia.ImageHandling;

public static class GetImageModel
{
    // Group extensions by how they should be handled
    private static readonly Dictionary<string, ImageHandler> ExtensionHandlers = new()
    {
        { ".webp", new AnimatedWebpHandler() },
        { ".gif", new AnimatedGifHandler() },
        { ".png", new StandardBitmapHandler() },
        { ".jpg", new StandardBitmapHandler() },
        { ".jpeg", new StandardBitmapHandler() },
        { ".jpe", new StandardBitmapHandler() },
        { ".bmp", new StandardBitmapHandler() },
        { ".jfif", new StandardBitmapHandler() },
        { ".ico", new StandardBitmapHandler() },
        { ".wbmp", new StandardBitmapHandler() },
        { ".svg", new SvgHandler() },
        { ".svgz", new SvgHandler() },
        { ".b64", new Base64Handler() }
    };

    public static async Task<ImageModel?> GetImageModelAsync(FileInfo fileInfo)
    {
        if (fileInfo is null)
        {
            LogError("fileInfo is null");
            return CreateErrorImageModel(null);
        }

        var imageModel = new ImageModel { FileInfo = fileInfo };

        try
        {
            // Get extension and prepare MagickImage for metadata
            var ext = fileInfo.Extension.ToLower();
            using var magickImage = new MagickImage();
            magickImage.Ping(fileInfo);
            
            // If extension is missing, use format from MagickImage
            if (string.IsNullOrEmpty(ext))
            {
                ext = $".{magickImage.Format.ToString().ToLower()}";
            }
            
            // Extract EXIF orientation early
            imageModel.EXIFOrientation = EXIFHelper.GetImageOrientation(magickImage);

            // Process the image based on extension
            if (ExtensionHandlers.TryGetValue(ext, out var handler))
            {
                await handler.ProcessImageAsync(fileInfo, imageModel).ConfigureAwait(false);
            }
            else
            {
                // Unknown format - try default handler
                await new DefaultImageHandler().ProcessImageAsync(fileInfo, imageModel).ConfigureAwait(false);
            }

            return imageModel;
        }
        catch (Exception e)
        {
            LogError($"Error processing {fileInfo.Name}: {e.Message}");
            return CreateErrorImageModel(fileInfo);
        }
    }

    private static void LogError(string message)
    {
#if DEBUG
        Console.WriteLine($"Error: {nameof(GetImageModel)}:{nameof(GetImageModelAsync)}: {message}");
#endif
    }

    private static ImageModel CreateErrorImageModel(FileInfo? fileInfo)
    {
        return new ImageModel
        {
            FileInfo = fileInfo,
            ImageType = ImageType.Invalid,
            Image = null, // TODO replace with error image
            PixelHeight = 0,
            PixelWidth = 0,
            EXIFOrientation = EXIFHelper.EXIFOrientation.None
        };
    }

    #region Image Handlers

    // Base abstract class for all image handlers
    private abstract class ImageHandler
    {
        public abstract Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel);

        protected static void SetBitmapModel(Bitmap bitmap, FileInfo fileInfo, ImageModel imageModel, ImageType imageType = ImageType.Bitmap)
        {
            imageModel.Image = bitmap;
            imageModel.PixelWidth = bitmap?.PixelSize.Width ?? 0;
            imageModel.PixelHeight = bitmap?.PixelSize.Height ?? 0;
            imageModel.ImageType = imageType;
            imageModel.EXIFOrientation = EXIFHelper.GetImageOrientation(fileInfo);
        }
    }

    // Handler for standard bitmap formats
    private class StandardBitmapHandler : ImageHandler
    {
        public override async Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            var bitmap = await GetImage.GetStandardBitmapAsync(fileInfo).ConfigureAwait(false);
            SetBitmapModel(bitmap, fileInfo, imageModel);
        }
    }

    // Handler for animated WebP
    private class AnimatedWebpHandler : ImageHandler
    {
        public override async Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            var bitmap = await GetImage.GetStandardBitmapAsync(fileInfo).ConfigureAwait(false);
            SetBitmapModel(bitmap, fileInfo, imageModel, 
                ImageAnalyzer.IsAnimated(fileInfo) ? ImageType.AnimatedWebp : ImageType.Bitmap);
        }
    }

    // Handler for animated GIF
    private class AnimatedGifHandler : ImageHandler
    {
        public override async Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            var bitmap = await GetImage.GetStandardBitmapAsync(fileInfo).ConfigureAwait(false);
            SetBitmapModel(bitmap, fileInfo, imageModel, 
                ImageAnalyzer.IsAnimated(fileInfo) ? ImageType.AnimatedGif : ImageType.Bitmap);
        }
    }

    // Handler for SVG images
    private class SvgHandler : ImageHandler
    {
        public override Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            using var svg = new MagickImage();
            svg.Ping(fileInfo.FullName);
            
            imageModel.PixelWidth = (int)svg.Width;
            imageModel.PixelHeight = (int)svg.Height;
            imageModel.ImageType = ImageType.Svg;
            imageModel.Image = fileInfo.FullName;
            
            return Task.CompletedTask;
        }
    }

    // Handler for Base64 encoded images
    private class Base64Handler : ImageHandler
    {
        public override async Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            var bitmap = await GetImage.GetBase64ImageAsync(fileInfo).ConfigureAwait(false);
            SetBitmapModel(bitmap, fileInfo, imageModel);
        }
    }

    // Default handler for unknown formats
    private class DefaultImageHandler : ImageHandler
    {
        public override async Task ProcessImageAsync(FileInfo fileInfo, ImageModel imageModel)
        {
            var bitmap = await GetImage.GetDefaultBitmapAsync(fileInfo).ConfigureAwait(false);
            SetBitmapModel(bitmap, fileInfo, imageModel);
        }
    }

    #endregion
}