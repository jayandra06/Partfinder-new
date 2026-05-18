using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using PartFinder.Core;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class MasterDataViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _records;
    private readonly IContextActionsService _contextActions;
    private readonly ActivityLogger _activityLogger;

    private bool _suppressTemplateSelectionChange;
    private IReadOnlyList<PartTemplateDefinition> _cachedTemplates = Array.Empty<PartTemplateDefinition>();
    private IReadOnlyList<TemplateContextAction> _loadedContextActions = Array.Empty<TemplateContextAction>();

    private readonly ICurrentUserAccessService _access;
    private readonly BackendApiClient _api;
    private readonly ExplorerNavigationCoordinator _explorerNav;
    private readonly INavigationService _navigation;
    private readonly ShellViewModel _shell;
    private Dictionary<string, EnrichedRowDto> _enrichedByRowId = new(StringComparer.Ordinal);
    private HashSet<string> _matchKeyColumnLabels = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<WorksheetRelationDto> _worksheetRelations = [];

    public event Action? GridFilterChanged;

    public MasterDataViewModel(
        ITemplateSchemaService templateSchema,
        IMasterDataRecordsService records,
        IContextActionsService contextActions,
        ActivityLogger activityLogger,
        ICurrentUserAccessService access,
        BackendApiClient api,
        ExplorerNavigationCoordinator explorerNav,
        INavigationService navigation,
        ShellViewModel shell)
    {
        _templateSchema = templateSchema;
        _records = records;
        _contextActions = contextActions;
        _activityLogger = activityLogger;
        _access = access;
        _api = api;
        _explorerNav = explorerNav;
        _navigation = navigation;
        _shell = shell;
    }

    public ObservableCollection<PartTemplateDefinition> DataTemplates { get; } = new();

    public ObservableCollection<PartTemplateDefinition> FilteredDataTemplates { get; } = new();

    public ObservableCollection<TemplateFieldDefinition> Columns { get; } = new();

    public ObservableCollection<MasterDataRowViewModel> Rows { get; } = new();

    public ObservableCollection<RelationDetailSection> LinkedRelationSections { get; } = new();

    public ObservableCollection<DisplayPair> LinkedPrimaryDetails { get; } = new();

    [ObservableProperty]
    private bool showLinkedInfoPanel;

    [ObservableProperty]
    private string linkedPanelTitle = "Linked information";

    [ObservableProperty]
    private MasterDataRowViewModel? selectedGridRow;

    [ObservableProperty]
    private MasterDataCellViewModel? selectedGridCell;

    [ObservableProperty]
    private string selectedCellSummary = string.Empty;

    [ObservableProperty]
    private string editModeBannerText = string.Empty;

    [ObservableProperty]
    private PartTemplateDefinition? selectedDataTemplate;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string? statusMessage;

    [ObservableProperty]
    private bool showNoTemplateHint;

    [ObservableProperty]
    private bool showNoDatabaseHint;

    [ObservableProperty]
    private bool showTemplatePicker;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private string templatePickerText = string.Empty;

    [ObservableProperty]
    private string gridSearchText = string.Empty;

    [ObservableProperty]
    private ExplorerRowMatchFilter rowMatchFilter = ExplorerRowMatchFilter.All;

    [ObservableProperty]
    private string relationHealthSummary = string.Empty;

    [ObservableProperty]
    private bool isDrawerPinned;

    [ObservableProperty]
    private string toastMessage = string.Empty;

    public ObservableCollection<ExplorerColumnVisibilityItem> ColumnVisibility { get; } = new();

    public IReadOnlyList<ExplorerRowMatchFilter> RowMatchFilterOptions { get; } =
        Enum.GetValues<ExplorerRowMatchFilter>();

    private bool _suppressTemplatePickerTextSync;

    public string EditModeButtonText => IsEditMode ? "Stop Editing" : "Edit Data";

    public string FilteredRowCountText
    {
        get
        {
            var visible = GetVisibleRows().Count;
            var total = Rows.Count;
            return visible == total ? $"{total} rows" : $"{visible} of {total} rows";
        }
    }

    public bool CanEditMasterData => _access.Capabilities.CanEditMasterData;
    public bool CanAddMasterData => _access.Capabilities.CanAddMasterData;
    public bool CanCopyMasterData => _access.Capabilities.CanCopyMasterData;
    public bool CanDeleteMasterData => _access.Capabilities.CanDeleteMasterData;

    public IReadOnlyList<MasterDataRowViewModel> GetVisibleRows()
    {
        var hasRelations = _worksheetRelations.Count > 0;
        var search = GridSearchText.Trim();
        return Rows
            .Where(row => RowMatchesSearch(row, search))
            .Where(row =>
            {
                var matched = GetRowMatchBadge(row.RowId) == ExplorerCellMatchBadge.Matched;
                return ExplorerGridFilter.RowMatchesLinkFilter(RowMatchFilter, hasRelations, matched);
            })
            .ToList();
    }

    public IReadOnlyList<TemplateFieldDefinition> GetVisibleColumns()
    {
        var hidden = ColumnVisibility
            .Where(c => !c.IsVisible)
            .Select(c => c.Key)
            .ToHashSet(StringComparer.Ordinal);
        return Columns.Where(c => !hidden.Contains(c.Key)).ToList();
    }

    partial void OnGridSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(FilteredRowCountText));
        GridFilterChanged?.Invoke();
    }

    partial void OnRowMatchFilterChanged(ExplorerRowMatchFilter value)
    {
        OnPropertyChanged(nameof(FilteredRowCountText));
        GridFilterChanged?.Invoke();
    }

    partial void OnTemplatePickerTextChanged(string value) => RefreshFilteredDataTemplates();

    public void ShowToast(string message)
    {
        ToastMessage = message;
        OnPropertyChanged(nameof(ToastMessage));
    }

    [RelayCommand]
    private void ClearGridFilters()
    {
        GridSearchText = string.Empty;
        RowMatchFilter = ExplorerRowMatchFilter.All;
    }

    [RelayCommand]
    private void ToggleDrawerPin() => IsDrawerPinned = !IsDrawerPinned;

    [RelayCommand]
    private void OpenLookupTemplate(RelationDetailSection? section)
    {
        if (section is null || string.IsNullOrWhiteSpace(section.LookupTemplateId))
        {
            return;
        }

        _navigation.Navigate(AppPage.MasterData);
        _shell.NavigateToPage(AppPage.MasterData);
        _explorerNav.RequestOpenTemplate(section.LookupTemplateId);
        ShowToast($"Opening {section.LookupTemplateName}…");
    }

    public void OpenTemplateById(string templateId)
    {
        var template = DataTemplates.FirstOrDefault(
            t => string.Equals(t.Id, templateId, StringComparison.Ordinal));
        if (template is not null)
        {
            SelectTemplateFromPicker(template);
        }
    }

    public bool TryMoveSelection(int rowDelta, int colDelta)
    {
        var rows = GetVisibleRows();
        var cols = GetVisibleColumns();
        if (rows.Count == 0 || cols.Count == 0)
        {
            return false;
        }

        var rowIndex = 0;
        if (SelectedGridRow is not null)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i], SelectedGridRow))
                {
                    rowIndex = i;
                    break;
                }
            }
        }

        var colIndex = 0;
        if (SelectedGridCell is not null)
        {
            for (var i = 0; i < cols.Count; i++)
            {
                if (string.Equals(cols[i].Key, SelectedGridCell.FieldKey, StringComparison.Ordinal))
                {
                    colIndex = i;
                    break;
                }
            }
        }

        rowIndex = Math.Clamp(rowIndex + rowDelta, 0, rows.Count - 1);
        colIndex = Math.Clamp(colIndex + colDelta, 0, cols.Count - 1);

        var row = rows[rowIndex];
        var field = cols[colIndex];
        var cell = row.Cells.FirstOrDefault(c => c.FieldKey == field.Key);
        SelectGridCell(row, cell);
        return true;
    }

    private static bool RowMatchesSearch(MasterDataRowViewModel row, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var cells = row.Cells.ToDictionary(c => c.FieldKey, c => c.Text, StringComparer.OrdinalIgnoreCase);
        return ExplorerGridFilter.RowMatchesSearch(cells, search);
    }

    private void RebuildColumnVisibility()
    {
        ColumnVisibility.Clear();
        foreach (var col in Columns)
        {
            var item = new ExplorerColumnVisibilityItem { Key = col.Key, Label = col.Label };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExplorerColumnVisibilityItem.IsVisible))
                {
                    GridFilterChanged?.Invoke();
                }
            };
            ColumnVisibility.Add(item);
        }
    }

    private void RefreshRelationHealth()
    {
        var total = Rows.Count;
        var matched = Rows.Count(r => GetRowMatchBadge(r.RowId) == ExplorerCellMatchBadge.Matched);
        var health = ExplorerGridFilter.ComputeHealth(total, matched, _worksheetRelations.Count);
        RelationHealthSummary = health.SummaryText;

        if (SelectedDataTemplate is not null)
        {
            _explorerNav.PublishHealth(
                new RelationHealthSnapshot(
                    SelectedDataTemplate.Id,
                    SelectedDataTemplate.Name,
                    total,
                    matched,
                    _worksheetRelations.Count));
        }
    }

    public void RefreshFilteredDataTemplates()
    {
        var query = TemplatePickerText.Trim();
        FilteredDataTemplates.Clear();
        IEnumerable<PartTemplateDefinition> matches = string.IsNullOrEmpty(query)
            ? DataTemplates
            : DataTemplates.Where(
                t => t.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var template in matches)
        {
            FilteredDataTemplates.Add(template);
        }
    }

    public void SyncTemplatePickerTextFromSelection()
    {
        _suppressTemplatePickerTextSync = true;
        TemplatePickerText = SelectedDataTemplate?.Name ?? string.Empty;
        _suppressTemplatePickerTextSync = false;
        RefreshFilteredDataTemplates();
    }

    public void SelectTemplateFromPicker(PartTemplateDefinition template)
    {
        if (string.Equals(SelectedDataTemplate?.Id, template.Id, StringComparison.Ordinal))
        {
            SyncTemplatePickerTextFromSelection();
            return;
        }

        _suppressTemplateSelectionChange = true;
        _suppressTemplatePickerTextSync = true;
        SelectedDataTemplate = template;
        TemplatePickerText = template.Name;
        _suppressTemplatePickerTextSync = false;
        _suppressTemplateSelectionChange = false;
        _ = LoadForTemplateAsync(template.Id);
    }

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EditModeButtonText));
        EditModeBannerText = value
            ? "You are editing — save your changes or cancel to discard."
            : string.Empty;
    }

    partial void OnSelectedDataTemplateChanged(PartTemplateDefinition? value)
    {
        if (!_suppressTemplatePickerTextSync)
        {
            _suppressTemplatePickerTextSync = true;
            TemplatePickerText = value?.Name ?? string.Empty;
            _suppressTemplatePickerTextSync = false;
            RefreshFilteredDataTemplates();
        }

        if (_suppressTemplateSelectionChange || value is null)
        {
            return;
        }

        _ = LoadForTemplateAsync(value.Id);
    }

    /// <summary>Initial load: templates list + default grid (Master Data if present).</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = null;
        if (!_access.Capabilities.CanViewMasterData)
        {
            StatusMessage = "You do not have permission to view master data.";
            IsLoading = false;
            DataTemplates.Clear();
            return;
        }
        ShowNoTemplateHint = false;
        ShowNoDatabaseHint = false;
        ShowTemplatePicker = false;
        Columns.Clear();
        Rows.Clear();
        DataTemplates.Clear();
        _loadedContextActions = Array.Empty<TemplateContextAction>();

        try
        {
            _cachedTemplates = await _templateSchema
                .GetTemplatesAsync(cancellationToken)
                .ConfigureAwait(true);

            if (_cachedTemplates.Count == 0)
            {
                ShowNoTemplateHint = true;
                return;
            }

            foreach (var t in _cachedTemplates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                DataTemplates.Add(t);
            }

            ShowTemplatePicker = false;
            RefreshFilteredDataTemplates();

            var master = _cachedTemplates.FirstOrDefault(
                t => MongoTemplateSchemaService.IsExplorerTemplateName(t.Name));
            var pick = master ?? DataTemplates[0];

            _suppressTemplateSelectionChange = true;
            _suppressTemplatePickerTextSync = true;
            SelectedDataTemplate = pick;
            TemplatePickerText = pick.Name;
            _suppressTemplatePickerTextSync = false;
            _suppressTemplateSelectionChange = false;
            RefreshFilteredDataTemplates();

            await LoadForTemplateAsync(pick.Id, cancellationToken).ConfigureAwait(true);
        }
        catch (InvalidOperationException)
        {
            ShowNoDatabaseHint = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadForTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = null;
        ShowNoTemplateHint = false;
        Columns.Clear();
        Rows.Clear();
        _loadedContextActions = Array.Empty<TemplateContextAction>();
        _enrichedByRowId.Clear();
        LinkedRelationSections.Clear();
        ShowLinkedInfoPanel = false;
        SelectedGridRow = null;

        try
        {
            try
            {
                _cachedTemplates = await _templateSchema
                    .GetTemplatesAsync(cancellationToken)
                    .ConfigureAwait(true);
            }
            catch
            {
                // Keep prior cache for link resolution if refresh fails.
            }

            var template = await _templateSchema
                .GetTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(true);

            if (template is null || template.Fields.Count == 0)
            {
                ShowNoTemplateHint = true;
                return;
            }

            foreach (var f in template.Fields.OrderBy(x => x.DisplayOrder))
            {
                Columns.Add(f);
            }

            RebuildColumnVisibility();
            GridSearchText = string.Empty;
            RowMatchFilter = ExplorerRowMatchFilter.All;

            _loadedContextActions = await _contextActions
                .GetForSourceTemplateAsync(template.Id, cancellationToken)
                .ConfigureAwait(true);

            var persisted = await _records
                .GetRowsAsync(template.Id, cancellationToken)
                .ConfigureAwait(true);

            if (persisted.Count == 0)
            {
                Rows.Add(CreateEmptyRow());
            }
            else
            {
                foreach (var rec in persisted)
                {
                    Rows.Add(MapRecordToRow(rec));
                }
            }

            await ResolveRecordLinkLabelsAsync(cancellationToken).ConfigureAwait(true);
            await LoadLinkedDataAsync(template.Id, cancellationToken).ConfigureAwait(true);
            SelectGridRow(Rows.FirstOrDefault());
        }
        catch (InvalidOperationException)
        {
            ShowNoDatabaseHint = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadLinkedDataAsync(string templateId, CancellationToken cancellationToken)
    {
        _enrichedByRowId = new Dictionary<string, EnrichedRowDto>(StringComparer.Ordinal);
        LinkedRelationSections.Clear();
        LinkedPrimaryDetails.Clear();
        ShowLinkedInfoPanel = false;
        _matchKeyColumnLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var (relationsOk, _, relations) = await _api.GetRelationsAsync(cancellationToken).ConfigureAwait(true);
        if (relationsOk)
        {
            _worksheetRelations = relations
                .Where(r => string.Equals(r.PrimaryTemplateId, templateId, StringComparison.Ordinal))
                .ToList();
            foreach (var relation in _worksheetRelations)
            {
                foreach (var key in relation.MatchKeys)
                {
                    if (!string.IsNullOrWhiteSpace(key.SourceColumn))
                    {
                        _matchKeyColumnLabels.Add(key.SourceColumn);
                    }
                }
            }
        }

        var (ok, _, rows) = await _api.GetViewDataAsync(templateId, cancellationToken).ConfigureAwait(true);
        if (!ok || rows.Count == 0)
        {
            return;
        }

        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.RowId))
            {
                _enrichedByRowId[row.RowId] = row;
            }
        }

        ShowLinkedInfoPanel = rows.Any(r => r.LinkedData.Count > 0) || _worksheetRelations.Count > 0;
        RefreshRelationHealth();
        OnPropertyChanged(nameof(FilteredRowCountText));
    }

    public bool IsMatchKeyColumn(string fieldKey)
    {
        var label = GetColumnLabel(fieldKey);
        return _matchKeyColumnLabels.Contains(label);
    }

    public ExplorerCellMatchBadge GetRowMatchBadge(string? rowId)
    {
        if (string.IsNullOrWhiteSpace(rowId) || _worksheetRelations.Count == 0)
        {
            return ExplorerCellMatchBadge.None;
        }

        if (!_enrichedByRowId.TryGetValue(rowId, out var enriched))
        {
            return ExplorerCellMatchBadge.Unmatched;
        }

        return enriched.LinkedData.Values.Any(v => v.Matched)
            ? ExplorerCellMatchBadge.Matched
            : ExplorerCellMatchBadge.Unmatched;
    }

    public bool ShouldShowMatchBadge(MasterDataRowViewModel row, MasterDataCellViewModel cell) =>
        IsMatchKeyColumn(cell.FieldKey) && GetRowMatchBadge(row.RowId) != ExplorerCellMatchBadge.None;

    public void SelectGridCell(MasterDataRowViewModel? row, MasterDataCellViewModel? cell = null)
    {
        SelectedGridCell = cell;
        if (cell is not null)
        {
            var label = GetColumnLabel(cell.FieldKey);
            SelectedCellSummary = string.IsNullOrWhiteSpace(cell.Text)
                ? label
                : $"{label}: {cell.Text}";
        }
        else
        {
            SelectedCellSummary = string.Empty;
        }

        SelectGridRow(row);
        ShowLinkedInfoPanel = true;
    }

    [RelayCommand]
    private void CloseDetailsPanel()
    {
        if (IsDrawerPinned)
        {
            return;
        }

        ShowLinkedInfoPanel = false;
        SelectedGridCell = null;
        SelectedCellSummary = string.Empty;
    }

    private string GetColumnLabel(string fieldKey) =>
        Columns.FirstOrDefault(c => string.Equals(c.Key, fieldKey, StringComparison.Ordinal))?.Label
        ?? fieldKey;

    public void SelectGridRow(MasterDataRowViewModel? row)
    {
        SelectedGridRow = row;
        LinkedRelationSections.Clear();
        LinkedPrimaryDetails.Clear();

        if (row is null || string.IsNullOrWhiteSpace(row.RowId))
        {
            LinkedPanelTitle = "Linked information";
            return;
        }

        if (!_enrichedByRowId.TryGetValue(row.RowId, out var enriched))
        {
            LinkedPanelTitle = "Linked information";
            LinkedPrimaryDetails.Add(new DisplayPair("Status", "Select a row to see linked template data."));
            return;
        }

        foreach (var kv in enriched.Cells.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            LinkedPrimaryDetails.Add(new DisplayPair(kv.Key, kv.Value));
        }

        var orderedRelations = enriched.LinkedData
            .OrderByDescending(kv => kv.Value.Matched)
            .ThenBy(kv => kv.Value.MenuLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstLinked = orderedRelations.Select(kv => kv.Value).FirstOrDefault(v => v.Matched);
        LinkedPanelTitle = firstLinked is not null && !string.IsNullOrWhiteSpace(firstLinked.MenuLabel)
            ? firstLinked.MenuLabel
            : "Linked information";

        foreach (var relation in orderedRelations)
        {
            var meta = _worksheetRelations.FirstOrDefault(
                r => string.Equals(r.Id, relation.Key, StringComparison.Ordinal));
            var lookupName = meta is null
                ? string.Empty
                : _cachedTemplates.FirstOrDefault(t => t.Id == meta.LookupTemplateId)?.Name
                  ?? meta.LookupTemplateId;
            LinkedRelationSections.Add(
                RelationDetailSectionBuilder.FromEnrichedRelation(
                    relation.Key,
                    relation.Value,
                    meta,
                    lookupName));
        }
    }

    private async Task ResolveRecordLinkLabelsAsync(CancellationToken cancellationToken)
    {
        foreach (var col in Columns)
        {
            if (col.Type != TemplateFieldType.RecordLink
                || string.IsNullOrWhiteSpace(col.LinkedTemplateId))
            {
                continue;
            }

            var targetTemplate = _cachedTemplates.FirstOrDefault(t => t.Id == col.LinkedTemplateId)
                                 ?? await _templateSchema.GetTemplateAsync(col.LinkedTemplateId!, cancellationToken)
                                     .ConfigureAwait(true);
            if (targetTemplate is null)
            {
                continue;
            }

            var displayKey = !string.IsNullOrWhiteSpace(col.LinkedDisplayFieldKey)
                ? col.LinkedDisplayFieldKey!
                : targetTemplate.Fields.OrderBy(f => f.DisplayOrder).FirstOrDefault()?.Key;
            if (string.IsNullOrEmpty(displayKey))
            {
                continue;
            }

            var ids = Rows
                .SelectMany(r => r.Cells)
                .Where(c => c.FieldKey == col.Key && !string.IsNullOrWhiteSpace(c.LinkedRowId))
                .Select(c => c.LinkedRowId!)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (ids.Count == 0)
            {
                continue;
            }

            var linkedRows = await _records
                .GetRowsByIdsAsync(col.LinkedTemplateId!, ids, cancellationToken)
                .ConfigureAwait(true);
            var byId = linkedRows.ToDictionary(r => r.Id, StringComparer.Ordinal);

            foreach (var row in Rows)
            {
                var cell = row.Cells.FirstOrDefault(c => c.FieldKey == col.Key);
                if (cell is null || string.IsNullOrWhiteSpace(cell.LinkedRowId))
                {
                    continue;
                }

                if (byId.TryGetValue(cell.LinkedRowId!, out var target))
                {
                    cell.Text = target.Values.TryGetValue(displayKey, out var v) && !string.IsNullOrWhiteSpace(v)
                        ? v
                        : cell.LinkedRowId!;
                }
                else
                {
                    cell.Text = "(missing record)";
                }
            }
        }
    }

    [RelayCommand]
    private void AddRow()
    {
        if (!IsEditMode || Columns.Count == 0)
        {
            return;
        }

        Rows.Add(CreateEmptyRow());
    }

    public void InsertRowAt(int index)
    {
        if (!IsEditMode || Columns.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(index, 0, Rows.Count);
        Rows.Insert(clamped, CreateEmptyRow());
    }

    public async Task<bool> InsertColumnAtAsync(int index, string columnName, CancellationToken cancellationToken = default)
    {
        if (SelectedDataTemplate is null)
        {
            StatusMessage = "No template selected.";
            return false;
        }

        var trimmed = (columnName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            StatusMessage = "Column name cannot be empty.";
            return false;
        }

        var fields = SelectedDataTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var safeIndex = Math.Clamp(index, 0, fields.Count);
        fields.Insert(
            safeIndex,
            new TemplateFieldDefinition
            {
                Key = BuildFieldKey(trimmed, safeIndex),
                Label = trimmed,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = safeIndex,
                ValidationPattern = null,
                Options = null,
                LinkedTemplateId = null,
                LinkedDisplayFieldKey = null,
            });

        var remapped = fields
            .Select((f, i) => new TemplateFieldDefinition
            {
                Key = string.IsNullOrWhiteSpace(f.Key) ? BuildFieldKey(f.Label, i) : f.Key,
                Label = f.Label,
                Type = f.Type,
                IsRequired = f.IsRequired,
                DisplayOrder = i,
                ValidationPattern = f.ValidationPattern,
                Options = f.Options,
                LinkedTemplateId = f.LinkedTemplateId,
                LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
            })
            .ToList();

        var patched = new PartTemplateDefinition
        {
            Id = SelectedDataTemplate.Id,
            Name = SelectedDataTemplate.Name,
            Version = SelectedDataTemplate.Version + 1,
            IsPublished = true,
            Fields = remapped,
        };

        try
        {
            await _templateSchema.SaveTemplateAsync(patched, cancellationToken).ConfigureAwait(true);
            await LoadForTemplateAsync(SelectedDataTemplate.Id, cancellationToken).ConfigureAwait(true);
            StatusMessage = $"Column \"{trimmed}\" added.";
            _activityLogger.LogUserAction("Column Added",
                $"Column \"{trimmed}\" added to template \"{SelectedDataTemplate.Name}\"");
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
            return false;
        }
    }

    [RelayCommand]
    private async Task SaveGrid(CancellationToken cancellationToken = default)
    {
        if (!IsEditMode || Columns.Count == 0 || SelectedDataTemplate is null)
        {
            return;
        }

        IsLoading = true;
        StatusMessage = null;
        try
        {
            var templateId = SelectedDataTemplate.Id;
            for (var i = 0; i < Rows.Count; i++)
            {
                var row = Rows[i];
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var cell in row.Cells)
                {
                    // RecordLink supports both linked IDs and direct typed values.
                    dict[cell.FieldKey] = cell.IsRecordLink
                        ? (!string.IsNullOrWhiteSpace(cell.LinkedRowId) ? cell.LinkedRowId : cell.Text)
                        : cell.Text;
                }

                var newId = await _records
                    .UpsertRowAsync(templateId, row.RowId, i, dict, cancellationToken)
                    .ConfigureAwait(true);
                row.RowId = newId;
            }

            await ResolveRecordLinkLabelsAsync(cancellationToken).ConfigureAwait(true);
            IsEditMode = false;
            StatusMessage = "Saved. You're back in view mode.";
            _activityLogger.LogUserAction("Explorer Saved",
                $"Saved {Rows.Count} rows for template \"{SelectedDataTemplate.Name}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ToggleEditMode()
    {
        if (!IsEditMode && !_access.Capabilities.CanEditMasterData && !_access.Capabilities.CanAddMasterData)
        {
            StatusMessage = "You do not have permission to edit or add master data.";
            return;
        }

        IsEditMode = !IsEditMode;
        StatusMessage = IsEditMode
            ? "Edit mode enabled. Make changes, then click Save."
            : "View mode enabled.";
    }

    private MasterDataRowViewModel CreateEmptyRow()
    {
        var row = new MasterDataRowViewModel();
        foreach (var col in Columns)
        {
            row.Cells.Add(CreateCell(col, string.Empty, null));
        }

        return row;
    }

    private MasterDataRowViewModel MapRecordToRow(MasterDataRowRecord rec)
    {
        var row = new MasterDataRowViewModel { RowId = rec.Id };
        foreach (var col in Columns)
        {
            var raw = rec.Values.TryGetValue(col.Key, out var v) ? v : string.Empty;
            if (col.Type == TemplateFieldType.RecordLink)
            {
                if (!string.IsNullOrWhiteSpace(raw) && ObjectId.TryParse(raw, out _))
                {
                    row.Cells.Add(CreateCell(col, string.Empty, raw));
                }
                else
                {
                    // Support direct typed values in link columns for high-volume entry flows.
                    row.Cells.Add(CreateCell(col, raw, null));
                }
            }
            else
            {
                row.Cells.Add(CreateCell(col, raw, null));
            }
        }

        return row;
    }

    private static MasterDataCellViewModel CreateCell(TemplateFieldDefinition col, string text, string? linkedRowId)
    {
        return new MasterDataCellViewModel
        {
            FieldKey = col.Key,
            FieldType = col.Type,
            LinkedTemplateId = col.LinkedTemplateId,
            LinkedDisplayFieldKey = col.LinkedDisplayFieldKey,
            Text = text,
            LinkedRowId = linkedRowId,
        };
    }

    /// <summary>Apply a picked linked row after the user chooses one in the UI.</summary>
    public void SetLinkedCell(MasterDataCellViewModel cell, string? rowId, string displayLabel)
    {
        cell.LinkedRowId = string.IsNullOrWhiteSpace(rowId) ? null : rowId;
        cell.Text = string.IsNullOrWhiteSpace(rowId) ? string.Empty : displayLabel;
    }

    public void ClearLinkedCell(MasterDataCellViewModel cell) => SetLinkedCell(cell, null, string.Empty);

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetTemplateRowsForPickerAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        return await _records.GetRowsAsync(templateId, cancellationToken).ConfigureAwait(true);
    }

    public async Task<PartTemplateDefinition?> GetTemplateDefinitionAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        return await _templateSchema.GetTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetRowsByIdsForTemplateAsync(
        string templateId,
        IReadOnlyList<string> rowIds,
        CancellationToken cancellationToken = default)
    {
        return await _records.GetRowsByIdsAsync(templateId, rowIds, cancellationToken).ConfigureAwait(true);
    }

    public IReadOnlyList<TemplateContextAction> GetContextActionsForField(string fieldKey) =>
        _loadedContextActions
            .Where(a => string.Equals(a.SourceFieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static IReadOnlyDictionary<string, string> BuildSourceRowMap(MasterDataRowViewModel row)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.Cells)
        {
            d[cell.FieldKey] = cell.IsRecordLink ? (cell.LinkedRowId ?? string.Empty) : cell.Text;
        }

        return d;
    }

    public async Task<(PartTemplateDefinition? targetTemplate, IReadOnlyList<MasterDataRowRecord> rows)> RunContextActionAsync(
        TemplateContextAction action,
        IReadOnlyDictionary<string, string> sourceRowValues,
        CancellationToken cancellationToken = default)
    {
        var targetDef = await _templateSchema
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

    public async Task<int> ImportRowsAsync(
        IReadOnlyList<IReadOnlyDictionary<string, string>> importedRows,
        CancellationToken cancellationToken = default)
    {
        if (SelectedDataTemplate is null || importedRows.Count == 0)
        {
            return 0;
        }

        var templateId = SelectedDataTemplate.Id;
        var startOrder = Rows.Count;
        var importedCount = 0;
        foreach (var source in importedRows)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in Columns)
            {
                values[col.Key] = source.TryGetValue(col.Key, out var value)
                    ? value?.Trim() ?? string.Empty
                    : string.Empty;
            }

            await _records.UpsertRowAsync(templateId, null, startOrder + importedCount, values, cancellationToken)
                .ConfigureAwait(true);
            importedCount++;
        }

        await LoadForTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);

        if (importedCount > 0)
        {
            _activityLogger.LogUserAction("Rows Imported",
                $"Imported {importedCount} rows into template \"{SelectedDataTemplate.Name}\"");
        }

        return importedCount;
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

    private static string BuildFieldKey(string label, int index)
    {
        var lowered = label.ToLowerInvariant();
        var chars = lowered.Where(static c => char.IsLetterOrDigit(c) || c == '_').ToArray();
        var baseKey = new string(chars);
        if (string.IsNullOrEmpty(baseKey))
        {
            baseKey = "column";
        }

        return $"{baseKey}_{index}";
    }
}
