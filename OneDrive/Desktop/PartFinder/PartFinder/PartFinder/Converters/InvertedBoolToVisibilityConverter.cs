using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

/// <summary>Visible when value is false; collapsed when true (e.g. hide labels when sidebar is collapsed).</summary>
public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b && b)
        {
            return Visibility.Collapsed;
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}
