using PartFinder.Core;
using PartFinder.Models;

namespace PartFinder.Services;

public static class TemplateCatalogFilters
{
    public static IReadOnlyList<PartTemplateDefinition> MasterDataCatalogs(
        IReadOnlyList<PartTemplateDefinition> templates) =>
        templates.Where(t => t.IsMasterDataCatalog).ToList();

    /// <summary>Analytics / parts templates (excludes master data catalogs).</summary>
    public static IReadOnlyList<PartTemplateDefinition> OrganizationTemplates(
        IReadOnlyList<PartTemplateDefinition> templates) =>
        templates.Where(t => !t.IsMasterDataCatalog).ToList();

    public static bool HasSharedColumnLabels(
        PartTemplateDefinition primary,
        PartTemplateDefinition lookup) =>
        WorksheetRelationUiText.GetSharedColumnNames(
            primary.Fields.Select(f => f.Label),
            lookup.Fields.Select(f => f.Label)).Count > 0;
}
