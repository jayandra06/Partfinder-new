namespace PartFinder.Models;

/// <summary>
/// User-defined right-click menu entry on a template column: opens matching rows from another template.
/// </summary>
public sealed class TemplateContextAction
{
    public required string Id { get; init; }

    public required string SourceTemplateId { get; init; }

    /// <summary>Field key on the source template where the menu appears.</summary>
    public required string SourceFieldKey { get; init; }

    public required string MenuLabel { get; init; }

    public required string TargetTemplateId { get; init; }

    public required IReadOnlyList<ContextActionMatchRule> MatchRules { get; init; }

    /// <summary>Optional column order in the results dialog; empty = all target columns in template order.</summary>
    public IReadOnlyList<string>? DisplayFieldKeys { get; init; }
}
