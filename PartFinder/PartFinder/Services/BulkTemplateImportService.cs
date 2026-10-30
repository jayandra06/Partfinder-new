using ClosedXML.Excel;
using PartFinder.Models;

namespace PartFinder.Services;

public sealed class BulkTemplateImportService
{
    private readonly ITemplateSchemaService _templates;

    public BulkTemplateImportService(ITemplateSchemaService templates)
    {
        _templates = templates;
    }

    public async Task<BulkTemplateImportResult> ImportFromExcelAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existing = await _templates.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        var existingNames = new HashSet<string>(
            existing.Select(t => t.Name),
            StringComparer.OrdinalIgnoreCase);

        var drafts = ParseWorkbook(sourcePath);
        if (drafts.Count == 0)
        {
            return new BulkTemplateImportResult
            {
                Errors = ["No templates found. Use one sheet per template (sheet name = template name, row 1 = column labels) or a sheet named BulkTemplates with columns TemplateName and Columns."],
            };
        }

        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var draft in drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(draft.Name))
            {
                errors.Add("Skipped a row with an empty template name.");
                skipped++;
                continue;
            }

            if (existingNames.Contains(draft.Name))
            {
                errors.Add($"Skipped \"{draft.Name}\" — a template with that name already exists.");
                skipped++;
                continue;
            }

            if (draft.ColumnLabels.Count == 0)
            {
                errors.Add($"Skipped \"{draft.Name}\" — no column labels.");
                skipped++;
                continue;
            }

            var fields = draft.ColumnLabels
                .Select((label, index) => new TemplateFieldDefinition
                {
                    Key = MakeFieldKey(label, index),
                    Label = label,
                    Type = TemplateFieldType.Text,
                    DisplayOrder = index,
                    IsRequired = false,
                })
                .ToList();

            var def = new PartTemplateDefinition
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = draft.Name.Trim(),
                Version = 1,
                IsPublished = true,
                Fields = fields,
            };

            try
            {
                await _templates.SaveTemplateAsync(def, cancellationToken).ConfigureAwait(false);
                existingNames.Add(def.Name);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to save \"{draft.Name}\": {ex.Message}");
            }
        }

        return new BulkTemplateImportResult
        {
            CreatedCount = created,
            SkippedCount = skipped,
            Errors = errors,
        };
    }

    private static List<BulkTemplateDraft> ParseWorkbook(string sourcePath)
    {
        using var workbook = new XLWorkbook(sourcePath);
        var bulkSheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "BulkTemplates", StringComparison.OrdinalIgnoreCase));

        if (bulkSheet is not null)
        {
            return ParseBulkTemplatesSheet(bulkSheet);
        }

        var list = new List<BulkTemplateDraft>();
        foreach (var sheet in workbook.Worksheets)
        {
            if (string.Equals(sheet.Name, "Instructions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fromSheet = ParseTemplateSheet(sheet.Name, sheet);
            if (fromSheet is not null)
            {
                list.Add(fromSheet);
            }
        }

        return list;
    }

    private static List<BulkTemplateDraft> ParseBulkTemplatesSheet(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
        {
            return [];
        }

        var header = sheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();
        var nameIdx = header.FindIndex(h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                                            || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));
        var colsIdx = header.FindIndex(h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
                                             || h.Equals("Column Labels", StringComparison.OrdinalIgnoreCase));

        var list = new List<BulkTemplateDraft>();
        for (var r = 2; r <= lastRow; r++)
        {
            var name = nameIdx >= 0 ? sheet.Cell(r, nameIdx + 1).GetString().Trim() : string.Empty;
            var colsRaw = colsIdx >= 0 ? sheet.Cell(r, colsIdx + 1).GetString() : string.Empty;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(colsRaw))
            {
                continue;
            }

            var labels = SplitColumnLabels(colsRaw);
            if (labels.Count == 0 && colsIdx < 0)
            {
                labels = sheet.Row(r).CellsUsed().Skip(1).Select(c => c.GetString().Trim()).Where(x => x.Length > 0).ToList();
            }

            list.Add(new BulkTemplateDraft(name, labels));
        }

        return list;
    }

    private static BulkTemplateDraft? ParseTemplateSheet(string sheetName, IXLWorksheet sheet)
    {
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastCol == 0)
        {
            return null;
        }

        var labels = new List<string>();
        for (var c = 1; c <= lastCol; c++)
        {
            var label = sheet.Cell(1, c).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(label))
            {
                labels.Add(label);
            }
        }

        if (labels.Count == 0)
        {
            return null;
        }

        var name = sheetName.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : new BulkTemplateDraft(name, labels);
    }

    private static List<string> SplitColumnLabels(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split([';', '|', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string MakeFieldKey(string label, int index)
    {
        var lowered = label.ToLowerInvariant();
        var chars = lowered.Where(static c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var baseKey = new string(chars);
        if (string.IsNullOrEmpty(baseKey))
        {
            baseKey = "column";
        }

        return $"{baseKey}_{index}";
    }

    private sealed record BulkTemplateDraft(string Name, List<string> ColumnLabels);
}

public sealed class BulkTemplateImportResult
{
    public int CreatedCount { get; init; }
    public int SkippedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool Success => CreatedCount > 0 && Errors.Count == 0;
}
