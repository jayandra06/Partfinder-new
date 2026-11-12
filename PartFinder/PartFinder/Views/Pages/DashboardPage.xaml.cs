using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureDashboardVisible();

            PlayEntrance(HeaderGrid, HeaderSlide, delay: 0);
            PlayEntrance(FavouritesRow, FavouritesSlide, delay: 40);

            StartPulseDot();

            await _viewModel.PrepareForPageDisplayAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] OnLoaded failed: {ex}");
            EnsureDashboardVisible();
        }
    }

    private void OnFavouriteOpenClick(object sender, RoutedEventArgs e)
    {
        if (!TryGetFavouriteFromSender(sender, out var item))
        {
            return;
        }

        _viewModel.OpenFavouriteCommand.Execute(item);
    }

    private async void OnFavouriteRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            btn.IsEnabled = false;
        }

        if (!TryGetFavouriteFromSender(sender, out var item))
        {
            if (sender is Button b)
            {
                b.IsEnabled = true;
            }

            return;
        }
        if (item is null)
        {
            return;
        }

        await _viewModel.RemoveFavouriteAsync(item).ConfigureAwait(true);

        if (sender is Button btnDone)
        {
            btnDone.IsEnabled = true;
        }
    }

    private bool TryGetFavouriteFromSender(object sender, out DashboardFavouriteItem? item)
    {
        item = null;
        if (sender is not FrameworkElement fe || fe.Tag is not string templateId)
        {
            return false;
        }

        item = _viewModel.FavouriteItems.FirstOrDefault(f => f.TemplateId == templateId);
        return item is not null;
    }

    private void EnsureDashboardVisible()
    {
        HeaderGrid.Opacity = 1;
        FavouritesRow.Opacity = 1;
    }

    private static void PlayEntrance(UIElement element, Microsoft.UI.Xaml.Media.TranslateTransform slide, int delay)
    {
        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(520),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 },
        };
        Storyboard.SetTarget(fadeIn, element);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var slideUp = new DoubleAnimation
        {
            From = 18, To = 0,
            Duration = TimeSpan.FromMilliseconds(520),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 },
        };
        Storyboard.SetTarget(slideUp, slide);
        Storyboard.SetTargetProperty(slideUp, "Y");

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Children.Add(slideUp);
        sb.Begin();
    }

    private void StartPulseDot()
    {
        var scaleX = new DoubleAnimation { From = 0.4, To = 1.8, Duration = TimeSpan.FromSeconds(2.1) };
        var scaleY = new DoubleAnimation { From = 0.4, To = 1.8, Duration = TimeSpan.FromSeconds(2.1) };
        var fade   = new DoubleAnimation { From = 0.7, To = 0,   Duration = TimeSpan.FromSeconds(2.1) };

        Storyboard.SetTarget(scaleX, PulseRing);
        Storyboard.SetTarget(scaleY, PulseRing);
        Storyboard.SetTarget(fade,   PulseRing);
        Storyboard.SetTargetProperty(scaleX, "(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
        Storyboard.SetTargetProperty(scaleY, "(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
        Storyboard.SetTargetProperty(fade,   "Opacity");

        PulseRing.RenderTransform = new Microsoft.UI.Xaml.Media.ScaleTransform
        {
            CenterX = 5, CenterY = 5
        };

        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        sb.Children.Add(scaleX);
        sb.Children.Add(scaleY);
        sb.Children.Add(fade);
        sb.Begin();
    }
}
