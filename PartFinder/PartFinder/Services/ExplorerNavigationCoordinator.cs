namespace PartFinder.Services;

/// <summary>Cross-page Explorer navigation (open template, share relation health).</summary>
public sealed class ExplorerNavigationCoordinator
{
    public event Action<string>? OpenTemplateRequested;

    public RelationHealthSnapshot? LastHealth { get; private set; }

    public void RequestOpenTemplate(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        OpenTemplateRequested?.Invoke(templateId);
    }

    public void PublishHealth(RelationHealthSnapshot snapshot) => LastHealth = snapshot;
}

public sealed record RelationHealthSnapshot(
    string PrimaryTemplateId,
    string PrimaryTemplateName,
    int TotalRows,
    int MatchedRows,
    int RelationCount)
{
    public int MatchRatePercent =>
        TotalRows == 0 ? 0 : (int)Math.Round(100.0 * MatchedRows / TotalRows);
}
