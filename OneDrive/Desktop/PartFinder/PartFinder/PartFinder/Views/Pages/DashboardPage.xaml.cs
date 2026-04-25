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
        // Staggered entrance animations
        PlayEntrance(HeaderGrid,    HeaderSlide,  delay: 0);
        PlayEntrance(KpiRow,        KpiSlide,     delay: 80);
        PlayEntrance(Row1,          Row1Slide,    delay: 160);
        PlayEntrance(Row2,          Row2Slide,    delay: 240);
        PlayEntrance(Row3,          Row3Slide,    delay: 320);

        // Pulse the online dot forever
        StartPulseDot();

        // Load data
        if (_viewModel.LazyLoadTrendCommand.CanExecute(null))
        {
            await _viewModel.LazyLoadTrendCommand.ExecuteAsync(null);
        }

        // Animate banner in if low stock
        if (_viewModel.HasLowStock)
            AnimateBannerIn();

        // Watch for HasLowStock changes after data loads
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(DashboardViewModel.HasLowStock) && _viewModel.HasLowStock)
                AnimateBannerIn();
        };
    }

    // ── Entrance slide-fade ───────────────────────────────────────────────────
    private static void PlayEntrance(UIElement element, Microsoft.UI.Xaml.Media.TranslateTransform slide, int delay)
    {
        var fadeIn = new DoubleAnimation
        {
            From = 0, To = 1,
            Duration = TimeSpan.FromMilliseconds(400),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 4 },
        };
        Storyboard.SetTarget(fadeIn, element);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        var slideUp = new DoubleAnimation
        {
            From = 14, To = 0,
            Duration = TimeSpan.FromMilliseconds(400),
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

    // ── Pulsing online dot ────────────────────────────────────────────────────
    private void StartPulseDot()
    {
        // Scale pulse ring out and fade
        var scaleX = new DoubleAnimation { From = 0.4, To = 1.8, Duration = TimeSpan.FromSeconds(1.6) };
        var scaleY = new DoubleAnimation { From = 0.4, To = 1.8, Duration = TimeSpan.FromSeconds(1.6) };
        var fade   = new DoubleAnimation { From = 0.7, To = 0,   Duration = TimeSpan.FromSeconds(1.6) };

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
            Duration = TimeSpan.FromMilliseconds(500),
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 3 },
        };
        Storyboard.SetTarget(fadeIn, LowStockBanner);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");

        // Pulsing border glow on banner
        var borderPulse = new ColorAnimation
        {
            From = Windows.UI.Color.FromArgb(40, 255, 183, 129),
            To   = Windows.UI.Color.FromArgb(120, 255, 183, 129),
            Duration = TimeSpan.FromSeconds(1.2),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
        };
        Storyboard.SetTarget(borderPulse, BannerBorderBrush);
        Storyboard.SetTargetProperty(borderPulse, "Color");

        // Icon bounce
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
