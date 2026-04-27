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
    private WorksheetRelationDto? _loadedRelation;

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
                MatchKeys = selectedKeys,
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
            StatusMessage = "Worksheet relation saved.";
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
        var keySet = new HashSet<string>(existing.MatchKeys, StringComparer.OrdinalIgnoreCase);
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
}

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
