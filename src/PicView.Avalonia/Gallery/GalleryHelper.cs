using Avalonia.Media;
using PicView.Avalonia.ViewModels;

namespace PicView.Avalonia.Gallery;

public static class GalleryHelper
{
    public static void SetGalleryItemStretch(string value, MainViewModel vm)
    {
        if (value.Equals("Square", StringComparison.OrdinalIgnoreCase))
        {
            if (GalleryFunctions.IsFullGalleryOpen)
            {
                GalleryStretchMode.ChangeFullGalleryStretchSquare(vm);
            }
            else
            {
                GalleryStretchMode.ChangeBottomGalleryStretchSquare(vm);
            }

            return;
        }

        if (value.Equals("FillSquare", StringComparison.OrdinalIgnoreCase))
        {
            if (GalleryFunctions.IsFullGalleryOpen)
            {
                GalleryStretchMode.ChangeFullGalleryStretchSquareFill(vm);
            }
            else
            {
                GalleryStretchMode.ChangeBottomGalleryStretchSquareFill(vm);
            }

            return;
        }

        if (Enum.TryParse<Stretch>(value, out var stretch))
        {
            if (GalleryFunctions.IsFullGalleryOpen)
            {
                GalleryStretchMode.ChangeFullGalleryItemStretch(vm, stretch);
            }
            else
            {
                GalleryStretchMode.ChangeBottomGalleryItemStretch(vm, stretch);
            }
        }
    }
}