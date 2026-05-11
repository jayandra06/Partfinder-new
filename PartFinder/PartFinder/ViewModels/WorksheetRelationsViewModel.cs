using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class WorksheetRelationsViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templates;
    private readonly BackendApiClient _api;

    public WorksheetRelationsViewModel(ITemplateSchemaService templates, BackendApiClient api)
    {
        _templates = templates;
        _api = api;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<RelationColumnPickItem> PrimaryColumns { get; } = [];
    public ObservableCollection<RelationColumnPickItem> LookupColumns { get; } = [];

    // ── NEW: all saved relations for the chip strip ──────────────────────────
    public ObservableCollection<WorksheetRelationDto> AllRelations { get; } = [];
    public ObservableCollection<RelationChipItem> RelationChips { get; } = [];

    [ObservableProperty]
    private string? _selectedRelationId;

    public int RelationCount => AllRelations.Count;
    public bool HasRelations => AllRelations.Count > 0;
    // ────────────────────────────────────────────────────────────────────────

    [ObservableProperty]
    private PartTemplateDefinition? _primaryTemplate;

    [ObservableProperty]
    private PartTemplateDefinition? _lookupTemplate;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLoadedRelation))]
    private WorksheetRelationDto? _loadedRelation;

    // ── NEW: computed property for Delete button visibility ──────────────────
    public bool HasLoadedRelation => LoadedRelation is not null;
    public bool HasNoPrimaryColumns => PrimaryColumns.Count == 0;
    public bool HasNoLookupColumns => LookupColumns.Count == 0;
    // ────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var list = await _templates.GetTemplatesAsync(ct).ConfigureAwait(true);
            Templates.Clear();
            foreach (var item in list)
            {
                Templates.Add(item);
            }

            PrimaryTemplate = Templates.FirstOrDefault();
            if (Templates.Count > 1)
            {
                LookupTemplate = Templates.Skip(1).FirstOrDefault();
            }
            else
            {
                LookupTemplate = Templates.FirstOrDefault();
            }
            BuildColumnLists();

            // ── NEW: load all saved relations for chip strip ─────────────────
            await LoadAllRelationsAsync(ct).ConfigureAwait(true);
            // ────────────────────────────────────────────────────────────────

            await TryLoadExistingRelationAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnPrimaryTemplateChanged(PartTemplateDefinition? value)
    {
        BuildColumnLists();
        _ = TryLoadExistingRelationAsync();
    }

    partial void OnLookupTemplateChanged(PartTemplateDefinition? value)
    {
        BuildColumnLists();
        _ = TryLoadExistingRelationAsync();
    }

    // ── NEW: Select a relation chip → load it into the editor ───────────────
    [RelayCommand]
    private void SelectRelation(RelationChipItem chip)
    {
        SelectedRelationId = chip.RelationId;
        var primary = Templates.FirstOrDefault(t => t.Id == chip.PrimaryTemplateId);
        var lookup = Templates.FirstOrDefault(t => t.Id == chip.LookupTemplateId);
        if (primary is not null) PrimaryTemplate = primary;
        if (lookup is not null) LookupTemplate = lookup;
        // OnPrimaryTemplateChanged / OnLookupTemplateChanged will trigger TryLoadExistingRelationAsync
    }

    // ── NEW: Delete the currently loaded relation ────────────────────────────
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (LoadedRelation is null) return;
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var (ok, error) = await _api.DeleteRelationAsync(LoadedRelation.Id).ConfigureAwait(true);
            if (!ok)
            {
                ErrorMessage = error ?? "Failed to delete relation.";
                return;
            }
            LoadedRelation = null;
            SelectedRelationId = null;
            StatusMessage = "Relation deleted.";
            await LoadAllRelationsAsync().ConfigureAwait(true);
            BuildColumnLists();
        }
        finally
        {
            IsLoading = false;
        }
    }
    // ────────────────────────────────────────────────────────────────────────

    // ── NEW: Reset — uncheck all user selections, rebuild column lists ────────
    [RelayCommand]
    private void Reset()
    {
        BuildColumnLists();
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }
    // ────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            ErrorMessage = "Choose both primary and lookup templates.";
            return;
        }

        var selectedKeys = PrimaryColumns.Where(x => x.IsChecked).Select(x => x.ColumnName).ToList();
        if (selectedKeys.Count == 0)
        {
            ErrorMessage = "Select at least one match key on the primary side.";
            return;
        }

        var selectedDisplays = LookupColumns.Where(x => x.IsChecked).Select(x => x.ColumnName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var key in selectedKeys)
        {
            if (!selectedDisplays.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                selectedDisplays.Add(key);
            }
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var dto = new SaveWorksheetRelationRequest
            {
                PrimaryTemplateId = PrimaryTemplate.Id,
                LookupTemplateId = LookupTemplate.Id,
                Name = $"{PrimaryTemplate.Name} to {LookupTemplate.Name}",
                TriggerColumn = selectedKeys.FirstOrDefault() ?? "",
                MatchKeys = selectedKeys.Select(k => new RelationMatchPairDto { SourceColumn = k, TargetColumn = k }).ToList(),
                DisplayColumns = selectedDisplays,
                MenuLabel = $"{LookupTemplate.Name} info",
            };

            var (ok, error, relation) = LoadedRelation is null
                ? await _api.CreateRelationAsync(dto).ConfigureAwait(true)
                : await _api.UpdateRelationAsync(LoadedRelation.Id, dto).ConfigureAwait(true);

            if (!ok || relation is null)
            {
                ErrorMessage = error ?? "Failed to save relation.";
                return;
            }

            LoadedRelation = relation;
            SelectedRelationId = relation.Id;
            StatusMessage = "Worksheet relation saved.";

            // ── NEW: refresh chip strip after save ───────────────────────────
            await LoadAllRelationsAsync().ConfigureAwait(true);
            // ────────────────────────────────────────────────────────────────
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void BuildColumnLists()
    {
        PrimaryColumns.Clear();
        LookupColumns.Clear();
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            return;
        }

        foreach (var f in PrimaryTemplate.Fields.OrderBy(f => f.DisplayOrder))
        {
            var item = new RelationColumnPickItem(f.Label, isPrimary: true);
            item.PropertyChanged += OnPrimaryColumnChanged;
            PrimaryColumns.Add(item);
        }

        var primarySet = new HashSet<string>(PrimaryColumns.Select(x => x.ColumnName), StringComparer.OrdinalIgnoreCase);
        foreach (var f in LookupTemplate.Fields.OrderBy(f => f.DisplayOrder))
        {
            var isAutoKey = primarySet.Contains(f.Label);
            var item = new RelationColumnPickItem(f.Label, isPrimary: false)
            {
                IsDisabled = isAutoKey,
                IsChecked = isAutoKey,
                IsAutoMatchKey = isAutoKey,
            };
            LookupColumns.Add(item);
        }

        OnPropertyChanged(nameof(HasNoPrimaryColumns));
        OnPropertyChanged(nameof(HasNoLookupColumns));
    }

    private void OnPrimaryColumnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RelationColumnPickItem.IsChecked))
        {
            return;
        }

        SyncLookupWithPrimary();
    }

    private void SyncLookupWithPrimary()
    {
        var selectedKeys = new HashSet<string>(
            PrimaryColumns.Where(x => x.IsChecked).Select(x => x.ColumnName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var lookup in LookupColumns)
        {
            var shouldAuto = selectedKeys.Contains(lookup.ColumnName);
            lookup.IsAutoMatchKey = shouldAuto;
            lookup.IsDisabled = shouldAuto;
            if (shouldAuto)
            {
                lookup.IsChecked = true;
            }
        }
    }

    private async Task TryLoadExistingRelationAsync(CancellationToken ct = default)
    {
        LoadedRelation = null;
        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            return;
        }

        var (ok, _, relations) = await _api.GetRelationsAsync(ct).ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        var existing = relations.FirstOrDefault(
            r => string.Equals(r.PrimaryTemplateId, PrimaryTemplate.Id, StringComparison.Ordinal)
              && string.Equals(r.LookupTemplateId, LookupTemplate.Id, StringComparison.Ordinal));
        if (existing is null)
        {
            return;
        }

        LoadedRelation = existing;
        SelectedRelationId = existing.Id;
        var keySet = new HashSet<string>(existing.MatchKeys.Select(k => k.SourceColumn), StringComparer.OrdinalIgnoreCase);
        var displaySet = new HashSet<string>(existing.DisplayColumns, StringComparer.OrdinalIgnoreCase);

        foreach (var p in PrimaryColumns)
        {
            p.IsChecked = keySet.Contains(p.ColumnName);
        }
        SyncLookupWithPrimary();
        foreach (var l in LookupColumns.Where(x => !x.IsDisabled))
        {
            l.IsChecked = displaySet.Contains(l.ColumnName);
        }
    }

    // ── NEW: private helper — load all relations + build chip items ──────────
    private async Task LoadAllRelationsAsync(CancellationToken ct = default)
    {
        var (ok, _, relations) = await _api.GetRelationsAsync(ct).ConfigureAwait(true);
        if (!ok) return;

        AllRelations.Clear();
        RelationChips.Clear();

        foreach (var r in relations)
        {
            AllRelations.Add(r);

            var primaryName = Templates.FirstOrDefault(t => t.Id == r.PrimaryTemplateId)?.Name ?? r.PrimaryTemplateId;
            var lookupName = Templates.FirstOrDefault(t => t.Id == r.LookupTemplateId)?.Name ?? r.LookupTemplateId;
            RelationChips.Add(new RelationChipItem(r.Id, r.PrimaryTemplateId, r.LookupTemplateId, primaryName, lookupName));
        }

        OnPropertyChanged(nameof(RelationCount));
        OnPropertyChanged(nameof(HasRelations));
    }
    // ────────────────────────────────────────────────────────────────────────
}

// ── NEW: chip item for the relations strip ───────────────────────────────────
public sealed class RelationChipItem(
    string relationId,
    string primaryTemplateId,
    string lookupTemplateId,
    string primaryName,
    string lookupName)
{
    public string RelationId { get; } = relationId;
    public string PrimaryTemplateId { get; } = primaryTemplateId;
    public string LookupTemplateId { get; } = lookupTemplateId;
    public string Label { get; } = $"{primaryName}  →  {lookupName}";
}
// ────────────────────────────────────────────────────────────────────────────

public partial class RelationColumnPickItem : ObservableObject
{
    public RelationColumnPickItem(string columnName, bool isPrimary)
    {
        ColumnName = columnName;
        IsPrimary = isPrimary;
    }

    public string ColumnName { get; }
    public bool IsPrimary { get; }

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private bool _isDisabled;

    partial void OnIsDisabledChanged(bool value) => OnPropertyChanged(nameof(IsSelectable));

    [ObservableProperty]
    private bool _isAutoMatchKey;

    public bool IsSelectable => !IsDisabled;
}
