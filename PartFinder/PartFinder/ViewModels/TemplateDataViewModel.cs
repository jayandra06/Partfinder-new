using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class TemplateDataViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _records;

    public TemplateDataViewModel(
        ITemplateSchemaService templateSchema,
        IMasterDataRecordsService records)
    {
        _templateSchema = templateSchema;
        _records = records;
    }

    [ObservableProperty]
    private string _templateName = string.Empty;

    [ObservableProperty]
    private string? _templateId;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _statusMessage;

    public IReadOnlyList<TemplateFieldDefinition> Columns { get; private set; } = [];

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; private set; } = [];

    public async Task LoadAsync(string templateId, CancellationToken cancellationToken = default)
    {
        TemplateId = templateId;
        IsLoading = true;
        StatusMessage = null;
        try
        {
            var templates = await _templateSchema.GetTemplatesAsync(cancellationToken).ConfigureAwait(true);
            var template = templates.FirstOrDefault(t => string.Equals(t.Id, templateId, StringComparison.Ordinal));
            if (template is null)
            {
                TemplateName = "Template";
                Columns = [];
                Rows = [];
                StatusMessage = "Template not found.";
                OnPropertyChanged(nameof(Columns));
                OnPropertyChanged(nameof(Rows));
                return;
            }

            TemplateName = template.Name;
            Columns = template.Fields.ToList();
            OnPropertyChanged(nameof(Columns));

            var rowRecords = await _records.GetRowsAsync(templateId, cancellationToken).ConfigureAwait(true);
            Rows = rowRecords
                .OrderBy(r => r.RowOrder)
                .Select(r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r.Values, StringComparer.Ordinal))
                .ToList();
            OnPropertyChanged(nameof(Rows));

            if (Rows.Count == 0)
            {
                StatusMessage = "No rows yet for this template.";
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TemplateDataViewModel] Load failed: {ex.Message}");
            StatusMessage = "Could not load template data.";
            Columns = [];
            Rows = [];
            OnPropertyChanged(nameof(Columns));
            OnPropertyChanged(nameof(Rows));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
