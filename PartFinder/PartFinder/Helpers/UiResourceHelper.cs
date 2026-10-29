using Microsoft.UI;
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
        box.Background = new SolidColorBrush(Colors.White);
        box.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 24, 39));
        box.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 209, 213, 219));
        box.BorderThickness = new Thickness(1);
        box.PlaceholderForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 156, 163, 175));
    }

    public static void ApplyLightComboBox(ComboBox box)
    {
        box.RequestedTheme = ElementTheme.Light;
        box.Background = new SolidColorBrush(Colors.White);
        box.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 17, 24, 39));
        box.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 209, 213, 219));
        box.BorderThickness = new Thickness(1);
        box.PlaceholderForeground = new SolidColorBrush(ColorHelper.FromArgb(255, 156, 163, 175));
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
