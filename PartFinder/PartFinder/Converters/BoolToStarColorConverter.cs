using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;

namespace PartFinder.Converters;

public sealed class BoolToStarColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isFavorite)
        {
            if (isFavorite)
            {
                // Return accent color for favorited items
                return Application.Current.Resources["AccentPrimaryBrush"] as SolidColorBrush 
                       ?? new SolidColorBrush(Microsoft.UI.Colors.Gold);
            }
            else
            {
                // Return secondary text color for non-favorited items
                return Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush 
                       ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
            }
        }
        return Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush 
               ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}