using PartFinder.Models;

namespace PartFinder.Services;

public interface IPartsDataService
{
    Task<(IReadOnlyList<PartRecord> Records, bool HasMore)> GetPageAsync(
        string templateId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default);
}
