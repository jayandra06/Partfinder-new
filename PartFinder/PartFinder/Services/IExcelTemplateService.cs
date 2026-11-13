using PartFinder.Models;

namespace PartFinder.Services;

public interface IExcelTemplateService
{
    Task ExportTemplateAsync(
        PartTemplateDefinition template,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task ExportTemplateWithDataAsync(
        PartTemplateDefinition template,
        string destinationPath,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows,
        CancellationToken cancellationToken = default);

    Task<ExcelImportParseResult> ParseImportFileAsync(
        PartTemplateDefinition template,
        string sourcePath,
        CancellationToken cancellationToken = default);

    /// <summary>Row 1 = column headers; following rows = values (Excel or CSV).</summary>
    Task<ExcelSchemaImportParseResult> ParseImportFileInferSchemaAsync(
        string sourcePath,
        CancellationToken cancellationToken = default);
}

public sealed class ExcelSchemaImportParseResult
{
    public IReadOnlyList<TemplateFieldDefinition> Fields { get; init; } = Array.Empty<TemplateFieldDefinition>();

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } =
        Array.Empty<IReadOnlyDictionary<string, string>>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int EmptyRowsSkipped { get; init; }
}

public sealed class ExcelImportParseResult
{
    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; } =
        Array.Empty<IReadOnlyDictionary<string, string>>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int TotalSheetRowsRead { get; init; }

    public int EmptyRowsSkipped { get; init; }
}
