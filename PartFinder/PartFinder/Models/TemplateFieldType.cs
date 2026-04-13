namespace PartFinder.Models;

public enum TemplateFieldType
{
    Text,
    Number,
    Decimal,
    Dropdown,
    Date,
    Boolean,

    /// <summary>Optional link to a row in another template (stored value = target row id).</summary>
    RecordLink,
}
