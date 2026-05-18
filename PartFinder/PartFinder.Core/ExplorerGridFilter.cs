namespace PartFinder.Core;

public enum ExplorerRowMatchFilter
{
    All,
    MatchedOnly,
    UnmatchedOnly,
}

public static class ExplorerGridFilter
{
    public static bool RowMatchesSearch(
        IReadOnlyDictionary<string, string> cells,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        return cells.Values.Any(
            v => !string.IsNullOrEmpty(v)
                 && v.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    public static bool RowMatchesLinkFilter(
        ExplorerRowMatchFilter filter,
        bool hasRelationConfigured,
        bool rowMatched)
    {
        return filter switch
        {
            ExplorerRowMatchFilter.MatchedOnly => hasRelationConfigured && rowMatched,
            ExplorerRowMatchFilter.UnmatchedOnly => hasRelationConfigured && !rowMatched,
            _ => true,
        };
    }

    public static RelationHealthSummary ComputeHealth(int totalRows, int matchedRows, int relationCount) =>
        new(totalRows, matchedRows, relationCount);
}

public sealed record RelationHealthSummary(int TotalRows, int MatchedRows, int RelationCount)
{
    public int UnmatchedRows => Math.Max(0, TotalRows - MatchedRows);

    public int MatchRatePercent => TotalRows == 0 ? 0 : (int)Math.Round(100.0 * MatchedRows / TotalRows);

    public string SummaryText =>
        RelationCount == 0
            ? "No template links configured."
            : $"{MatchedRows}/{TotalRows} rows matched ({MatchRatePercent}%) across {RelationCount} link(s).";
}
