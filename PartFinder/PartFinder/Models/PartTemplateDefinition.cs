namespace PartFinder.Models;

public sealed class PartTemplateDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Version { get; init; }
    public bool IsPublished { get; init; }

    /// <summary>Optional parent template — fields are merged (child overrides by key).</summary>
    public string? BaseTemplateId { get; init; }

    public required IReadOnlyList<TemplateFieldDefinition> Fields { get; init; }
}
