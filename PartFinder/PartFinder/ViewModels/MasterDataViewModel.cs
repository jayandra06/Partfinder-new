using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MongoDB.Bson;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class MasterDataViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _records;
    private readonly IContextActionsService _contextActions;
    private readonly BackendApiClient _api;
    private readonly ActivityLogger _activityLogger;

    private bool _suppressTemplateSelectionChange;
    private IReadOnlyList<PartTemplateDefinition> _cachedTemplates = Array.Empty<PartTemplateDefinition>();
    private IReadOnlyList<TemplateContextAction> _loadedContextActions = Array.Empty<TemplateContextAction>();
    private readonly List<WorksheetRelationDto> _worksheetRelations = [];
    private CancellationTokenSource? _templateLoadCts;
    private readonly Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);

    private readonly ICurrentUserAccessService _access;

    public event EventHandler? ColumnFiltersChanged;

    public MasterDataViewModel(
        ITemplateSchemaService templateSchema,
        IMasterDataRecordsService records,
        IContextActionsService contextActions,
        BackendApiClient api,
        ActivityLogger activityLogger,
        ICurrentUserAccessService access)
    {
        _templateSchema = templateSchema;
        _records = records;
        _contextActions = contextActions;
        _api = api;
        _activityLogger = activityLogger;
        _access = access;
    }

    public ObservableCollection<PartTemplateDefinition> DataTemplates { get; } = new();

    public ObservableCollection<TemplateFieldDefinition> Columns { get; } = new();

    public ObservableCollection<MasterDataRowViewModel> Rows { get; } = new();

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

    public string EditModeButtonText => IsEditMode ? "Stop Editing" : "Edit Data";

    public bool CanEditMasterData => _access.Capabilities.CanEditMasterData;
    public bool CanAddMasterData => _access.Capabilities.CanAddMasterData;
    public bool CanCopyMasterData => _access.Capabilities.CanCopyMasterData;
    public bool CanDeleteMasterData => _access.Capabilities.CanDeleteMasterData;

    public IReadOnlyList<MasterDataRowViewModel> GetVisibleRows() =>
        Rows.Where(IsRowVisible).ToList();

    public bool IsRowVisible(MasterDataRowViewModel row) =>
        _columnFilters.Count == 0 || RowMatchesColumnFilters(row);

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

        ColumnFiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearColumnFilters()
    {
        if (_columnFilters.Count == 0)
        {
            return;
        }

        _columnFilters.Clear();
        ColumnFiltersChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool RowMatchesColumnFilters(MasterDataRowViewModel row)
    {
        foreach (var filter in _columnFilters)
        {
            var cell = row.Cells.FirstOrDefault(
                c => string.Equals(c.FieldKey, filter.Key, StringComparison.OrdinalIgnoreCase));
            var text = cell?.Text ?? string.Empty;
            if (!text.Contains(filter.Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public bool CanAddRows =>
        IsEditMode && Columns.Count > 0 && (CanEditMasterData || CanAddMasterData);

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(EditModeButtonText));
        OnPropertyChanged(nameof(CanAddRows));
        OnPropertyChanged(nameof(CanManageColumns));
        OnPropertyChanged(nameof(CanRemoveColumns));
        AddRowCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedDataTemplateChanged(PartTemplateDefinition? value)
    {
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

            ShowTemplatePicker = DataTemplates.Count > 1;

            var master = _cachedTemplates.FirstOrDefault(
                t => string.Equals(t.Name, MongoTemplateSchemaService.MasterDataTemplateName, StringComparison.OrdinalIgnoreCase));
            var pick = master ?? DataTemplates[0];

            _suppressTemplateSelectionChange = true;
            SelectedDataTemplate = pick;
            _suppressTemplateSelectionChange = false;

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

    public async Task LoadForTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default,
        bool restoreEditModeAfterLoad = false)
    {
        _templateLoadCts?.Cancel();
        _templateLoadCts?.Dispose();
        _templateLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var loadCt = _templateLoadCts.Token;
        var loadForId = templateId;
        var preserveEditMode = restoreEditModeAfterLoad && IsEditMode;

        IsLoading = true;
        StatusMessage = null;
        ShowNoTemplateHint = false;
        if (!preserveEditMode)
        {
            IsEditMode = false;
        }
        Columns.Clear();
        Rows.Clear();
        _loadedContextActions = Array.Empty<TemplateContextAction>();
        _worksheetRelations.Clear();
        _columnFilters.Clear();

        try
        {
            try
            {
                _cachedTemplates = await _templateSchema
                    .GetTemplatesAsync(loadCt)
                    .ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (loadCt.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // Keep prior cache for link resolution if refresh fails.
            }

            loadCt.ThrowIfCancellationRequested();

            var template = await _templateSchema
                .GetTemplateAsync(loadForId, loadCt)
                .ConfigureAwait(true);

            loadCt.ThrowIfCancellationRequested();
            if (!IsLoadStillCurrent(loadForId))
            {
                return;
            }

            if (template is null || template.Fields.Count == 0)
            {
                ShowNoTemplateHint = true;
                return;
            }

            foreach (var f in template.Fields.OrderBy(x => x.DisplayOrder))
            {
                Columns.Add(f);
            }

            _loadedContextActions = await _contextActions
                .GetForSourceTemplateAsync(template.Id, loadCt)
                .ConfigureAwait(true);

            var (relationsOk, _, relations) = await _api.GetRelationsAsync(loadCt).ConfigureAwait(true);
            if (relationsOk)
            {
                _worksheetRelations.Clear();
                _worksheetRelations.AddRange(
                    relations.Where(r => string.Equals(r.PrimaryTemplateId, template.Id, StringComparison.Ordinal)));
            }

            loadCt.ThrowIfCancellationRequested();
            if (!IsLoadStillCurrent(loadForId))
            {
                return;
            }

            var persisted = await _records
                .GetStoredRowsAsync(template.Id, loadCt)
                .ConfigureAwait(true);
            if (persisted.Count == 0)
            {
                persisted = await _records
                    .GetRowsAsync(template.Id, loadCt)
                    .ConfigureAwait(true);
            }

            loadCt.ThrowIfCancellationRequested();
            if (!IsLoadStillCurrent(loadForId))
            {
                return;
            }

            if (persisted.Count == 0)
            {
                Rows.Add(CreateEmptyRow());
            }
            else
            {
                var added = 0;
                foreach (var rec in persisted)
                {
                    if (loadCt.IsCancellationRequested || !IsLoadStillCurrent(loadForId))
                    {
                        return;
                    }

                    Rows.Add(MapRecordToRow(rec));
                    added++;
                    if (added % 250 == 0)
                    {
                        await Task.Yield();
                    }
                }
            }

            await ResolveRecordLinkLabelsAsync(loadCt).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (loadCt.IsCancellationRequested)
        {
            // Superseded by a newer template selection or page unload.
        }
        catch (InvalidOperationException)
        {
            if (IsLoadStillCurrent(loadForId))
            {
                ShowNoDatabaseHint = true;
            }
        }
        catch (Exception ex)
        {
            if (IsLoadStillCurrent(loadForId))
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }
        finally
        {
            if (IsLoadStillCurrent(loadForId))
            {
                IsLoading = false;
                if (preserveEditMode)
                {
                    IsEditMode = true;
                }

                OnPropertyChanged(nameof(CanAddRows));
                OnPropertyChanged(nameof(CanManageColumns));
                OnPropertyChanged(nameof(CanRemoveColumns));
                AddRowCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool IsLoadStillCurrent(string templateId) =>
        string.Equals(SelectedDataTemplate?.Id, templateId, StringComparison.Ordinal);

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

    [RelayCommand(CanExecute = nameof(CanAddRows))]
    private void AddRow()
    {
        Rows.Add(CreateEmptyRow());
        StatusMessage = $"Added row {Rows.Count}. Click Save when finished.";
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

    public bool CanManageColumns =>
        IsEditMode && Columns.Count > 0 && (CanEditMasterData || CanAddMasterData);

    public bool CanRemoveColumns =>
        IsEditMode && CanEditMasterData && Columns.Count > 1;

    public async Task<bool> InsertColumnAtAsync(int index, string columnName, CancellationToken cancellationToken = default)
    {
        if (!CanManageColumns)
        {
            StatusMessage = "You do not have permission to add columns.";
            return false;
        }

        if (SelectedDataTemplate is null)
        {
            StatusMessage = "No template selected.";
            return false;
        }

        var trimmed = (columnName ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            StatusMessage = "Enter a column name in the add-column bar above the table.";
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

    public async Task<bool> RemoveColumnAtAsync(int index, CancellationToken cancellationToken = default)
    {
        if (!IsEditMode || !CanEditMasterData)
        {
            StatusMessage = "You do not have permission to remove columns.";
            return false;
        }

        if (SelectedDataTemplate is null)
        {
            return false;
        }

        if (index < 0 || index >= Columns.Count)
        {
            StatusMessage = "Column is no longer on the grid. Try again.";
            return false;
        }

        var fieldKey = Columns[index].Key;
        var fields = SelectedDataTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var fieldIndex = fields.FindIndex(
            f => string.Equals(f.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
        if (fieldIndex < 0)
        {
            StatusMessage = "Column could not be found on the template.";
            return false;
        }

        if (fields.Count <= 1)
        {
            StatusMessage = "At least one column is required.";
            return false;
        }

        var removed = fields[fieldIndex].Label;
        fields.RemoveAt(fieldIndex);
        var remapped = fields
            .Select((f, i) => new TemplateFieldDefinition
            {
                Key = f.Key,
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
            await LoadForTemplateAsync(
                SelectedDataTemplate.Id,
                cancellationToken,
                restoreEditModeAfterLoad: true).ConfigureAwait(true);
            StatusMessage = $"Column \"{removed}\" removed.";
            _activityLogger.LogUserAction("Column Removed",
                $"Column \"{removed}\" removed from template \"{SelectedDataTemplate.Name}\"");
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
            _activityLogger.LogUserAction("Master Data Saved",
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
            ? "Edit mode: add columns above the grid, remove with × on column headers, then Save."
            : "View mode: browse data only.";
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

    public bool HasWorksheetRelationsForFieldKey(string fieldKey)
    {
        var field = Columns.FirstOrDefault(c => string.Equals(c.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
        return field is not null && GetWorksheetRelationsForField(field).Count > 0;
    }

    public IReadOnlyList<WorksheetRelationDto> GetWorksheetRelationsForField(TemplateFieldDefinition field) =>
        _worksheetRelations.Where(r =>
                string.Equals(r.TriggerColumn, field.Label, StringComparison.OrdinalIgnoreCase)
                || r.MatchKeys.Any(m =>
                    string.Equals(m.SourceColumn, field.Label, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    public async Task<IReadOnlyList<WorksheetRelationMatchResult>> QueryWorksheetRelationsForCellAsync(
        string fieldKey,
        MasterDataRowViewModel row,
        CancellationToken cancellationToken = default)
    {
        if (SelectedDataTemplate is null)
        {
            return [];
        }

        var field = Columns.FirstOrDefault(c => string.Equals(c.Key, fieldKey, StringComparison.OrdinalIgnoreCase));
        if (field is null)
        {
            return [];
        }

        var sourceMap = BuildSourceRowMap(row);
        var relations = GetWorksheetRelationsForField(field);
        if (relations.Count == 0)
        {
            return [];
        }

        var results = new List<WorksheetRelationMatchResult>();
        foreach (var relation in relations)
        {
            var lookup = _cachedTemplates.FirstOrDefault(t => t.Id == relation.LookupTemplateId)
                         ?? await _templateSchema.GetTemplateAsync(relation.LookupTemplateId, cancellationToken)
                             .ConfigureAwait(true);
            if (lookup is null)
            {
                continue;
            }

            var lookupRows = await _records.GetRowsAsync(relation.LookupTemplateId, cancellationToken).ConfigureAwait(true);
            var matches = lookupRows
                .Where(r => RowMatchesWorksheetRelation(
                    r.Values,
                    lookup,
                    relation,
                    sourceMap,
                    SelectedDataTemplate))
                .ToList();

            var header = !string.IsNullOrWhiteSpace(relation.MenuLabel)
                ? relation.MenuLabel
                : lookup.Name;
            results.Add(new WorksheetRelationMatchResult(header, lookup, relation, matches));
        }

        return results;
    }

    public async Task<WorksheetRelationMatchResult?> QueryDirectRecordLinkAsync(
        MasterDataCellViewModel cell,
        CancellationToken cancellationToken = default)
    {
        if (!cell.IsRecordLink
            || string.IsNullOrWhiteSpace(cell.LinkedRowId)
            || string.IsNullOrWhiteSpace(cell.LinkedTemplateId))
        {
            return null;
        }

        var lookup = await _templateSchema.GetTemplateAsync(cell.LinkedTemplateId, cancellationToken).ConfigureAwait(true);
        if (lookup is null)
        {
            return null;
        }

        var rows = await _records
            .GetRowsByIdsAsync(cell.LinkedTemplateId, [cell.LinkedRowId], cancellationToken)
            .ConfigureAwait(true);
        return new WorksheetRelationMatchResult(lookup.Name, lookup, null, rows);
    }

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

    private static bool RowMatchesWorksheetRelation(
        IReadOnlyDictionary<string, string> targetValues,
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto relation,
        IReadOnlyDictionary<string, string> sourceValues,
        PartTemplateDefinition sourceTemplate)
    {
        var pairs = relation.MatchKeys;
        if (pairs.Count == 0 && !string.IsNullOrWhiteSpace(relation.TriggerColumn))
        {
            pairs =
            [
                new RelationMatchPairDto
                {
                    SourceColumn = relation.TriggerColumn,
                    TargetColumn = relation.TriggerColumn,
                },
            ];
        }

        if (pairs.Count == 0)
        {
            return false;
        }

        foreach (var pair in pairs)
        {
            var sourceField = sourceTemplate.Fields.FirstOrDefault(f =>
                string.Equals(f.Label, pair.SourceColumn, StringComparison.OrdinalIgnoreCase));
            var targetField = lookupTemplate.Fields.FirstOrDefault(f =>
                string.Equals(f.Label, pair.TargetColumn, StringComparison.OrdinalIgnoreCase));
            if (sourceField is null || targetField is null)
            {
                return false;
            }

            if (!sourceValues.TryGetValue(sourceField.Key, out var sourceValue)
                || string.IsNullOrWhiteSpace(sourceValue))
            {
                return false;
            }

            if (!targetValues.TryGetValue(targetField.Key, out var targetValue))
            {
                return false;
            }

            if (!string.Equals(sourceValue.Trim(), targetValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public static IReadOnlyList<TemplateFieldDefinition> ResolveRelationDisplayFields(
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto? relation)
    {
        var ordered = lookupTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        if (relation is null || relation.DisplayColumns.Count == 0)
        {
            return ordered;
        }

        var picked = new List<TemplateFieldDefinition>();
        foreach (var label in relation.DisplayColumns)
        {
            var field = ordered.FirstOrDefault(f =>
                string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase));
            if (field is not null)
            {
                picked.Add(field);
            }
        }

        return picked.Count > 0 ? picked : ordered;
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

public sealed class WorksheetRelationMatchResult(
    string tabHeader,
    PartTemplateDefinition lookupTemplate,
    WorksheetRelationDto? relation,
    IReadOnlyList<MasterDataRowRecord> matches)
{
    public string TabHeader { get; } = tabHeader;
    public PartTemplateDefinition LookupTemplate { get; } = lookupTemplate;
    public WorksheetRelationDto? Relation { get; } = relation;
    public IReadOnlyList<MasterDataRowRecord> Matches { get; } = matches;
}
