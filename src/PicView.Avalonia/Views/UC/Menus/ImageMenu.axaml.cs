using System.Reactive.Linq;
using Avalonia.Input;
using Avalonia.Media;
using PicView.Avalonia.Crop;
using PicView.Avalonia.CustomControls;
using PicView.Avalonia.Navigation;
using PicView.Avalonia.ViewModels;
using ReactiveUI;

namespace PicView.Avalonia.Views.UC.Menus;

public partial class ImageMenu  : AnimatedMenu
{
    public ImageMenu()
    {
        InitializeComponent();
        Loaded += delegate
        {
            if (Settings.Theme.GlassTheme)
            {
               GoToPicButton.Classes.Remove("noBorderHover");
               GoToPicButton.Classes.Add("hover");
               GoToPicBox.Background = new SolidColorBrush(Color.FromArgb(90, 197, 197, 197));
            }
            else if (!Settings.Theme.Dark)
            {
                TopBorder.Background = Brushes.White;
            }
            GoToPicBox.KeyDown += async (_, e) => await GoToPicBox_OnKeyDown(e);
            this.WhenAnyValue(x => x.IsVisible)
                .Where(isVisible => !isVisible).Subscribe(_ => SlideShowButton.Flyout.Hide());
            this.WhenAnyValue(x => x.IsOpen).Subscribe(_ => DetermineIfCropShouldBeEnabled());
        };
    }

    private void DetermineIfCropShouldBeEnabled()
    {
        if (DataContext is not MainViewModel vm)
        {
            return;
        }

        CropFunctions.DetermineIfShouldBeEnabled(vm);
    }

    private async Task GoToPicBox_OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is not MainViewModel vm)
            {
                return;
            }

            if (!NavigationManager.CanNavigate(vm))
            {
                return;
            }

            if (!int.TryParse(GoToPicBox.Text, out var number))
            {
                return;
            }

            if (number < 1)
            {
                number = 0;
            }
            else if (number > NavigationManager.GetCount)
            {
                number = NavigationManager.GetCount - 1;
            }
            else
            {
                number--;
            }

            await NavigationManager.Navigate(number, vm).ConfigureAwait(false);
        }
    }
}