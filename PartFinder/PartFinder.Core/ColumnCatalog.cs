namespace PartFinder.Core;

/// <summary>Reusable column definitions for parts / vendor templates.</summary>
public static class ColumnCatalog
{
    public sealed record Entry(string Key, string Label, string Category);

    public static IReadOnlyList<Entry> StandardColumns { get; } =
    [
        new("brand", "Brand", "Vehicle"),
        new("model", "Model", "Vehicle"),
        new("category", "Category", "Vehicle"),
        new("part_no", "Part No", "Part identity"),
        new("position_no", "Position No", "Part identity"),
        new("vendor", "Vendor", "Vendor"),
        new("vendor_warehouse", "Vendor Warehouse", "Vendor"),
        new("vendor_phone", "Vendor Phone Number", "Vendor"),
        new("vendor_type", "Type", "Vendor"),
    ];

    public static IReadOnlyList<Entry> GetMissingByLabel(IEnumerable<string> existingLabels)
    {
        var existing = new HashSet<string>(existingLabels, StringComparer.OrdinalIgnoreCase);
        return StandardColumns.Where(c => !existing.Contains(c.Label)).ToList();
    }
}
