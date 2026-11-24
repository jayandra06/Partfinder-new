using ClosedXML.Excel;
using PartFinder.Models;

namespace PartFinder.Services;

public sealed class BulkTemplateImportService
{
    private readonly ITemplateSchemaService _templates;
    private readonly IMasterDataRecordsService _records;

    public BulkTemplateImportService(
        ITemplateSchemaService templates,
        IMasterDataRecordsService records)
    {
        _templates = templates;
        _records = records;
    }

    public async Task<BulkTemplateImportResult> ImportFromFileAsync(
        string sourcePath,
        bool canAddTemplate,
        bool canAddMasterData,
        IProgress<BulkImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return new BulkTemplateImportResult
            {
                Errors = ["Could not read the selected file. Try saving it locally and choose it again."],
            };
        }

        List<BulkTemplateDraft> drafts;
        try
        {
            drafts = await Task.Run(
                    () => IsCsvPath(sourcePath)
                        ? ParseCsvFile(sourcePath)
                        : ParseWorkbook(sourcePath),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new BulkTemplateImportResult
            {
                Errors = [$"Could not parse the file: {ex.Message}"],
            };
        }

        if (drafts.Count == 0)
        {
            return new BulkTemplateImportResult
            {
                Errors =
                [
                    "No templates found.",
                    "Excel: one sheet per template (sheet name = template name, row 1 = column labels, row 2+ = data),",
                    "or a sheet named BulkTemplates with columns TemplateName and Columns.",
                    "CSV: same BulkTemplates header row, or row 1 = column labels with data below (file name = template name),",
                    "or row 1 includes TemplateName plus data columns (one row per record, grouped by template).",
                ],
            };
        }

        var existing = await _templates.GetTemplatesAsync(cancellationToken).ConfigureAwait(false);
        var existingByName = existing.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skipped = 0;
        var importedRows = 0;
        var errors = new List<string>();
        var totalRows = drafts.Sum(d => d.DataRows.Count);
        var countingRows = totalRows > 0;
        var totalUnits = countingRows ? totalRows : drafts.Count;
        var completed = 0;

        progress?.Report(new BulkImportProgress(completed, totalUnits, "Starting upload…"));

        foreach (var draft in drafts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(draft.Name))
            {
                errors.Add("Skipped a row with an empty template name.");
                skipped++;
                continue;
            }

            if (draft.ColumnLabels.Count == 0)
            {
                errors.Add($"Skipped \"{draft.Name}\" — no column labels.");
                skipped++;
                continue;
            }

            var name = draft.Name.Trim();
            PartTemplateDefinition? template;
            progress?.Report(new BulkImportProgress(completed, totalUnits, $"Preparing \"{name}\"…"));

            if (existingByName.TryGetValue(name, out var existingTemplate))
            {
                template = existingTemplate;
                skipped++;
            }
            else
            {
                if (!canAddTemplate)
                {
                    errors.Add($"Skipped \"{name}\" — template does not exist and you cannot add templates.");
                    skipped++;
                    continue;
                }

                var fields = draft.ColumnLabels
                    .Select(
                        (label, index) => new TemplateFieldDefinition
                        {
                            Key = MakeFieldKey(label, index),
                            Label = label,
                            Type = TemplateFieldType.Text,
                            DisplayOrder = index,
                            IsRequired = false,
                        })
                    .ToList();

                template = new PartTemplateDefinition
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    Version = 1,
                    IsPublished = true,
                    IsMasterDataCatalog = false,
                    Fields = fields,
                };

                try
                {
                    await _templates.SaveTemplateAsync(template, cancellationToken).ConfigureAwait(false);
                    existingByName[name] = template;
                    created++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to save \"{name}\": {ex.Message}");
                    continue;
                }
            }

            if (!countingRows)
            {
                completed++;
                progress?.Report(new BulkImportProgress(completed, totalUnits, $"{name}"));
            }

            if (draft.DataRows.Count == 0)
            {
                continue;
            }

            if (!canAddMasterData)
            {
                errors.Add(
                    $"\"{name}\": {draft.DataRows.Count} data row(s) were not imported — you do not have permission to add master data.");
                continue;
            }

            var importedThisTemplate = 0;
            try
            {
                var batchProgress = new SyncProgress<int>(inserted =>
                {
                    importedThisTemplate = inserted;
                    progress?.Report(
                        new BulkImportProgress(completed + inserted, totalUnits, $"Uploading \"{name}\""));
                });
                var imported = await ImportDataRowsAsync(template, draft.DataRows, batchProgress, cancellationToken)
                    .ConfigureAwait(false);
                importedRows += imported;
                completed += imported;
            }
            catch (Exception ex)
            {
                importedRows += importedThisTemplate;
                completed += importedThisTemplate;
                errors.Add($"Failed to import rows for \"{name}\": {ex.Message}");
            }
        }

        progress?.Report(new BulkImportProgress(completed, totalUnits, "Finishing upload…"));

        return new BulkTemplateImportResult
        {
            CreatedCount = created,
            SkippedCount = skipped,
            ImportedRowCount = importedRows,
            TotalCount = totalUnits,
            UploadedCount = completed,
            Errors = errors,
        };
    }

    private async Task<int> ImportDataRowsAsync(
        PartTemplateDefinition template,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rowsByLabel,
        IProgress<int>? insertedCount,
        CancellationToken cancellationToken)
    {
        var prepared = new List<Dictionary<string, string>>(rowsByLabel.Count);
        foreach (var source in rowsByLabel)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in template.Fields.OrderBy(f => f.DisplayOrder))
            {
                if (source.TryGetValue(field.Label, out var byLabel) && !string.IsNullOrWhiteSpace(byLabel))
                {
                    values[field.Key] = byLabel.Trim();
                    continue;
                }

                if (source.TryGetValue(field.Key, out var byKey))
                {
                    values[field.Key] = byKey?.Trim() ?? string.Empty;
                    continue;
                }

                values[field.Key] = string.Empty;
            }

            if (values.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            prepared.Add(values);
        }

        if (prepared.Count == 0)
        {
            return 0;
        }

        return await _records
            .AppendRowsAsync(template.Id, prepared, insertedCount, cancellationToken)
            .ConfigureAwait(false);
    }

    private const int MaxConsecutiveBlankRows = 250;

    private static bool IsCsvPath(string path) =>
        path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

    private static List<BulkTemplateDraft> ParseWorkbook(string sourcePath)
    {
        using var workbook = new XLWorkbook(sourcePath);
        var bulkSheet = workbook.Worksheets.FirstOrDefault(w =>
            string.Equals(w.Name, "BulkTemplates", StringComparison.OrdinalIgnoreCase));

        if (bulkSheet is not null)
        {
            return ParseBulkTemplatesDefinitionRows(ParseBulkTemplatesSheet(bulkSheet));
        }

        var list = new List<BulkTemplateDraft>();
        foreach (var sheet in workbook.Worksheets)
        {
            if (string.Equals(sheet.Name, "Instructions", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.AddRange(ParseTemplateSheets(sheet.Name, sheet));
        }

        return list;
    }

    private static List<BulkTemplateDraft> ParseBulkTemplatesDefinitionRows(List<BulkTemplateDraft> definitionRows) =>
        definitionRows.Select(d => new BulkTemplateDraft(d.Name, d.ColumnLabels, [])).ToList();

    private static List<BulkTemplateDraft> ParseBulkTemplatesSheet(IXLWorksheet sheet)
    {
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < 2)
        {
            return [];
        }

        var header = sheet.Row(1).CellsUsed().Select(c => c.GetString().Trim()).ToList();
        var nameIdx = header.FindIndex(
            h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));
        var colsIdx = header.FindIndex(
            h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
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
                labels = sheet.Row(r).CellsUsed().Skip(1).Select(c => c.GetString().Trim()).Where(x => x.Length > 0)
                    .ToList();
            }

            list.Add(new BulkTemplateDraft(name, labels, []));
        }

        return list;
    }

    private static List<BulkTemplateDraft> ParseTemplateSheets(string sheetName, IXLWorksheet sheet)
    {
        var lastCol = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastCol == 0)
        {
            return [];
        }

        var headerCells = new List<string>();
        for (var c = 1; c <= lastCol; c++)
        {
            headerCells.Add(sheet.Cell(1, c).GetString().Trim());
        }

        var templateNameIdx = headerCells.FindIndex(
            h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));

        if (templateNameIdx >= 0 && headerCells.Count > 1
            && !headerCells.Any(
                h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
                     || h.Equals("Column Labels", StringComparison.OrdinalIgnoreCase)))
        {
            return ParseSheetWithTemplateNameColumn(sheet, headerCells, templateNameIdx);
        }

        var labels = headerCells.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (labels.Count == 0)
        {
            return [];
        }

        var name = sheetName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var dataRows = ReadExcelDataRows(sheet, headerCells, startRow: 2);
        return [new BulkTemplateDraft(name, labels, dataRows)];
    }

    private static List<BulkTemplateDraft> ParseSheetWithTemplateNameColumn(
        IXLWorksheet sheet,
        IReadOnlyList<string> headerCells,
        int templateNameIdx)
    {
        var labels = headerCells
            .Where((h, i) => i != templateNameIdx && !string.IsNullOrWhiteSpace(h))
            .ToList();
        if (labels.Count == 0)
        {
            return [];
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var groups = new Dictionary<string, List<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
        var blankStreak = 0;

        for (var r = 2; r <= lastRow; r++)
        {
            var templateName = sheet.Cell(r, templateNameIdx + 1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(templateName))
            {
                blankStreak++;
                if (blankStreak >= MaxConsecutiveBlankRows)
                {
                    break;
                }

                continue;
            }

            blankStreak = 0;

            var rowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < headerCells.Count; c++)
            {
                if (c == templateNameIdx || string.IsNullOrWhiteSpace(headerCells[c]))
                {
                    continue;
                }

                rowMap[headerCells[c]] = sheet.Cell(r, c + 1).GetString().Trim();
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (!groups.TryGetValue(templateName, out var list))
            {
                list = [];
                groups[templateName] = list;
            }

            list.Add(rowMap);
        }

        return groups.Select(g => new BulkTemplateDraft(g.Key, labels, g.Value)).ToList();
    }

    private static List<IReadOnlyDictionary<string, string>> ReadExcelDataRows(
        IXLWorksheet sheet,
        IReadOnlyList<string> headerCells,
        int startRow)
    {
        var columns = new List<(string Label, int Col)>();
        for (var c = 0; c < headerCells.Count; c++)
        {
            if (!string.IsNullOrWhiteSpace(headerCells[c]))
            {
                columns.Add((headerCells[c], c + 1));
            }
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var blankStreak = 0;

        for (var r = startRow; r <= lastRow; r++)
        {
            var rowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (label, col) in columns)
            {
                rowMap[label] = sheet.Cell(r, col).GetString().Trim();
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                blankStreak++;
                if (blankStreak >= MaxConsecutiveBlankRows)
                {
                    break;
                }

                continue;
            }

            blankStreak = 0;
            rows.Add(rowMap);
        }

        return rows;
    }

    private static List<BulkTemplateDraft> ParseCsvFile(string sourcePath)
    {
        var table = CsvTable.Read(sourcePath);
        if (table.Count == 0)
        {
            return [];
        }

        var header = table[0];
        if (IsBulkTemplatesHeader(header))
        {
            return ParseBulkTemplatesCsvRows(table);
        }

        var templateNameIdx = IndexOfHeader(
            header,
            h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));
        var columnsIdx = IndexOfHeader(
            header,
            h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Column Labels", StringComparison.OrdinalIgnoreCase));

        if (templateNameIdx >= 0 && columnsIdx >= 0)
        {
            return ParseBulkTemplatesCsvRows(table);
        }

        if (templateNameIdx >= 0)
        {
            return ParseCsvGroupedByTemplateName(table, header, templateNameIdx);
        }

        var labels = header.Where(h => !string.IsNullOrWhiteSpace(h)).ToList();
        if (labels.Count == 0)
        {
            return [];
        }

        var name = Path.GetFileNameWithoutExtension(sourcePath).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Imported Template";
        }

        var dataRows = ParseCsvDataRows(table, header, labels, startRowIndex: 1);
        return [new BulkTemplateDraft(name, labels, dataRows)];
    }

    private static List<BulkTemplateDraft> ParseBulkTemplatesCsvRows(IReadOnlyList<IReadOnlyList<string>> table)
    {
        var header = table[0];
        var nameIdx = IndexOfHeader(
            header,
            h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));
        var colsIdx = IndexOfHeader(
            header,
            h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Column Labels", StringComparison.OrdinalIgnoreCase));

        var list = new List<BulkTemplateDraft>();
        for (var r = 1; r < table.Count; r++)
        {
            var row = PadRow(table[r], header.Count);
            var name = nameIdx >= 0 ? row[nameIdx].Trim() : string.Empty;
            var colsRaw = colsIdx >= 0 ? row[colsIdx] : string.Empty;
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(colsRaw))
            {
                continue;
            }

            var labels = SplitColumnLabels(colsRaw);
            list.Add(new BulkTemplateDraft(name, labels, []));
        }

        return list;
    }

    private static List<BulkTemplateDraft> ParseCsvGroupedByTemplateName(
        IReadOnlyList<IReadOnlyList<string>> table,
        IReadOnlyList<string> header,
        int templateNameIdx)
    {
        var labels = header
            .Where((h, i) => i != templateNameIdx && !string.IsNullOrWhiteSpace(h))
            .ToList();
        if (labels.Count == 0)
        {
            return [];
        }

        var groups = new Dictionary<string, List<IReadOnlyDictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
        for (var r = 1; r < table.Count; r++)
        {
            var row = PadRow(table[r], header.Count);
            var templateName = row[templateNameIdx].Trim();
            if (string.IsNullOrWhiteSpace(templateName))
            {
                continue;
            }

            var rowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < header.Count; c++)
            {
                if (c == templateNameIdx || string.IsNullOrWhiteSpace(header[c]))
                {
                    continue;
                }

                rowMap[header[c]] = row[c].Trim();
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (!groups.TryGetValue(templateName, out var list))
            {
                list = [];
                groups[templateName] = list;
            }

            list.Add(rowMap);
        }

        return groups.Select(g => new BulkTemplateDraft(g.Key, labels, g.Value)).ToList();
    }

    private static List<IReadOnlyDictionary<string, string>> ParseCsvDataRows(
        IReadOnlyList<IReadOnlyList<string>> table,
        IReadOnlyList<string> header,
        IReadOnlyList<string> labels,
        int startRowIndex)
    {
        var rows = new List<IReadOnlyDictionary<string, string>>();
        for (var r = startRowIndex; r < table.Count; r++)
        {
            var row = PadRow(table[r], header.Count);
            var rowMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < labels.Count; i++)
            {
                var headerIdx = IndexOfHeader(
                    header,
                    h => string.Equals(h, labels[i], StringComparison.OrdinalIgnoreCase));
                rowMap[labels[i]] = headerIdx >= 0 && headerIdx < row.Count ? row[headerIdx].Trim() : string.Empty;
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(rowMap);
        }

        return rows;
    }

    private static bool IsBulkTemplatesHeader(IReadOnlyList<string> header)
    {
        var hasName = header.Any(
            h => h.Equals("TemplateName", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Template Name", StringComparison.OrdinalIgnoreCase));
        var hasCols = header.Any(
            h => h.Equals("Columns", StringComparison.OrdinalIgnoreCase)
                 || h.Equals("Column Labels", StringComparison.OrdinalIgnoreCase));
        return hasName && hasCols;
    }

    private static int IndexOfHeader(IReadOnlyList<string> header, Func<string, bool> predicate)
    {
        for (var i = 0; i < header.Count; i++)
        {
            if (predicate(header[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static IReadOnlyList<string> PadRow(IReadOnlyList<string> row, int width)
    {
        if (row.Count >= width)
        {
            return row;
        }

        var padded = row.ToList();
        while (padded.Count < width)
        {
            padded.Add(string.Empty);
        }

        return padded;
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

    private sealed record BulkTemplateDraft(
        string Name,
        List<string> ColumnLabels,
        List<IReadOnlyDictionary<string, string>> DataRows);

    private sealed class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _report;

        public SyncProgress(Action<T> report)
        {
            _report = report;
        }

        public void Report(T value) => _report(value);
    }

    private static class CsvTable
    {
        public static List<List<string>> Read(string path)
        {
            var rows = new List<List<string>>();
            using var reader = new StreamReader(path);
            while (true)
            {
                var line = reader.ReadLine();
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                rows.Add(ParseLine(line));
            }

            return rows;
        }

        private static List<string> ParseLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }

                    continue;
                }

                if (ch == ',' && !inQuotes)
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            fields.Add(current.ToString().Trim());
            return fields;
        }
    }
}

public sealed record BulkImportProgress(int Completed, int Total, string Detail)
{
    public int Percent => Total <= 0
        ? 0
        : (int)Math.Clamp(
            Math.Round(Completed * 100.0 / Total, MidpointRounding.AwayFromZero),
            0,
            100);
}

public sealed class BulkTemplateImportResult
{
    public int CreatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int ImportedRowCount { get; init; }
    public int TotalCount { get; init; }
    public int UploadedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    public bool HasWork => CreatedCount > 0 || ImportedRowCount > 0;
}
