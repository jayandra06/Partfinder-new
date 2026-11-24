using PartFinder.Models;

namespace PartFinder.Services;

public interface IMasterDataRecordsService
{
    Task<IReadOnlyList<MasterDataRowRecord>> GetRowsAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rows stored for the template. Does not substitute the shared parts catalog.
    /// </summary>
    Task<IReadOnlyList<MasterDataRowRecord>> GetStoredRowsAsync(string templateId, CancellationToken cancellationToken = default);

    Task<int> GetRowCountAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>Returns a page of rows and total row count without loading the full catalog.</summary>
    Task<(IReadOnlyList<MasterDataRowRecord> Rows, int TotalCount)> GetRowsPageAsync(
        string templateId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

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

    Task<bool> DeleteRowAsync(
        string templateId,
        string rowId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every data row for the template; does not change the template schema.</summary>
    Task<long> DeleteAllRowsForTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default);

    /// <summary>Upserts every row and deletes the listed row ids in one write.</summary>
    Task<IReadOnlyList<string>> SaveRowsAsync(
        string templateId,
        IReadOnlyList<(string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)> rows,
        IReadOnlyList<string> deleteRowIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts many new rows for a template in batches. Reports how many rows have been written so far.
    /// </summary>
    Task<int> AppendRowsAsync(
        string templateId,
        IEnumerable<IReadOnlyDictionary<string, string>> rows,
        IProgress<int>? insertedCount = null,
        CancellationToken cancellationToken = default);
}
