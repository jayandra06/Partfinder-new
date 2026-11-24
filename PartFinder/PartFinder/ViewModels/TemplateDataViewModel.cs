using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class TemplateDataViewModel : ViewModelBase
{
    public const int PageSize = 50;

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

    [ObservableProperty]
    private int _totalRowCount;

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

    public async Task LoadAsync(string templateId, int pageIndex = 0, CancellationToken cancellationToken = default)
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
                _rows = [];
                TotalRowCount = 0;
                StatusMessage = "Template not found.";
                OnPropertyChanged(nameof(Columns));
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(FilteredRows));
                return;
            }

            TemplateName = template.Name;
            Columns = template.Fields.ToList();
            OnPropertyChanged(nameof(Columns));

            await LoadRowsPageAsync(templateId, pageIndex, PageSize, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TemplateDataViewModel] Load failed: {ex.Message}");
            StatusMessage = "Could not load template data.";
            Columns = [];
            _rows = [];
            TotalRowCount = 0;
            OnPropertyChanged(nameof(Columns));
            OnPropertyChanged(nameof(Rows));
            OnPropertyChanged(nameof(FilteredRows));
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadRowsPageAsync(
        string templateId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
        {
            return;
        }

        var skip = Math.Max(0, pageIndex) * pageSize;
        var (rowRecords, total) = await _records
            .GetRowsPageAsync(templateId, skip, pageSize, cancellationToken)
            .ConfigureAwait(true);

        TotalRowCount = total;
        _rows = rowRecords
            .OrderBy(r => r.RowOrder)
            .Select(r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r.Values, StringComparer.Ordinal))
            .ToList();
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(FilteredRows));

        if (TotalRowCount == 0)
        {
            StatusMessage = "No rows yet for this template.";
        }
        else
        {
            StatusMessage = null;
        }
    }

    public int GetPagerTotalRowCount(int visibleRowCount) =>
        TotalRowCount > 0 ? TotalRowCount : visibleRowCount;
}