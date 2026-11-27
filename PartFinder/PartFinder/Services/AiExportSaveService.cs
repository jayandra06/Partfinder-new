using System.Text;
using PartFinder.Models;

namespace PartFinder.Services;

public enum AiExportSaveStatus
{
    Saved,
    Cancelled,
    Failed,
}

public sealed record AiExportSaveResult(
    AiExportSaveStatus Status,
    string? FilePath = null,
    int RowCount = 0,
    string? ErrorMessage = null);

public interface IAiExportSaveService
{
    Task<AiExportSaveResult> SaveExportAsync(
        AiStructuredExportPayloadDto payload,
        Func<string, string, CancellationToken, Task<string?>>? pickSavePathAsync = null,
        CancellationToken cancellationToken = default);
}

public sealed class AiExportSaveService : IAiExportSaveService
{
    private readonly IExcelTemplateService _excelTemplateService;

    public AiExportSaveService(IExcelTemplateService excelTemplateService)
    {
        _excelTemplateService = excelTemplateService;
    }

    public async Task<AiExportSaveResult> SaveExportAsync(
        AiStructuredExportPayloadDto payload,
        Func<string, string, CancellationToken, Task<string?>>? pickSavePathAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (payload is null || payload.Columns is null || payload.Columns.Count == 0)
        {
            return new AiExportSaveResult(
                AiExportSaveStatus.Failed,
                ErrorMessage: "Export payload has no columns.");
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isCsvRequested = string.Equals(payload.Format, "csv", StringComparison.OrdinalIgnoreCase);
            var defaultExt = isCsvRequested ? ".csv" : ".xlsx";
            var suggestedName = SanitizeSuggestedFileName(payload.FileNameSuggestion, defaultExt);

            var path = pickSavePathAsync is not null
                ? await pickSavePathAsync(suggestedName, defaultExt, cancellationToken).ConfigureAwait(true)
                : await PickSavePathViaDialogAsync(suggestedName, isCsvRequested).ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(path))
            {
                return new AiExportSaveResult(AiExportSaveStatus.Cancelled);
            }

            cancellationToken.ThrowIfCancellationRequested();

            var rows = payload.Rows ?? [];
            if (path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                var csv = !string.IsNullOrEmpty(payload.CsvContent)
                    ? payload.CsvContent
                    : BuildFormulaSafeCsv(payload.Columns, rows);
                await File.WriteAllTextAsync(path, csv, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var template = BuildTemplateDefinition(payload);
                var rowDicts = rows
                    .Select(r => (IReadOnlyDictionary<string, string>)r)
                    .ToList();
                await _excelTemplateService
                    .ExportTemplateWithDataAsync(template, path, rowDicts, cancellationToken)
                    .ConfigureAwait(false);
            }

            return new AiExportSaveResult(
                AiExportSaveStatus.Saved,
                FilePath: path,
                RowCount: rows.Count);
        }
        catch (OperationCanceledException)
        {
            return new AiExportSaveResult(AiExportSaveStatus.Cancelled);
        }
        catch (Exception ex)
        {
            return new AiExportSaveResult(
                AiExportSaveStatus.Failed,
                ErrorMessage: ex.Message);
        }
    }

    public static PartTemplateDefinition BuildTemplateDefinition(AiStructuredExportPayloadDto payload)
    {
        var fields = payload.Columns
            .Select((col, idx) => new TemplateFieldDefinition
            {
                Key = string.IsNullOrWhiteSpace(col.Key) ? $"col_{idx}" : col.Key,
                Label = string.IsNullOrWhiteSpace(col.Label) ? col.Key : col.Label,
                Type = TemplateFieldType.Text,
                DisplayOrder = idx,
            })
            .ToList();

        return new PartTemplateDefinition
        {
            Id = string.IsNullOrWhiteSpace(payload.ExportType) ? "ai-export" : payload.ExportType,
            Name = string.IsNullOrWhiteSpace(payload.ExportType) ? "AI Export" : payload.ExportType,
            IsMasterDataCatalog = false,
            Fields = fields,
        };
    }

    public static string BuildFormulaSafeCsv(
        IReadOnlyList<AiExportColumnDto> columns,
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var lines = new List<string>(rows.Count + 1)
        {
            string.Join(",", columns.Select(c => SanitizeCsvCell(c.Label))),
        };

        foreach (var row in rows)
        {
            lines.Add(string.Join(",", columns.Select(c =>
            {
                row.TryGetValue(c.Key, out var value);
                return SanitizeCsvCell(value ?? string.Empty);
            })));
        }

        return string.Join("\r\n", lines);
    }

    public static string SanitizeCsvCell(string? raw)
    {
        var text = raw ?? string.Empty;
        if (text.Length > 0)
        {
            var trimmedStart = text.TrimStart();
            if (trimmedStart.Length > 0 && trimmedStart[0] is '=' or '+' or '-' or '@' || text[0] is '\t' or '\r')
            {
                text = "'" + text;
            }
        }

        if (text.IndexOfAny([',', '"', '\r', '\n']) >= 0)
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    private static string SanitizeSuggestedFileName(string? suggested, string defaultExt)
    {
        var raw = Path.GetFileNameWithoutExtension(suggested ?? string.Empty).Trim();
        var safe = string.IsNullOrWhiteSpace(raw) ? "partfinder-export" : raw;
        return $"{safe}{defaultExt}";
    }

    private static async Task<string?> PickSavePathViaDialogAsync(string suggestedFileName, bool isCsvDefault)
    {
        if (App.MainAppWindow is not MainWindow main)
        {
            return null;
        }

        var picker = new Windows.Storage.Pickers.FileSavePicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName),
        };

        if (isCsvDefault)
        {
            picker.FileTypeChoices.Add("CSV (Comma-separated)", [".csv"]);
            picker.FileTypeChoices.Add("Excel Workbook", [".xlsx"]);
        }
        else
        {
            picker.FileTypeChoices.Add("Excel Workbook", [".xlsx"]);
            picker.FileTypeChoices.Add("CSV (Comma-separated)", [".csv"]);
        }

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(main);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }
}
