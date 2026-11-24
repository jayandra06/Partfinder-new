namespace PartFinder.ViewModels;

public sealed class DashboardFavouritePreviewRow
{
    public required IReadOnlyList<string> Cells { get; init; }
}

/// <summary>Favourite template with a live data preview for the home page.</summary>
public sealed class DashboardFavouriteItem
{
    public required string TemplateId { get; init; }

    public required string Name { get; init; }

    public required string KindLabel { get; init; }

    public required bool IsMasterDataCatalog { get; init; }

    public required IReadOnlyList<string> ColumnHeaders { get; init; }

    public required IReadOnlyList<DashboardFavouritePreviewRow> PreviewRows { get; init; }

    public int TotalRowCount { get; init; }

    public int TotalColumnCount { get; init; }

    public string SummaryLine =>
        TotalRowCount == 0
            ? $"{TotalColumnCount} columns · no rows yet"
            : TotalRowCount > PreviewRows.Count
                ? $"Showing {PreviewRows.Count} of {TotalRowCount:N0} rows · {ColumnHeaders.Count} of {TotalColumnCount} columns"
                : $"{TotalRowCount:N0} rows · {ColumnHeaders.Count} columns";

    public bool HasPreviewTable => ColumnHeaders.Count > 0;

    public bool HasRows => PreviewRows.Count > 0;

    public bool ShowEmptyRowsHint => HasPreviewTable && !HasRows;
}
