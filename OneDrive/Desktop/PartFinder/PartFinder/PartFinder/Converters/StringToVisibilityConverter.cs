using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

/// <summary>Visible when the value is a non-empty string; otherwise collapsed.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is string s && !string.IsNullOrWhiteSpace(s)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
