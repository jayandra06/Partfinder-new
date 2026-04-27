using PartFinder.Models;

namespace PartFinder.ViewModels;

/// <summary>Row for Templates UI list (includes resolved target template name).</summary>
public sealed class ContextActionListRow
{
    public required TemplateContextAction Action { get; init; }

    public required string TargetTemplateName { get; init; }

    public string SummaryLine =>
        $"{Action.MenuLabel} — column {Action.SourceFieldKey} → {TargetTemplateName} ({Action.MatchRules.Count} rule(s))";
}
