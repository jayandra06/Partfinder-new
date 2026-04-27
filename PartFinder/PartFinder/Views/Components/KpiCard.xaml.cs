using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.ViewModels;
using Windows.UI;

namespace PartFinder.Views.Components;

public sealed partial class KpiCard : UserControl
{
    // ── Dependency properties ─────────────────────────────────────────────────
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DeltaProperty =
        DependencyProperty.Register(nameof(Delta), typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconGlyphProperty =
        DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(KpiCard),
            new PropertyMetadata("\uE9D9", (d, _) => ((KpiCard)d).ApplyStyle()));

    public static readonly DependencyProperty AccentHexProperty =
        DependencyProperty.Register(nameof(AccentHex), typeof(string), typeof(KpiCard),
            new PropertyMetadata("#1F7AE0", (d, _) => ((KpiCard)d).ApplyStyle()));

    public static readonly DependencyProperty AlertLevelProperty =
        DependencyProperty.Register(nameof(AlertLevel), typeof(KpiAlertLevel), typeof(KpiCard),
            new PropertyMetadata(KpiAlertLevel.Normal, (d, _) => ((KpiCard)d).ApplyAlertLevel()));

    // ── CLR wrappers ──────────────────────────────────────────────────────────
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Delta
    {
        get => (string)GetValue(DeltaProperty);
        set => SetValue(DeltaProperty, value);
    }

    public string IconGlyph
    {
        get => (string)GetValue(IconGlyphProperty);
        set => SetValue(IconGlyphProperty, value);
    }

    public string AccentHex
    {
        get => (string)GetValue(AccentHexProperty);
        set => SetValue(AccentHexProperty, value);
    }

    public KpiAlertLevel AlertLevel
    {
        get => (KpiAlertLevel)GetValue(AlertLevelProperty);
        set => SetValue(AlertLevelProperty, value);
    }

    // ── Constructor ───────────────────────────────────────────────────────────
    public KpiCard()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ApplyStyle();
            ApplyAlertLevel();
            PlayEntrance();
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void PlayEntrance()
    {
        if (Resources["EntranceSb"] is Storyboard sb)
            sb.Begin();
    }

    private void ApplyStyle()
    {
        if (!IsLoaded) return;

        var color = ParseHex(AccentHex);
        var brush = new SolidColorBrush(color);

        // Icon
        CardIcon.Glyph = IconGlyph;
        CardIcon.Foreground = brush;

        // Icon circle background (10% opacity)
        IconCircle.Background = new SolidColorBrush(Color.FromArgb(26, color.R, color.G, color.B));

        // Accent bar
        AccentBar.Background = brush;
    }

    private void ApplyAlertLevel()
    {
        if (!IsLoaded) return;

        // Stop any running pulse first
        Storyboard? pulse = null;
        if (Resources["PulseSb"] is Storyboard pulseSb)
        {
            pulse = pulseSb;
            pulse.Stop();
        }

        switch (AlertLevel)
        {
            case KpiAlertLevel.Warning:
                AlertGlowBrush.Color = ParseHex("#FFB781");
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 183, 129));
                DeltaBadge.Background = new SolidColorBrush(Color.FromArgb(26, 255, 183, 129));
                DeltaText.Foreground = new SolidColorBrush(ParseHex("#FFB781"));
                pulse?.Begin();
                break;

            case KpiAlertLevel.Danger:
                AlertGlowBrush.Color = ParseHex("#FFB4AB");
                RootBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 100, 100));
                DeltaBadge.Background = new SolidColorBrush(Color.FromArgb(26, 255, 100, 100));
                DeltaText.Foreground = new SolidColorBrush(ParseHex("#FFB4AB"));
                pulse?.Begin();
                break;

            default:
                AlertGlow.Opacity = 0;
                RootBorder.BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderDefaultBrush"];
                DeltaBadge.Background = new SolidColorBrush(Color.FromArgb(26, 42, 189, 143));
                DeltaText.Foreground = (SolidColorBrush)Application.Current.Resources["SuccessBrush"];
                break;
        }
    }

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            hex = "FF" + hex;
        if (hex.Length != 8) return Colors.White;
        return Color.FromArgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16));
    }
}
