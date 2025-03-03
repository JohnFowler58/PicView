using PicView.Avalonia.ViewModels;
using PicView.Core.Localization;

namespace PicView.Avalonia.SettingsManagement;

public static class LanguageUpdater
{
    public static async Task UpdateLanguageAsync(MainViewModel vm, bool settingsExists)
    {
        if (settingsExists)
        {
            await TranslationHelper.LoadLanguage(Settings.UIProperties.UserLanguage).ConfigureAwait(false);
        }
        else
        {
            await TranslationHelper.DetermineAndLoadLanguage().ConfigureAwait(false);
        }

        vm.UpdateLanguage();

        vm.GetIsFlippedTranslation = vm.ScaleX == 1 ? vm.Flip : vm.UnFlip;
        
        vm.GetIsShowingUITranslation = !Settings.UIProperties.ShowInterface ? vm.ShowUI : vm.HideUI;
        
        vm.GetIsScrollingTranslation = Settings.Zoom.ScrollEnabled ?
            TranslationHelper.Translation.ScrollingEnabled : TranslationHelper.Translation.ScrollingDisabled;
        
        vm.GetIsShowingBottomGalleryTranslation = Settings.Gallery.IsBottomGalleryShown ?
            TranslationHelper.Translation.HideBottomGallery :
            TranslationHelper.Translation.ShowBottomGallery;
        
        vm.GetIsLoopingTranslation = Settings.UIProperties.Looping
            ? TranslationHelper.Translation.LoopingEnabled
            : TranslationHelper.Translation.LoopingDisabled;
        
        vm.GetIsCtrlZoomTranslation = Settings.Zoom.CtrlZoom
            ? TranslationHelper.Translation.CtrlToZoom
            : TranslationHelper.Translation.ScrollToZoom;
        
        vm.GetIsShowingBottomToolbarTranslation = Settings.UIProperties.ShowBottomNavBar
            ? TranslationHelper.Translation.HideBottomToolbar
            : TranslationHelper.Translation.ShowBottomToolbar;
        
        vm.GetIsShowingFadingUIButtonsTranslation = Settings.UIProperties.ShowAltInterfaceButtons
            ? TranslationHelper.Translation.DisableFadeInButtonsOnHover
            : TranslationHelper.Translation.ShowFadeInButtonsOnHover;
        
        vm.GetIsUsingTouchpadTranslation = Settings.Zoom.IsUsingTouchPad
            ? TranslationHelper.Translation.UsingTouchpad
            : TranslationHelper.Translation.UsingMouse;
    }
}
