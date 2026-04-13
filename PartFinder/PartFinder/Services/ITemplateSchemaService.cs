using PartFinder.Models;

namespace PartFinder.Services;

public interface ITemplateSchemaService
{
    Task<IReadOnlyList<PartTemplateDefinition>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<PartTemplateDefinition?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);

    Task SaveTemplateAsync(PartTemplateDefinition template, CancellationToken cancellationToken = default);
}
