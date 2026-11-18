using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

public sealed class WmsSectionEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var current = value?.ToString();
        var target = parameter?.ToString();
        return current == target ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}