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
}
