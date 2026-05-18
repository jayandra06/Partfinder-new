namespace PartFinder.Core;

public static class WorksheetRelationUiText
{
    public static IReadOnlyList<string> GetSharedColumnNames(
        IEnumerable<string> primaryLabels,
        IEnumerable<string> lookupLabels)
    {
        var lookup = new HashSet<string>(lookupLabels, StringComparer.OrdinalIgnoreCase);
        return primaryLabels
            .Where(label => !string.IsNullOrWhiteSpace(label) && lookup.Contains(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string GetMatchColumnHint(IReadOnlyList<string> selectedMatchColumns)
    {
        if (selectedMatchColumns.Count == 0)
        {
            return "Choose one or more columns to match on.";
        }

        if (selectedMatchColumns.Count == 1)
        {
            return $"Matching when \"{selectedMatchColumns[0]}\" equals a row in the lookup template.";
        }

        var joined = string.Join(", ", selectedMatchColumns);
        return $"Matching when all of these equal the lookup row: {joined}.";
    }

    public static string GetMatchColumnHint(string? selectedMatchColumn) =>
        GetMatchColumnHint(
            string.IsNullOrEmpty(selectedMatchColumn)
                ? []
                : [selectedMatchColumn]);

    public static string? GetSelectedMatchColumn(IEnumerable<(string Name, bool IsChecked)> columns) =>
        columns.FirstOrDefault(c => c.IsChecked).Name;

    public static string GetSharedColumnsSummary(int sharedCount) =>
        sharedCount == 0
            ? "No columns share the same name in both templates."
            : $"{sharedCount} shared column{(sharedCount == 1 ? "" : "s")} — use “Match all shared” to link them.";
}
