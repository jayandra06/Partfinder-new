using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        // Staggered entrance — each row slides up with increasing delay
        AnimateIn(HeaderGrid, HeaderSlide, 0);
        AnimateIn(KpiRow, KpiSlide, 100);
        AnimateIn(Row1, Row1Slide, 220);
        AnimateIn(Row2, Row2Slide, 360);

        // Load real data
        if (_viewModel.LazyLoadTrendCommand.CanExecute(null))
        {
            await _viewModel.LazyLoadTrendCommand.ExecuteAsync(null);
        }
    }

    private static void AnimateIn(UIElement element, TranslateTransform slide, int delayMs)
    {
        var fade = new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(500),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, element);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var moveUp = new DoubleAnimation
        {
            From = slide.Y,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(500),
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(moveUp, slide);
        Storyboard.SetTargetProperty(moveUp, "Y");

        var sb = new Storyboard();
        sb.Children.Add(fade);
        sb.Children.Add(moveUp);
        sb.Begin();
    }
}
