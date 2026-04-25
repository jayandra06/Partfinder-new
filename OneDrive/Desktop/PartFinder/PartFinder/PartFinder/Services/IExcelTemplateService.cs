using PartFinder.Models;

namespace PartFinder.Services;

public interface IExcelTemplateService
{
    Task ExportTemplateAsync(
        PartTemplateDefinition template,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<ExcelImportParseResult> ParseImportFileAsync(
        PartTemplateDefinition template,
        string sourcePath,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelImportParseResult
{
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } =
        Array.Empty<IReadOnlyDictionary<string, string>>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int TotalSheetRowsRead { get; init; }

    public int EmptyRowsSkipped { get; init; }
}
