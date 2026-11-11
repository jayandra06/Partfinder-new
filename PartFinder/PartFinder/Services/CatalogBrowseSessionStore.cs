using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// In-memory catalog browse state for Master Data and Linked Templates until logout.
/// </summary>
public sealed class CatalogBrowseSessionStore
{
    private readonly Dictionary<string, MasterDataSheetCacheEntry> _sheetCache = new(StringComparer.Ordinal);

    public CatalogGridBrowseState MasterDataGrid { get; } = new();
    public CatalogGridBrowseState LinkedTemplatesGrid { get; } = new();

    public string? LinkedTemplatesBrowseTemplateId { get; set; }

    public void Reset()
    {
        MasterDataGrid.Reset();
        LinkedTemplatesGrid.Reset();
        LinkedTemplatesBrowseTemplateId = null;
        _sheetCache.Clear();
    }

    private static string SheetCacheKey(MasterDataSurfaceMode mode, string templateId) =>
        $"{(int)mode}:{templateId}";

    public bool TryGetSheetCache(
        MasterDataSurfaceMode mode,
        string templateId,
        out MasterDataSheetCacheEntry entry) =>
        _sheetCache.TryGetValue(SheetCacheKey(mode, templateId), out entry!);

    public void SetSheetCache(MasterDataSurfaceMode mode, string templateId, MasterDataSheetCacheEntry entry) =>
        _sheetCache[SheetCacheKey(mode, templateId)] = entry;

    public void InvalidateSheetCache(MasterDataSurfaceMode mode, string templateId)
    {
        _sheetCache.Remove(SheetCacheKey(mode, templateId));
    }

    public CatalogGridBrowseState GetGridStateFor(MasterDataSurfaceMode mode) =>
        mode == MasterDataSurfaceMode.LinkedBrowse ? LinkedTemplatesGrid : MasterDataGrid;

    public static IReadOnlyList<PartTemplateDefinition> OrderForCatalogSidebar(
        IReadOnlyList<PartTemplateDefinition> templates)
    {
        if (templates.Count == 0)
        {
            return templates;
        }

        var parts = templates
            .Where(t => MongoSharedPartsCatalog.IsCatalogTemplateName(t.Name))
            .ToList();
        var rest = templates
            .Where(t => !MongoSharedPartsCatalog.IsCatalogTemplateName(t.Name))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ordered = new List<PartTemplateDefinition>(templates.Count);
        ordered.AddRange(parts);
        ordered.AddRange(rest);
        return ordered;
    }

    public static PartTemplateDefinition? ResolveInitialTemplate(
        IReadOnlyList<PartTemplateDefinition> ordered,
        string? savedTemplateId)
    {
        if (ordered.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(savedTemplateId))
        {
            var saved = ordered.FirstOrDefault(
                t => string.Equals(t.Id, savedTemplateId, StringComparison.Ordinal));
            if (saved is not null)
            {
                return saved;
            }
        }

        var defaultParts = ordered.FirstOrDefault(
            t => MongoSharedPartsCatalog.IsCatalogTemplateName(t.Name));
        return defaultParts ?? ordered[0];
    }
}

public sealed class MasterDataSheetCacheEntry
{
    public required string TemplateId { get; init; }
    public required MasterDataSurfaceMode SurfaceMode { get; init; }
    public required IReadOnlyList<TemplateFieldDefinition> Fields { get; init; }
    public required IReadOnlyList<MasterDataRowCacheRow> Rows { get; init; }
    public IReadOnlyList<TemplateContextAction> ContextActions { get; init; } = [];
    public IReadOnlyList<WorksheetRelationDto> WorksheetRelations { get; init; } = [];
}

public sealed class MasterDataRowCacheRow
{
    public string? RowId { get; init; }
    public required IReadOnlyList<MasterDataCellCacheCell> Cells { get; init; }
}

public sealed class MasterDataCellCacheCell
{
    public required string FieldKey { get; init; }
    public required TemplateFieldType FieldType { get; init; }
    public string? LinkedTemplateId { get; init; }
    public string? LinkedDisplayFieldKey { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? LinkedRowId { get; init; }
}

public sealed class CatalogGridBrowseState
{
    public string? TemplateId { get; set; }
    public int PageIndex { get; set; }
    public double ScrollHorizontalOffset { get; set; }
    public double ScrollVerticalOffset { get; set; }

    public void Reset()
    {
        TemplateId = null;
        PageIndex = 0;
        ScrollHorizontalOffset = 0;
        ScrollVerticalOffset = 0;
    }
}
