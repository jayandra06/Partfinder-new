namespace PartFinder.Models;

public sealed class PartTemplateDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Version { get; init; }
    public bool IsPublished { get; init; }

    /// <summary>Optional parent template — fields are merged (child overrides by key).</summary>
    public string? BaseTemplateId { get; init; }

    /// <summary>When true, catalog appears in Master Data. Organization templates created on the Templates page use false.</summary>
    public bool IsMasterDataCatalog { get; init; } = true;

    public required IReadOnlyList<TemplateFieldDefinition> Fields { get; init; }

    public string ColumnCountLabel => Fields.Count == 1 ? "1 column" : $"{Fields.Count} columns";

    public override string ToString() => Name;
}
