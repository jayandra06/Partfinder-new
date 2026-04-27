using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

public sealed class BoolToStarGlyphConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isFavorite)
        {
            return isFavorite ? "\uE735" : "\uE734"; // Filled star vs outline star
        }
        return "\uE734"; // Default to outline star
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}