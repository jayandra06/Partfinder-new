using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class PartsViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchemaService;
    private readonly IPartsDataService _partsDataService;
    private readonly IContextActionsService _contextActions;
    private readonly IMasterDataRecordsService _records;
    private readonly ICurrentUserAccessService _access;
    private CancellationTokenSource? _loadCts;
    private int _offset;
    private IReadOnlyList<TemplateContextAction> _loadedContextActions = Array.Empty<TemplateContextAction>();
    private readonly Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);

    public PartsViewModel(
        ITemplateSchemaService templateSchemaService,
        IPartsDataService partsDataService,
        IContextActionsService contextActions,
        IMasterDataRecordsService records,
        ICurrentUserAccessService access)
    {
        _templateSchemaService = templateSchemaService;
        _partsDataService = partsDataService;
        _contextActions = contextActions;
        _records = records;
        _access = access;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<GridColumnDefinition> DynamicColumns { get; } = [];
    public ObservableCollection<PartRecord> Records { get; } = [];
    public ObservableCollection<PartRecord> FilteredRecords { get; } = [];
    public int FilteredRecordsCount => FilteredRecords.Count;
    public int TotalRecordsCount => Records.Count;

    private PartTemplateDefinition? _selectedTemplate;
    public PartTemplateDefinition? SelectedTemplate
    {
        get => _selectedTemplate;
        set
        {
            if (SetProperty(ref _selectedTemplate, value) && value is not null)
            {
                _ = RefreshAsync();
            }
        }
    }

    private bool _hasMoreRows;
    public bool HasMoreRows
    {
        get => _hasMoreRows;
        set => SetProperty(ref _hasMoreRows, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _access.RefreshAsync(cancellationToken).ConfigureAwait(true);
        var templates = await _templateSchemaService.GetTemplatesAsync(cancellationToken);
        var filtered = _access.FilterTemplatesForParts(templates);
        Templates.Clear();
        foreach (var template in filtered)
        {
            Templates.Add(template);
        }

        SelectedTemplate = Templates.FirstOrDefault();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        _offset = 0;
        Records.Clear();
        FilteredRecords.Clear();
        DynamicColumns.Clear();
        _columnFilters.Clear();
        _loadedContextActions = Array.Empty<TemplateContextAction>();

        foreach (var field in SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder))
        {
            DynamicColumns.Add(new GridColumnDefinition { Key = field.Key, Header = field.Label });
        }

        _loadedContextActions = await _contextActions
            .GetForSourceTemplateAsync(SelectedTemplate.Id, _loadCts.Token)
            .ConfigureAwait(true);

        await LoadNextPageAsync(_loadCts.Token);
    }

    [RelayCommand(CanExecute = nameof(CanLoadNextPage))]
    private async Task LoadMoreAsync()
    {
        if (_loadCts is null)
        {
            _loadCts = new CancellationTokenSource();
        }

        await LoadNextPageAsync(_loadCts.Token);
    }

    private bool CanLoadNextPage() => HasMoreRows && !IsLoading;

    private async Task LoadNextPageAsync(CancellationToken cancellationToken)
    {
        if (SelectedTemplate is null || IsLoading)
        {
            return;
        }

        IsLoading = true;
        LoadMoreCommand.NotifyCanExecuteChanged();

        try
        {
            var page = await _partsDataService.GetPageAsync(SelectedTemplate.Id, _offset, 200, cancellationToken);
            foreach (var item in page.Records)
            {
                Records.Add(item);
            }

            _offset += page.Records.Count;
            HasMoreRows = page.HasMore;
            RebuildFilteredRecords();
        }
        finally
        {
            IsLoading = false;
            LoadMoreCommand.NotifyCanExecuteChanged();
        }
    }

    public IReadOnlyList<TemplateContextAction> GetContextActionsForField(string fieldKey) =>
        _loadedContextActions
            .Where(a => string.Equals(a.SourceFieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public void SetColumnFilter(string fieldKey, string? filterValue)
    {
        if (string.IsNullOrWhiteSpace(fieldKey))
        {
            return;
        }

        var normalized = filterValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            _columnFilters.Remove(fieldKey);
        }
        else
        {
            _columnFilters[fieldKey] = normalized;
        }

        RebuildFilteredRecords();
    }

    [RelayCommand]
    private void ClearAllFilters()
    {
        _columnFilters.Clear();
        RebuildFilteredRecords();
    }

    public async Task<bool> UpdateColumnHeadersAsync(
        IReadOnlyDictionary<string, string> headerUpdates,
        CancellationToken cancellationToken = default)
    {
        if (SelectedTemplate is null || headerUpdates.Count == 0)
        {
            return false;
        }

        var updatedFields = SelectedTemplate.Fields
            .OrderBy(f => f.DisplayOrder)
            .Select(
                f => new TemplateFieldDefinition
                {
                    Key = f.Key,
                    Label = headerUpdates.TryGetValue(f.Key, out var updatedLabel) && !string.IsNullOrWhiteSpace(updatedLabel)
                        ? updatedLabel.Trim()
                        : f.Label,
                    Type = f.Type,
                    IsRequired = f.IsRequired,
                    DisplayOrder = f.DisplayOrder,
                    ValidationPattern = f.ValidationPattern,
                    Options = f.Options,
                    LinkedTemplateId = f.LinkedTemplateId,
                    LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
                })
            .ToList();

        var updatedTemplate = new PartTemplateDefinition
        {
            Id = SelectedTemplate.Id,
            Name = SelectedTemplate.Name,
            Version = SelectedTemplate.Version + 1,
            IsPublished = true,
            Fields = updatedFields,
        };

        await _templateSchemaService.SaveTemplateAsync(updatedTemplate, cancellationToken).ConfigureAwait(true);
        SelectedTemplate = updatedTemplate;
        await RefreshAsync().ConfigureAwait(true);
        return true;
    }

    public async Task<bool> AddColumnToSelectedTemplateAsync(
        string headerLabel,
        CancellationToken cancellationToken = default)
    {
        if (SelectedTemplate is null)
        {
            return false;
        }

        var trimmed = headerLabel.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var fields = SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var newOrder = fields.Count;
        fields.Add(
            new TemplateFieldDefinition
            {
                Key = BuildFieldKey(trimmed, newOrder),
                Label = trimmed,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = newOrder,
                ValidationPattern = null,
                Options = null,
                LinkedTemplateId = null,
                LinkedDisplayFieldKey = null,
            });

        var updatedTemplate = new PartTemplateDefinition
        {
            Id = SelectedTemplate.Id,
            Name = SelectedTemplate.Name,
            Version = SelectedTemplate.Version + 1,
            IsPublished = true,
            Fields = fields,
        };

        await _templateSchemaService.SaveTemplateAsync(updatedTemplate, cancellationToken).ConfigureAwait(true);
        SelectedTemplate = updatedTemplate;
        await RefreshAsync().ConfigureAwait(true);
        return true;
    }

    public async Task<(PartTemplateDefinition? targetTemplate, IReadOnlyList<MasterDataRowRecord> rows)> RunContextActionAsync(
        TemplateContextAction action,
        IReadOnlyDictionary<string, string> sourceRowValues,
        CancellationToken cancellationToken = default)
    {
        var targetDef = await _templateSchemaService
            .GetTemplateAsync(action.TargetTemplateId, cancellationToken)
            .ConfigureAwait(true);
        var allRows = await _records
            .GetRowsAsync(action.TargetTemplateId, cancellationToken)
            .ConfigureAwait(true);
        var filtered = allRows
            .Where(r => RowMatchesAction(r.Values, action.MatchRules, sourceRowValues))
            .ToList();
        return (targetDef, filtered);
    }

    public static IReadOnlyDictionary<string, string> BuildSourceRowMap(PartRecord row)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in row.Values)
        {
            d[kv.Key] = kv.Value?.ToString() ?? string.Empty;
        }

        return d;
    }

    private static bool RowMatchesAction(
        IReadOnlyDictionary<string, string> targetValues,
        IReadOnlyList<ContextActionMatchRule> rules,
        IReadOnlyDictionary<string, string> sourceValues)
    {
        if (rules.Count == 0)
        {
            return false;
        }

        foreach (var rule in rules)
        {
            string? expected = null;
            if (!string.IsNullOrEmpty(rule.LiteralValue))
            {
                expected = rule.LiteralValue;
            }
            else if (!string.IsNullOrWhiteSpace(rule.SourceFieldKey)
                     && sourceValues.TryGetValue(rule.SourceFieldKey, out var sv))
            {
                expected = sv;
            }

            if (string.IsNullOrWhiteSpace(expected))
            {
                return false;
            }

            if (!targetValues.TryGetValue(rule.TargetFieldKey, out var tv))
            {
                return false;
            }

            if (!string.Equals(tv.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildFilteredRecords()
    {
        IEnumerable<PartRecord> query = Records;
        foreach (var filter in _columnFilters)
        {
            query = query.Where(row =>
            {
                if (!row.Values.TryGetValue(filter.Key, out var rawValue))
                {
                    return false;
                }

                var textValue = rawValue?.ToString() ?? string.Empty;
                return textValue.Contains(filter.Value, StringComparison.OrdinalIgnoreCase);
            });
        }

        FilteredRecords.Clear();
        foreach (var row in query)
        {
            FilteredRecords.Add(row);
        }

        OnPropertyChanged(nameof(FilteredRecordsCount));
        OnPropertyChanged(nameof(TotalRecordsCount));
    }

    private static string BuildFieldKey(string label, int index)
    {
        var lowered = label.ToLowerInvariant();
        var chars = lowered.Where(static c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var baseKey = new string(chars);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = "column";
        }

        return $"{baseKey}_{index}";
    }
}
