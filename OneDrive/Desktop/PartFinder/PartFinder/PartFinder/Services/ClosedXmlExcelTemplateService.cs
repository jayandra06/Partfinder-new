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
        cancellationToken.ThrowIfCancellationRequested();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Data");
        var orderedFields = template.Fields.OrderBy(f => f.DisplayOrder).ToList();

        for (var i = 0; i < orderedFields.Count; i++)
        {
            var field = orderedFields[i];
            var headerCell = sheet.Cell(1, i + 1);
            headerCell.Value = field.Label;
            headerCell.Style.Font.Bold = true;
            headerCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D0E8FC");

            var hintCell = sheet.Cell(2, i + 1);
            hintCell.Value = $"Type: {field.Type}";
            hintCell.Style.Font.FontColor = XLColor.Gray;
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
}
