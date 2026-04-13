namespace PartFinder.Models;

/// <summary>
/// One AND condition: target row field must equal a value from the source row or a literal.
/// </summary>
public sealed class ContextActionMatchRule
{
    public required string TargetFieldKey { get; init; }

    /// <summary>When set, compared to this cell on the source row (same template as the right-clicked cell).</summary>
    public string? SourceFieldKey { get; init; }

    /// <summary>When set, compared to this fixed string instead of <see cref="SourceFieldKey"/>.</summary>
    public string? LiteralValue { get; init; }
}
