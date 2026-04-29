using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using PartFinder.Models;

namespace PartFinder.Converters;

public sealed class TemplateFieldTypeEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TemplateFieldType fieldType || parameter is not string raw)
        {
            return Visibility.Collapsed;
        }

        if (!Enum.TryParse<TemplateFieldType>(raw, ignoreCase: true, out var expected))
        {
            return Visibility.Collapsed;
        }

        return fieldType == expected ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return TemplateFieldType.Text;
    }
}
