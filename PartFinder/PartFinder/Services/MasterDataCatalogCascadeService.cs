using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// When master data catalogs are removed, updates or removes analytics templates that imported columns from them.
/// </summary>
public sealed class MasterDataCatalogCascadeService
{
    private readonly ITemplateSchemaService _templates;
    private readonly IMasterDataRecordsService _records;
    private readonly WorksheetRelationCleanupService _relationCleanup;

    public MasterDataCatalogCascadeService(
        ITemplateSchemaService templates,
        IMasterDataRecordsService records,
        WorksheetRelationCleanupService relationCleanup)
    {
        _templates = templates;
        _records = records;
        _relationCleanup = relationCleanup;
    }

    public async Task<MasterDataCatalogCascadeResult> OnMasterDataCatalogDeletedAsync(
        string deletedCatalogId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deletedCatalogId))
        {
            return new MasterDataCatalogCascadeResult();
        }

        var all = await _templates.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        var catalogsRemaining = TemplateCatalogFilters.MasterDataCatalogs(all).Count;
        var analytics = TemplateCatalogFilters.OrganizationTemplates(all);

        if (catalogsRemaining == 0)
        {
            var deleted = 0;
            foreach (var template in analytics)
            {
                await DeleteAnalyticsTemplateFullyAsync(template.Id, cancellationToken).ConfigureAwait(false);
                deleted++;
            }

            return new MasterDataCatalogCascadeResult
            {
                DeletedAllAnalyticsTemplates = true,
                AnalyticsTemplatesDeleted = deleted,
            };
        }

        var columnsRemoved = 0;
        var templatesUpdated = 0;
        foreach (var template in analytics)
        {
            var (updated, removed) = await RemoveColumnsSourcedFromCatalogAsync(
                template,
                deletedCatalogId,
                cancellationToken).ConfigureAwait(false);
            if (updated)
            {
                templatesUpdated++;
                columnsRemoved += removed;
            }
        }

        return new MasterDataCatalogCascadeResult
        {
            AnalyticsTemplatesUpdated = templatesUpdated,
            ColumnsRemoved = columnsRemoved,
        };
    }

    private async Task<(bool Updated, int ColumnsRemoved)> RemoveColumnsSourcedFromCatalogAsync(
        PartTemplateDefinition template,
        string deletedCatalogId,
        CancellationToken cancellationToken)
    {
        var removeKeys = template.Fields
            .Where(f => MasterDataSourceFieldEncoding.IsSourcedFromCatalog(f, deletedCatalogId))
            .Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);
        if (removeKeys.Count == 0)
        {
            return (false, 0);
        }

        var remaining = template.Fields
            .Where(f => !removeKeys.Contains(f.Key))
            .OrderBy(f => f.DisplayOrder)
            .Select((f, index) => CloneField(f, index))
            .ToList();

        if (remaining.Count == 0)
        {
            remaining.Add(new TemplateFieldDefinition
            {
                Key = "col_" + Guid.NewGuid().ToString("N")[..8],
                Label = "Column 1",
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = 0,
            });
        }

        var patched = new PartTemplateDefinition
        {
            Id = template.Id,
            Name = template.Name,
            Version = template.Version + 1,
            IsPublished = template.IsPublished,
            IsMasterDataCatalog = false,
            BaseTemplateId = template.BaseTemplateId,
            Fields = remaining,
        };

        await _templates.SaveTemplateAsync(patched, cancellationToken).ConfigureAwait(false);
        await StripColumnValuesFromRowsAsync(template.Id, removeKeys, remaining, cancellationToken)
            .ConfigureAwait(false);

        return (true, removeKeys.Count);
    }

    private async Task StripColumnValuesFromRowsAsync(
        string templateId,
        IReadOnlySet<string> removedKeys,
        IReadOnlyList<TemplateFieldDefinition> schema,
        CancellationToken cancellationToken)
    {
        var rows = await _records.GetStoredRowsAsync(templateId, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            rows = await _records.GetRowsAsync(templateId, cancellationToken).ConfigureAwait(false);
        }

        if (rows.Count == 0)
        {
            return;
        }

        var validKeys = schema.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var payload = new List<(string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)>(rows.Count);
        foreach (var row in rows.OrderBy(r => r.RowOrder))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var key in validKeys)
            {
                row.Values.TryGetValue(key, out var value);
                values[key] = value ?? string.Empty;
            }

            payload.Add((row.Id, row.RowOrder, values));
        }

        await _records
            .SaveRowsAsync(templateId, payload, Array.Empty<string>(), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task DeleteAnalyticsTemplateFullyAsync(string templateId, CancellationToken cancellationToken)
    {
        await _relationCleanup
            .DeleteAllRelationsInvolvingTemplateAsync(templateId, cancellationToken)
            .ConfigureAwait(false);
        _ = await _records
            .DeleteAllRowsForTemplateAsync(templateId, cancellationToken)
            .ConfigureAwait(false);
        await _templates.DeleteTemplateAsync(templateId, cancellationToken).ConfigureAwait(false);
    }

    private static TemplateFieldDefinition CloneField(TemplateFieldDefinition f, int displayOrder) =>
        new()
        {
            Key = f.Key,
            Label = f.Label,
            Type = f.Type,
            IsRequired = f.IsRequired,
            DisplayOrder = displayOrder,
            ValidationPattern = f.ValidationPattern,
            Options = f.Options,
            LinkedTemplateId = f.LinkedTemplateId,
            LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
        };
}

public sealed class MasterDataCatalogCascadeResult
{
    public int AnalyticsTemplatesUpdated { get; init; }
    public int ColumnsRemoved { get; init; }
    public bool DeletedAllAnalyticsTemplates { get; init; }
    public int AnalyticsTemplatesDeleted { get; init; }
}
