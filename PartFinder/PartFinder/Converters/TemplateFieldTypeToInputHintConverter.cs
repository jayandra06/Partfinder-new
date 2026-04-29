using Microsoft.UI.Xaml.Data;
using PartFinder.Models;

namespace PartFinder.Converters;

public sealed class TemplateFieldTypeToInputHintConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TemplateFieldType fieldType)
        {
            return "Column label";
        }

        return fieldType switch
        {
            TemplateFieldType.Number => "e.g. 100",
            TemplateFieldType.Decimal => "e.g. 99.95",
            TemplateFieldType.Date => "e.g. 2026-04-29",
            TemplateFieldType.Boolean => "Yes / No",
            TemplateFieldType.Dropdown => "Select value",
            TemplateFieldType.RecordLink => "Linked record",
            _ => "Column label",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        return TemplateFieldType.Text;
    }
}
