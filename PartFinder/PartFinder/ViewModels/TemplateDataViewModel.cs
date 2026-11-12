using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class TemplateDataViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _records;
    private readonly Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<IReadOnlyDictionary<string, string>> _rows = [];

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

    public IReadOnlyList<IReadOnlyDictionary<string, string>> Rows => _rows;

    public IReadOnlyList<IReadOnlyDictionary<string, string>> FilteredRows =>
        _columnFilters.Count == 0
            ? _rows
            : _rows.Where(RowMatchesColumnFilters).ToList();

    public event EventHandler? ColumnFiltersChanged;

    public bool TryGetColumnFilter(string fieldKey, out string filterText) =>
        _columnFilters.TryGetValue(fieldKey, out filterText!);

    public void SetColumnFilter(string fieldKey, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return;
        }

        var normalized = filterValue?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            if (!_columnFilters.Remove(fieldKey))
            {
                return;
            }
        }
        else if (_columnFilters.TryGetValue(fieldKey, out var existing)
                 && string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        else
        {
            _columnFilters[fieldKey] = normalized;
        }

        OnPropertyChanged(nameof(FilteredRows));
        ColumnFiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool RowMatchesColumnFilters(IReadOnlyDictionary<string, string> row)
    {
        foreach (var filter in _columnFilters)
        {
            row.TryGetValue(filter.Key, out var cell);
            var text = cell ?? string.Empty;
            if (!text.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public async Task LoadAsync(string templateId, CancellationToken cancellationToken = default)
    {
        TemplateId = templateId;
        _columnFilters.Clear();
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
                _rows = [];
                StatusMessage = "Template not found.";
                OnPropertyChanged(nameof(Columns));
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(FilteredRows));
                return;
            }

            TemplateName = template.Name;
            Columns = template.Fields.ToList();
            OnPropertyChanged(nameof(Columns));

            var rowRecords = await _records.GetStoredRowsAsync(templateId, cancellationToken).ConfigureAwait(true);
            if (rowRecords.Count == 0)
            {
                rowRecords = await _records.GetRowsAsync(templateId, cancellationToken).ConfigureAwait(true);
            }
            _rows = rowRecords
                .OrderBy(r => r.RowOrder)
                .Select(r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r.Values, StringComparer.Ordinal))
                .ToList();
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(FilteredRows));

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
            _rows = [];
            OnPropertyChanged(nameof(Columns));
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(FilteredRows));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
