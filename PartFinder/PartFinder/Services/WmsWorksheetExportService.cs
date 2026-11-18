using ClosedXML.Excel;

namespace PartFinder.Services;

/// <summary>Export authorized WMS worksheet rows to Excel (read-only data export).</summary>
public sealed class WmsWorksheetExportService
{
    public static string ExportToTempFile(string sheetName, string[] headers, IEnumerable<string[]> rows)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"wms-{sheetName}-{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(sheetName.Length > 31 ? sheetName[..31] : sheetName);
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length && c < headers.Length; c++)
                ws.Cell(r, c + 1).Value = row[c];
            r++;
        }
        ws.Columns().AdjustToContents();
        wb.SaveAs(path);
        return path;
    }
}