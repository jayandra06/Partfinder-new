namespace PartFinder.Models;

public sealed class TemplateFieldDefinition
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public TemplateFieldType Type { get; init; }
    public bool IsRequired { get; init; }
    public int DisplayOrder { get; init; }
    public string? ValidationPattern { get; init; }
    public IReadOnlyList<string>? Options { get; init; }

    /// <summary>When <see cref="Type"/> is <see cref="TemplateFieldType.RecordLink"/>, id of the target template.</summary>
    public string? LinkedTemplateId { get; init; }

    /// <summary>Optional field key on the target template used as the visible label; default = first column.</summary>
    public string? LinkedDisplayFieldKey { get; init; }
}
