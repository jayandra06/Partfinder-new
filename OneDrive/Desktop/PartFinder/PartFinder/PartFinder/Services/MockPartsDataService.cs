using PartFinder.Models;

namespace PartFinder.Services;

public sealed class MockPartsDataService : IPartsDataService
{
    public async Task<(IReadOnlyList<PartRecord> Records, bool HasMore)> GetPageAsync(
        string templateId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Simulate API latency while keeping UI responsive.
        await Task.Delay(120, cancellationToken);

        const int totalRows = 100_000;
        var hasMore = offset + pageSize < totalRows;

        var rows = Enumerable.Range(offset, pageSize)
            .Where(i => i < totalRows)
            .Select(i => new PartRecord
            {
                Id = $"{templateId}-{i}",
                Values = new Dictionary<string, object?>
                {
                    ["part_no"] = $"PN-{i:000000}",
                    ["position_no"] = $"P-{i % 150}",
                    ["KF_number"] = $"KF-{1000 + (i % 7000)}",
                    ["price"] = Math.Round(20 + (i % 500) * 0.35m, 2),
                    ["remarks"] = i % 9 == 0 ? "QC required" : string.Empty,
                    ["SKU"] = $"SKU-{i:000000}",
                    ["stock_level"] = i % 250
                }
            })
            .ToList();

        return (rows, hasMore);
    }
}
