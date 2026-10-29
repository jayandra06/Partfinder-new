using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace PartFinder.Helpers;

/// <summary>
/// Safe access to app-wide theme resources for code-built UI (dialogs, dynamic fields).
/// Page-local <see cref="FrameworkElement.Resources"/> does not include merged App.xaml brushes.
/// </summary>
internal static class UiResourceHelper
{
    private static readonly SolidColorBrush LightInputBackground = new(Colors.White);
    private static readonly SolidColorBrush LightInputForeground = new(ColorHelper.FromArgb(255, 17, 24, 39));
    private static readonly SolidColorBrush LightInputBorder = new(ColorHelper.FromArgb(255, 209, 213, 219));
    private static readonly SolidColorBrush LightInputPlaceholder = new(ColorHelper.FromArgb(255, 156, 163, 175));

    public static Brush TryAppBrush(string key, string fallbackHex)
    {
        if (Application.Current.Resources.TryGetValue(key, out var resource) && resource is Brush brush)
        {
            return brush;
        }

        return BrushFromHex(fallbackHex);
    }

    /// <summary>
    /// Styles a TextBox for use inside a ContentDialog without applying app Styles (avoids WinRT COM errors).
    /// </summary>
    public static void ApplyLightDialogTextBox(TextBox box)
    {
        box.RequestedTheme = ElementTheme.Light;
        box.Background = LightInputBackground;
        box.Foreground = LightInputForeground;
        box.BorderBrush = LightInputBorder;
        box.BorderThickness = new Thickness(1);
        box.PlaceholderForeground = LightInputPlaceholder;
    }

    public static void ApplyLightComboBox(ComboBox box)
    {
        box.RequestedTheme = ElementTheme.Light;
        box.Background = LightInputBackground;
        box.Foreground = LightInputForeground;
        box.BorderBrush = LightInputBorder;
        box.BorderThickness = new Thickness(1);
        box.PlaceholderForeground = LightInputPlaceholder;
    }

    /// <summary>
    /// WinUI TextBox/ComboBox templates revert unfocused chrome to dark Fluent colors; re-apply after VSM runs.
    /// </summary>
    public static void WirePersistentLightTextBox(TextBox box)
    {
        ApplyLightDialogTextBox(box);
        box.Loaded -= OnCanvasTextBoxChromeRefresh;
        box.Loaded += OnCanvasTextBoxChromeRefresh;
        box.GotFocus -= OnCanvasTextBoxChromeRefresh;
        box.GotFocus += OnCanvasTextBoxChromeRefresh;
        box.LostFocus -= OnCanvasTextBoxChromeRefresh;
        box.LostFocus += OnCanvasTextBoxChromeRefresh;
        box.PointerExited -= OnCanvasTextBoxChromeRefresh;
        box.PointerExited += OnCanvasTextBoxChromeRefresh;
    }

    public static void WirePersistentLightComboBox(ComboBox box)
    {
        ApplyLightComboBox(box);
        box.Loaded -= OnCanvasComboBoxFocusRefresh;
        box.Loaded += OnCanvasComboBoxFocusRefresh;
        box.GotFocus -= OnCanvasComboBoxFocusRefresh;
        box.GotFocus += OnCanvasComboBoxFocusRefresh;
        box.LostFocus -= OnCanvasComboBoxFocusRefresh;
        box.LostFocus += OnCanvasComboBoxFocusRefresh;
        box.DropDownClosed -= OnCanvasComboBoxDropDownClosed;
        box.DropDownClosed += OnCanvasComboBoxDropDownClosed;
    }

    private static void OnCanvasTextBoxChromeRefresh(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box)
        {
            return;
        }

        ScheduleInputChromeRefresh(box, () =>
        {
            ApplyLightDialogTextBox(box);
            PatchInputTemplateBorders(box);
        });
    }

    private static void OnCanvasComboBoxFocusRefresh(object sender, RoutedEventArgs e)
    {
        RefreshLightComboBoxChrome(sender);
    }

    private static void OnCanvasComboBoxDropDownClosed(object sender, object e)
    {
        RefreshLightComboBoxChrome(sender);
    }

    private static void RefreshLightComboBoxChrome(object sender)
    {
        if (sender is not ComboBox box)
        {
            return;
        }

        ScheduleInputChromeRefresh(box, () =>
        {
            ApplyLightComboBox(box);
            PatchInputTemplateBorders(box);
        });
    }

    private static void ScheduleInputChromeRefresh(FrameworkElement element, Action apply)
    {
        apply();
        element.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => apply());
    }

    private static void PatchInputTemplateBorders(FrameworkElement root)
    {
        foreach (var border in FindVisualChildren<Border>(root))
        {
            if (border.Background is SolidColorBrush existing && existing.Color.A == 0)
            {
                continue;
            }

            border.Background = LightInputBackground;
            if (border.BorderThickness.Left > 0 || border.BorderThickness.Top > 0)
            {
                border.BorderBrush = LightInputBorder;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    public static TextBlock CreateDialogLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 75, 85, 99)),
        };
    }

    public static void ApplyLightContentDialog(ContentDialog dialog)
    {
        dialog.RequestedTheme = ElementTheme.Light;
    }

    private static SolidColorBrush BrushFromHex(string fallbackHex)
    {
        var hex = fallbackHex.TrimStart('#');
        if (hex.Length == 6 &&
            byte.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return new SolidColorBrush(ColorHelper.FromArgb(255, r, g, b));
        }

        return new SolidColorBrush(Colors.Gray);
    }
}
