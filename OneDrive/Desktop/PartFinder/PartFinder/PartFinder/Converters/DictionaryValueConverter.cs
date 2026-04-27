using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

public sealed class DictionaryValueConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is IReadOnlyDictionary<string, object?> dictionary && parameter is string key)
        {
            return dictionary.TryGetValue(key, out var current) ? current : null;
        }

        if (value is Dictionary<string, object?> mutableDictionary && parameter is string mutableKey)
        {
            return mutableDictionary.TryGetValue(mutableKey, out var current) ? current : null;
        }

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => value;
}
