using PartFinder.Models;

namespace PartFinder.Services;

public interface IMasterDataRecordsService
{
    Task<IReadOnlyList<MasterDataRowRecord>> GetRowsAsync(string templateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataRowRecord>> GetRowsByIdsAsync(
        string templateId,
        IReadOnlyList<string> rowIds,
        CancellationToken cancellationToken = default);

    Task<string> UpsertRowAsync(
        string templateId,
        string? rowId,
        int rowOrder,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default);
}
