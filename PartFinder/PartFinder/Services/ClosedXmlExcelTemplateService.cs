using ClosedXML.Excel;
using PartFinder.Models;

namespace PartFinder.Services;

public sealed class ClosedXmlExcelTemplateService : IExcelTemplateService
{
    public Task ExportTemplateAsync(
        PartTemplateDefinition template,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return ExportTemplateWithDataAsync(template, destinationPath, Array.Empty<IReadOnlyDictionary<string, string>>(), cancellationToken);
    }

    public Task ExportTemplateWithDataAsync(
        PartTemplateDefinition template,
        string destinationPath,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        var orderedFields = template.Fields.OrderBy(f => f.DisplayOrder).ToList();

        // Header row
        for (var i = 0; i < orderedFields.Count; i++)
        {
            var field = orderedFields[i];
            var headerCell = sheet.Cell(1, i + 1);
            headerCell.Value = field.Label;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D0E8FC");
        }

        // Data rows
        for (var r = 0; r < rows.Count; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowData = rows[r];
            for (var c = 0; c < orderedFields.Count; c++)
            {
                var field = orderedFields[c];
                var value = rowData.TryGetValue(field.Key, out var v) ? v : string.Empty;
                sheet.Cell(r + 2, c + 1).Value = value ?? string.Empty;
            }
        }

        if (orderedFields.Count > 0)
        {
            sheet.Range(1, 1, 1, orderedFields.Count).SetAutoFilter();
            sheet.SheetView.FreezeRows(1);
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(destinationPath);
        return Task.CompletedTask;
    }

    public Task<ExcelImportParseResult> ParseImportFileAsync(
        PartTemplateDefinition template,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook(sourcePath);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return Task.FromResult(new ExcelImportParseResult
            {
                Warnings = new[] { "No worksheet found in the selected Excel file." },
            });
        }

        var headerRow = sheet.Row(1);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        var orderedFields = template.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var fieldColumnMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var warnings = new List<string>();

        foreach (var field in orderedFields)
        {
            var matchCol = 0;
            for (var c = 1; c <= lastColumn; c++)
            {
                var header = headerRow.Cell(c).GetString().Trim();
                if (string.Equals(header, field.Label, StringComparison.OrdinalIgnoreCase))
                {
                    matchCol = c;
                    break;
                }
            }

            if (matchCol == 0)
            {
                warnings.Add($"Column not found in Excel: {field.Label}");
                continue;
            }

            fieldColumnMap[field.Key] = matchCol;
        }

        var rows = new List<IReadOnlyDictionary<string, string>>();
        var emptyRowsSkipped = 0;
        for (var r = 2; r <= lastRow; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rowMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in orderedFields)
            {
                if (!fieldColumnMap.TryGetValue(field.Key, out var col))
                {
                    rowMap[field.Key] = string.Empty;
                    continue;
                }

                rowMap[field.Key] = sheet.Cell(r, col).GetString().Trim();
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                emptyRowsSkipped++;
                continue;
            }

            rows.Add(rowMap);
        }

        return Task.FromResult(new ExcelImportParseResult
        {
            Rows = rows,
            Warnings = warnings,
            TotalSheetRowsRead = Math.Max(0, lastRow - 1),
            EmptyRowsSkipped = emptyRowsSkipped,
        });
    }

    public Task<ExcelSchemaImportParseResult> ParseImportFileInferSchemaAsync(
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(sourcePath);
        if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(ParseCsvInferSchema(sourcePath, cancellationToken));
        }

        using var workbook = new XLWorkbook(sourcePath);
        var sheet = workbook.Worksheets.FirstOrDefault();
        if (sheet is null)
        {
            return Task.FromResult(new ExcelSchemaImportParseResult
            {
                Warnings = new[] { "No worksheet found in the selected file." },
            });
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        if (lastRow < 1 || lastColumn < 1)
        {
            return Task.FromResult(new ExcelSchemaImportParseResult
            {
                Warnings = new[] { "The file has no header row." },
            });
        }

        var headerCells = new List<(int Col, string Label)>();
        for (var c = 1; c <= lastColumn; c++)
        {
            var label = sheet.Cell(1, c).GetString().Trim();
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            headerCells.Add((c, label));
        }

        if (headerCells.Count == 0)
        {
            return Task.FromResult(new ExcelSchemaImportParseResult
            {
                Warnings = new[] { "Row 1 has no column labels." },
            });
        }

        var fields = BuildInferredFields(headerCells.Select(h => h.Label).ToList());
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var emptyRowsSkipped = 0;
        for (var r = 2; r <= lastRow; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rowMap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headerCells.Count; i++)
            {
                var col = headerCells[i].Col;
                rowMap[fields[i].Key] = sheet.Cell(r, col).GetString().Trim();
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                emptyRowsSkipped++;
                continue;
            }

            rows.Add(rowMap);
        }

        return Task.FromResult(new ExcelSchemaImportParseResult
        {
            Fields = fields,
            Rows = rows,
            EmptyRowsSkipped = emptyRowsSkipped,
        });
    }

    private static ExcelSchemaImportParseResult ParseCsvInferSchema(string sourcePath, CancellationToken cancellationToken)
    {
        var lines = File.ReadAllLines(sourcePath);
        if (lines.Length == 0)
        {
            return new ExcelSchemaImportParseResult { Warnings = new[] { "The CSV file is empty." } };
        }

        var headers = SplitCsvLine(lines[0]);
        if (headers.Count == 0 || headers.All(string.IsNullOrWhiteSpace))
        {
            return new ExcelSchemaImportParseResult { Warnings = new[] { "Row 1 has no column labels." } };
        }

        var fields = BuildInferredFields(headers);
        var rows = new List<IReadOnlyDictionary<string, string>>();
        var emptyRowsSkipped = 0;
        for (var r = 1; r < lines.Length; r++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cells = SplitCsvLine(lines[r]);
            var rowMap = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var c = 0; c < fields.Count; c++)
            {
                rowMap[fields[c].Key] = c < cells.Count ? cells[c].Trim() : string.Empty;
            }

            if (rowMap.Values.All(string.IsNullOrWhiteSpace))
            {
                emptyRowsSkipped++;
                continue;
            }

            rows.Add(rowMap);
        }

        return new ExcelSchemaImportParseResult
        {
            Fields = fields,
            Rows = rows,
            EmptyRowsSkipped = emptyRowsSkipped,
        };
    }

    private static List<string> SplitCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line))
        {
            return result;
        }

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
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        result.Add(current.ToString());
        return result;
    }

    private static List<TemplateFieldDefinition> BuildInferredFields(IReadOnlyList<string> headerLabels)
    {
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = new List<TemplateFieldDefinition>(headerLabels.Count);
        for (var i = 0; i < headerLabels.Count; i++)
        {
            var label = string.IsNullOrWhiteSpace(headerLabels[i]) ? $"Column {i + 1}" : headerLabels[i].Trim();
            var key = SlugFieldKey(label, usedKeys);
            fields.Add(new TemplateFieldDefinition
            {
                Key = key,
                Label = label,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = i,
            });
        }

        return fields;
    }

    private static string SlugFieldKey(string label, HashSet<string> usedKeys)
    {
        var slug = new string(label
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "column";
        }

        var key = slug;
        var n = 2;
        while (!usedKeys.Add(key))
        {
            key = $"{slug}_{n++}";
        }

        return key;
    }
}
