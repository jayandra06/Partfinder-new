using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Core;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class WorksheetRelationsViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templates;
    private readonly BackendApiClient _api;
    private readonly ExplorerNavigationCoordinator _explorerNav;
    private readonly ICurrentUserAccessService _access;
    private readonly WorksheetRelationCleanupService _relationCleanup;

    public WorksheetRelationsViewModel(
        ITemplateSchemaService templates,
        BackendApiClient api,
        ExplorerNavigationCoordinator explorerNav,
        ICurrentUserAccessService access,
        WorksheetRelationCleanupService relationCleanup)
    {
        _templates = templates;
        _api = api;
        _explorerNav = explorerNav;
        _access = access;
        _relationCleanup = relationCleanup;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<PartTemplateDefinition> LookupTemplateChoices { get; } = [];
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

    public int SharedColumnCount =>
        PrimaryTemplate is null || LookupTemplate is null
            ? 0
            : WorksheetRelationUiText.GetSharedColumnNames(
                PrimaryTemplate.Fields.Select(f => f.Label),
                LookupTemplate.Fields.Select(f => f.Label)).Count;

    public bool HasSharedColumns => SharedColumnCount > 0;

    public string SharedColumnSummary => WorksheetRelationUiText.GetSharedColumnsSummary(SharedColumnCount);

    public bool CanCreateTemplateLinks => Templates.Count > 1;

    public bool CanSaveLink =>
        CanCreateTemplateLinks
        && PrimaryTemplate is not null
        && LookupTemplate is not null
        && HasSharedColumns
        && PrimaryColumns.Any(c => c.IsChecked);

    public string NoTemplatesToLinkMessage =>
        "No templates available to link. Template relationships require at least two templates in your organisation. Create another template to enable linking.";

    [ObservableProperty]
    private string relationHealthInsight = "Open Explorer and load a template to see link match rates.";

    public bool HasRelationHealthInsight => !string.IsNullOrWhiteSpace(RelationHealthInsight);
    // ────────────────────────────────────────────────────────────────────────

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var list = await _templates.GetTemplatesAsync(ct).ConfigureAwait(true);
            var filtered = TemplateCatalogFilters.OrganizationTemplates(
                _access.FilterTemplatesForParts(list));
            Templates.Clear();
            foreach (var item in filtered)
            {
                Templates.Add(item);
            }

            PrimaryTemplate = Templates.FirstOrDefault();
            RefreshLookupTemplateChoices();
            LookupTemplate = LookupTemplateChoices.FirstOrDefault();
            BuildColumnLists();

            OnPropertyChanged(nameof(CanCreateTemplateLinks));
            OnPropertyChanged(nameof(NoTemplatesToLinkMessage));

            // ── NEW: load all saved relations for chip strip ─────────────────
            await LoadAllRelationsAsync(ct).ConfigureAwait(true);
            // ────────────────────────────────────────────────────────────────

            await TryLoadExistingRelationAsync(ct).ConfigureAwait(true);
            RefreshRelationHealthInsight();
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

    private void RefreshRelationHealthInsight()
    {
        var snapshot = _explorerNav.LastHealth;
        if (snapshot is null)
        {
            RelationHealthInsight =
                HasRelations
                    ? $"{RelationCount} link(s) saved — open Explorer to view match rates."
                    : "No links yet. Match shared columns between templates, then check match rates in Explorer.";
            OnPropertyChanged(nameof(HasRelationHealthInsight));
            return;
        }

        if (PrimaryTemplate is not null
            && !string.Equals(snapshot.PrimaryTemplateId, PrimaryTemplate.Id, StringComparison.Ordinal))
        {
            RelationHealthInsight =
                $"Latest health is for \"{snapshot.PrimaryTemplateName}\" ({snapshot.MatchedRows}/{snapshot.TotalRows} matched).";
        }
        else
        {
            RelationHealthInsight =
                $"\"{snapshot.PrimaryTemplateName}\": {snapshot.MatchedRows}/{snapshot.TotalRows} rows matched ({snapshot.MatchRatePercent}%).";
        }

        OnPropertyChanged(nameof(HasRelationHealthInsight));
    }

    partial void OnPrimaryTemplateChanged(PartTemplateDefinition? value)
    {
        ErrorMessage = string.Empty;

        RefreshLookupTemplateChoices();
        BuildColumnLists();
        OnPropertyChanged(nameof(SharedColumnCount));
        OnPropertyChanged(nameof(HasSharedColumns));
        OnPropertyChanged(nameof(SharedColumnSummary));
        OnPropertyChanged(nameof(CanSaveLink));
        SaveCommand.NotifyCanExecuteChanged();
        RefreshRelationHealthInsight();
        _ = TryLoadExistingRelationAsync();
    }

    partial void OnLookupTemplateChanged(PartTemplateDefinition? value)
    {
        if (value is not null && PrimaryTemplate is not null &&
            string.Equals(value.Id, PrimaryTemplate.Id, StringComparison.OrdinalIgnoreCase))
        {
            LookupTemplate = LookupTemplateChoices.FirstOrDefault();
            return;
        }
        ErrorMessage = string.Empty;

        BuildColumnLists();
        OnPropertyChanged(nameof(SharedColumnCount));
        OnPropertyChanged(nameof(HasSharedColumns));
        OnPropertyChanged(nameof(SharedColumnSummary));
        OnPropertyChanged(nameof(CanSaveLink));
        SaveCommand.NotifyCanExecuteChanged();
        _ = TryLoadExistingRelationAsync();
    }

    [RelayCommand]
    private void ApplySharedMatchKeys()
    {
        var count = SelectSharedMatchColumns();
        StatusMessage = count > 0
            ? $"Selected {count} shared match column(s)."
            : "No shared column names between these templates.";
    }

    private int SelectSharedMatchColumns()
    {
        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            return 0;
        }

        var shared = new HashSet<string>(
            WorksheetRelationUiText.GetSharedColumnNames(
                PrimaryTemplate.Fields.Select(f => f.Label),
                LookupTemplate.Fields.Select(f => f.Label)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var column in PrimaryColumns)
        {
            column.IsChecked = shared.Contains(column.ColumnName);
        }

        SyncLookupWithPrimary();
        OnPropertyChanged(nameof(SelectedMatchColumnHint));
        return shared.Count;
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
            var (ok, error) = await _relationCleanup
                .DeleteRelationLinkAsync(LoadedRelation, AllRelations.ToList())
                .ConfigureAwait(true);
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

    [RelayCommand]
    private async Task DeleteRelationChipAsync(RelationChipItem? chip)
    {
        if (chip is null)
        {
            return;
        }

        var relation = AllRelations.FirstOrDefault(r =>
            string.Equals(r.Id, chip.RelationId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(r.Id, chip.RelationId + ":mirror", StringComparison.OrdinalIgnoreCase));
        if (relation is null)
        {
            relation = AllRelations.FirstOrDefault(r =>
                string.Equals(r.PrimaryTemplateId, chip.PrimaryTemplateId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.LookupTemplateId, chip.LookupTemplateId, StringComparison.OrdinalIgnoreCase));
        }

        if (relation is null)
        {
            ErrorMessage = "Could not find that link to delete.";
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var (ok, error) = await _relationCleanup
                .DeleteRelationLinkAsync(relation, AllRelations.ToList())
                .ConfigureAwait(true);
            if (!ok)
            {
                ErrorMessage = error ?? "Failed to delete relation.";
                return;
            }

            if (string.Equals(SelectedRelationId, chip.RelationId, StringComparison.OrdinalIgnoreCase))
            {
                LoadedRelation = null;
                SelectedRelationId = null;
                BuildColumnLists();
            }

            StatusMessage = "Link removed.";
            await LoadAllRelationsAsync().ConfigureAwait(true);
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
        SelectSharedMatchColumns();
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
    }
    // ────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanSaveLink))]
    private async Task SaveAsync()
    {
        if (!CanCreateTemplateLinks)
        {
            ErrorMessage = NoTemplatesToLinkMessage;
            return;
        }

        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            ErrorMessage = "Choose Template 1 and Template 2.";
            return;
        }

        var sharedNames = GetSharedColumnNameSet();
        var selectedKeys = PrimaryColumns
            .Where(x => x.IsChecked && sharedNames.Contains(x.ColumnName))
            .Select(x => x.ColumnName)
            .ToList();
        if (selectedKeys.Count == 0)
        {
            ErrorMessage = sharedNames.Count == 0
                ? "These templates have no columns with the same name. Rename or add matching column labels first."
                : "Select at least one shared key column on Template 1.";
            return;
        }

        var selectedDisplays = selectedKeys.ToList();

        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var dto = new SaveWorksheetRelationRequest
            {
                PrimaryTemplateId = PrimaryTemplate.Id,
                LookupTemplateId = LookupTemplate.Id,
                Name = $"{PrimaryTemplate.Name} ↔ {LookupTemplate.Name}",
                TriggerColumn = selectedKeys.FirstOrDefault() ?? "",
                MatchKeys = selectedKeys.Select(k => new RelationMatchPairDto { SourceColumn = k, TargetColumn = k }).ToList(),
                DisplayColumns = selectedDisplays,
                MenuLabel = $"{LookupTemplate.Name}",
            };

            var useUpdate = LoadedRelation is not null && !string.IsNullOrWhiteSpace(LoadedRelation.Id);
            var (ok, error, relation) = useUpdate
                ? await _api.UpdateRelationAsync(LoadedRelation!.Id, dto).ConfigureAwait(true)
                : await _api.CreateRelationAsync(dto).ConfigureAwait(true);

            if (!ok || relation is null)
            {
                ErrorMessage = error ?? "Failed to save relation.";
                return;
            }

            LoadedRelation = relation;
            SelectedRelationId = relation.Id;

            var reverseRequest = WorksheetRelationBidirectional.BuildReverseRequest(
                dto,
                PrimaryTemplate.Name,
                LookupTemplate.Name);
            await LoadAllRelationsAsync().ConfigureAwait(true);
            var existingReverse = WorksheetRelationBidirectional.FindRelationBetween(
                AllRelations.ToList(),
                reverseRequest.PrimaryTemplateId,
                reverseRequest.LookupTemplateId);
            var (reverseOk, reverseError, _) = existingReverse is not null
                ? await _api.UpdateRelationAsync(existingReverse.Id, reverseRequest).ConfigureAwait(true)
                : await _api.CreateRelationAsync(reverseRequest).ConfigureAwait(true);
            if (!reverseOk)
            {
                StatusMessage = "Forward link saved; reverse link failed: " + (reverseError ?? "unknown error");
            }
            else
            {
                StatusMessage = "Worksheet relation saved (both directions).";
            }

            await LoadAllRelationsAsync().ConfigureAwait(true);
            // ────────────────────────────────────────────────────────────────
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshLookupTemplateChoices()
    {
        LookupTemplateChoices.Clear();
        foreach (var template in Templates)
        {
            if (PrimaryTemplate is not null &&
                string.Equals(template.Id, PrimaryTemplate.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            LookupTemplateChoices.Add(template);
        }

        if (LookupTemplate is not null &&
            (PrimaryTemplate is null ||
             string.Equals(LookupTemplate.Id, PrimaryTemplate.Id, StringComparison.OrdinalIgnoreCase) ||
             !LookupTemplateChoices.Any(t => string.Equals(t.Id, LookupTemplate.Id, StringComparison.OrdinalIgnoreCase))))
        {
            LookupTemplate = LookupTemplateChoices.FirstOrDefault();
        }
    }

    private void BuildColumnLists()
    {
        PrimaryColumns.Clear();
        LookupColumns.Clear();
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;

        if (!CanCreateTemplateLinks || PrimaryTemplate is null || LookupTemplate is null)
        {
            return;
        }

        var sharedNames = GetSharedColumnNameSet().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (sharedNames.Count == 0)
        {
            ErrorMessage = "These templates have no columns with the same name. Add matching column labels on both templates to link them.";
        }

        foreach (var label in sharedNames)
        {
            var item = new RelationColumnPickItem(label, isPrimary: true);
            item.PropertyChanged += OnPrimaryColumnChanged;
            PrimaryColumns.Add(item);

            LookupColumns.Add(new RelationColumnPickItem(label, isPrimary: false)
            {
                IsDisabled = true,
                IsAutoMatchKey = true,
                IsChecked = false,
            });
        }

        if (LoadedRelation is null && SharedColumnCount > 0)
        {
            SelectSharedMatchColumns();
        }

        OnPropertyChanged(nameof(SelectedMatchColumnHint));
        OnPropertyChanged(nameof(SharedColumnCount));
        OnPropertyChanged(nameof(HasSharedColumns));
        OnPropertyChanged(nameof(SharedColumnSummary));
        OnPropertyChanged(nameof(HasNoPrimaryColumns));
        OnPropertyChanged(nameof(HasNoLookupColumns));
        OnPropertyChanged(nameof(CanSaveLink));
        SaveCommand.NotifyCanExecuteChanged();
    }

    private HashSet<string> GetSharedColumnNameSet()
    {
        if (PrimaryTemplate is null || LookupTemplate is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return WorksheetRelationUiText
            .GetSharedColumnNames(
                PrimaryTemplate.Fields.Select(f => f.Label),
                LookupTemplate.Fields.Select(f => f.Label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private void OnPrimaryColumnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(RelationColumnPickItem.IsChecked))
        {
            return;
        }

        SyncLookupWithPrimary();
        OnPropertyChanged(nameof(SelectedMatchColumnHint));
        OnPropertyChanged(nameof(CanSaveLink));
        SaveCommand.NotifyCanExecuteChanged();
    }

    public string SelectedMatchColumnHint =>
        WorksheetRelationUiText.GetMatchColumnHint(
            PrimaryColumns.Where(x => x.IsChecked).Select(x => x.ColumnName).ToList());

    private void SyncLookupWithPrimary()
    {
        var selectedKeys = new HashSet<string>(
            PrimaryColumns.Where(x => x.IsChecked).Select(x => x.ColumnName),
            StringComparer.OrdinalIgnoreCase);

        foreach (var lookup in LookupColumns)
        {
            lookup.IsChecked = selectedKeys.Contains(lookup.ColumnName);
            lookup.IsAutoMatchKey = true;
            lookup.IsDisabled = true;
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

        var existing = relations.FirstOrDefault(r =>
            (string.Equals(r.PrimaryTemplateId, PrimaryTemplate.Id, StringComparison.OrdinalIgnoreCase)
             && string.Equals(r.LookupTemplateId, LookupTemplate.Id, StringComparison.OrdinalIgnoreCase))
            || (string.Equals(r.PrimaryTemplateId, LookupTemplate.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.LookupTemplateId, PrimaryTemplate.Id, StringComparison.OrdinalIgnoreCase)));
        if (existing is null)
        {
            return;
        }

        LoadedRelation = existing;
        SelectedRelationId = existing.Id;

        var sharedNames = GetSharedColumnNameSet();

        var matchKeySet = new HashSet<string>(
            existing.MatchKeys.Select(k => k.SourceColumn).Where(k => !string.IsNullOrWhiteSpace(k)),
            StringComparer.OrdinalIgnoreCase);
        if (matchKeySet.Count == 0 && !string.IsNullOrWhiteSpace(existing.TriggerColumn))
        {
            matchKeySet.Add(existing.TriggerColumn);
        }

        foreach (var p in PrimaryColumns)
        {
            p.IsChecked = matchKeySet.Contains(p.ColumnName) && sharedNames.Contains(p.ColumnName);
        }

        if (!PrimaryColumns.Any(x => x.IsChecked) && sharedNames.Count > 0)
        {
            SelectSharedMatchColumns();
        }
        else
        {
            SyncLookupWithPrimary();
            OnPropertyChanged(nameof(SelectedMatchColumnHint));
        }

        OnPropertyChanged(nameof(CanSaveLink));
        SaveCommand.NotifyCanExecuteChanged();
    }

    // ── NEW: private helper — load all relations + build chip items ──────────
    private async Task LoadAllRelationsAsync(CancellationToken ct = default)
    {
        var (ok, _, relations) = await _api.GetRelationsAsync(ct).ConfigureAwait(true);
        if (!ok) return;

        AllRelations.Clear();
        RelationChips.Clear();

        var dedupeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in relations)
        {
            AllRelations.Add(r);

            var primary = Templates.FirstOrDefault(t => t.Id == r.PrimaryTemplateId);
            var lookup = Templates.FirstOrDefault(t => t.Id == r.LookupTemplateId);
            if (primary is null || lookup is null)
            {
                continue;
            }

            if (!TemplateCatalogFilters.HasSharedColumnLabels(primary, lookup))
            {
                continue;
            }

            var pairKey = string.Join(
                "|",
                new[] { r.PrimaryTemplateId, r.LookupTemplateId }.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            if (!dedupeKeys.Add(pairKey))
            {
                continue;
            }

            RelationChips.Add(new RelationChipItem(
                r.Id,
                r.PrimaryTemplateId,
                r.LookupTemplateId,
                primary.Name,
                lookup.Name));
        }

        OnPropertyChanged(nameof(RelationCount));
        OnPropertyChanged(nameof(HasRelations));
    }
    // ────────────────────────────────────────────────────────────────────────
}

public sealed class LinkedTemplateBrowseItem
{
    public required string RelationId { get; init; }
    public required string TemplateId { get; init; }
    public required string TemplateName { get; init; }
    public WorksheetRelationDto Relation { get; set; } = null!;
    public PartTemplateDefinition? LookupTemplate { get; init; }
}

public sealed class LinkedTemplatePreviewRow(IReadOnlyList<PreviewColumnCell> cells)
{
    public IReadOnlyList<PreviewColumnCell> Cells { get; } = cells;
}

public sealed class PreviewColumnCell(string header, string value)
{
    public string Header { get; } = header;
    public string Value { get; } = value;
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
    public string Label { get; } = $"{primaryName}  ↔  {lookupName}";
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
