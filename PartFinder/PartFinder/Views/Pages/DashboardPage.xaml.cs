using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Helpers;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;
    private readonly IThemeService _themeService;

    public DashboardPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<DashboardViewModel>();
        _themeService = App.Services.GetRequiredService<IThemeService>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _themeService.ThemeChanged += OnAppThemeChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _themeService.ThemeChanged -= OnAppThemeChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    private void OnAppThemeChanged(object? sender, EventArgs e) => ApplyChartChrome();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DashboardViewModel.ChartDrawMarginFrame)
            or nameof(DashboardViewModel.ChartXAxes)
            or nameof(DashboardViewModel.ChartYAxes)
            or nameof(DashboardViewModel.TrendXAxes)
            or nameof(DashboardViewModel.TrendSeries)
            or nameof(DashboardViewModel.StockLevelSeries))
        {
            ApplyChartBindings();
        }
    }

    private void ApplyChartChrome()
    {
        try
        {
            ApplyChartBindings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] ApplyChartChrome failed: {ex}");
        }
    }

    private void ApplyChartBindings()
    {
        try
        {
            var frame = _viewModel.ChartDrawMarginFrame;
            TrendChart.DrawMarginFrame = frame;
            StockLevelChart.DrawMarginFrame = frame;

            TrendChart.XAxes = _viewModel.TrendXAxes is { Length: > 0 }
                ? _viewModel.TrendXAxes
                : _viewModel.ChartXAxes;
            TrendChart.YAxes = _viewModel.ChartYAxes;
            StockLevelChart.XAxes = _viewModel.ChartXAxes;
            StockLevelChart.YAxes = _viewModel.ChartYAxes;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] ApplyChartBindings failed: {ex}");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyChartChrome();

            PlayEntrance(HeaderGrid, HeaderSlide, delay: 0);
            PlayEntrance(KpiRow, KpiSlide, delay: 80);
            PlayEntrance(Row1, Row1Slide, delay: 160);
            PlayEntrance(Row2, Row2Slide, delay: 240);
            PlayEntrance(Row3, Row3Slide, delay: 320);

            StartPulseDot();

            if (_viewModel.LazyLoadTrendCommand.CanExecute(null))
            {
                await _viewModel.LazyLoadTrendCommand.ExecuteAsync(null);
            }

            if (_viewModel.HasLowStock)
            {
                AnimateBannerIn();
            }

            _viewModel.PropertyChanged += OnHasLowStockChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DashboardPage] OnLoaded failed: {ex}");
            EnsureDashboardVisible();
        }
    }

    private void OnHasLowStockChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(DashboardViewModel.HasLowStock) && _viewModel.HasLowStock)
        {
            AnimateBannerIn();
        }
    }

    private void EnsureDashboardVisible()
    {
        HeaderGrid.Opacity = 1;
        KpiRow.Opacity = 1;
        Row1.Opacity = 1;
        Row2.Opacity = 1;
        Row3.Opacity = 1;
        LowStockBanner.Opacity = _viewModel.HasLowStock ? 1 : 0;
    }

    // ── Entrance slide-fade ───────────────────────────────────────────────────
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
        sb.Completed += (_, _) =>
        {
            if (element.Opacity < 0.99)
            {
                element.Opacity = 1;
            }
        };
        sb.Begin();
    }

    // ── Pulsing online dot ────────────────────────────────────────────────────
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

    // ── Banner slide-in ───────────────────────────────────────────────────────
    private void AnimateBannerIn()
    {
        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(640),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3 },
        };
        Storyboard.SetTarget(fadeIn, LowStockBanner);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var borderPulse = new ColorAnimation
        {
            From = Windows.UI.Color.FromArgb(40, 255, 183, 129),
            To   = Windows.UI.Color.FromArgb(120, 255, 183, 129),
            Duration = TimeSpan.FromSeconds(1.8),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(borderPulse, BannerBorderBrush);
        Storyboard.SetTargetProperty(borderPulse, "Color");

        var iconBounce = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        iconBounce.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero),          Value = 0 });
        iconBounce.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4)), Value = -4,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseOut } });
        iconBounce.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.8)), Value = 0,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseIn } });
        iconBounce.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromSeconds(2.5)), Value = 0 });
        Storyboard.SetTarget(iconBounce, BannerIcon);
        BannerIcon.RenderTransform = new Microsoft.UI.Xaml.Media.TranslateTransform();
        Storyboard.SetTargetProperty(iconBounce, "(UIElement.RenderTransform).(TranslateTransform.Y)");

        var sb = new Storyboard();
        sb.Children.Add(fadeIn);
        sb.Children.Add(borderPulse);
        sb.Children.Add(iconBounce);
        sb.Begin();
    }
}
