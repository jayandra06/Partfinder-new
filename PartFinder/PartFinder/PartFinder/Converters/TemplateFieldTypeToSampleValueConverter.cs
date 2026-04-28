using Microsoft.UI.Xaml.Data;
using PartFinder.Models;

namespace PartFinder.Converters;

public sealed class TemplateFieldTypeToSampleValueConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not TemplateFieldType fieldType)
        {
            return "sample";
        }

        return fieldType switch
        {
            TemplateFieldType.Text => "Sample text",
            TemplateFieldType.Number => "0",
            TemplateFieldType.Decimal => "0.00",
            TemplateFieldType.Dropdown => "Option A",
            TemplateFieldType.Date => "YYYY-MM-DD",
            TemplateFieldType.Boolean => "Yes / No",
            TemplateFieldType.RecordLink => "Linked record",
            _ => "sample",
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
