using PartFinder.Models;

namespace PartFinder.Services;

public interface IContextActionsService
{
    Task<IReadOnlyList<TemplateContextAction>> GetForSourceTemplateAsync(
        string sourceTemplateId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(TemplateContextAction action, CancellationToken cancellationToken = default);

    Task DeleteAsync(string actionId, CancellationToken cancellationToken = default);
}
