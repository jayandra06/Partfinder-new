using Microsoft.UI.Xaml.Data;

namespace PartFinder.Converters;

/// <summary>
/// When false, <c>x:Load</c> unloads section UI so collapsed workbooks do not keep duplicate ComboBox flyouts in the tree.
/// </summary>
public sealed class WmsSectionEqualsToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var current = value?.ToString();
        var target = parameter?.ToString();
        return string.Equals(current, target, StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}