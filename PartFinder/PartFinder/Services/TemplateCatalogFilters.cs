using PartFinder.Models;

namespace PartFinder.Services;

public static class TemplateCatalogFilters
{
    public static IReadOnlyList<PartTemplateDefinition> MasterDataCatalogs(
        IReadOnlyList<PartTemplateDefinition> templates) =>
        templates.Where(t => t.IsMasterDataCatalog).ToList();

    public static IReadOnlyList<PartTemplateDefinition> OrganizationTemplates(
        IReadOnlyList<PartTemplateDefinition> templates) =>
        templates.Where(t => !t.IsMasterDataCatalog).ToList();
}
