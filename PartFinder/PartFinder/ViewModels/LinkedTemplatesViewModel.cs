using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class LinkedTemplatesViewModel : ViewModelBase
{
    private readonly ITemplateSchemaService _templates;
    private readonly BackendApiClient _api;
    private readonly ICurrentUserAccessService _access;
    private readonly IMasterDataRecordsService _records;

    public LinkedTemplatesViewModel(
        ITemplateSchemaService templates,
        BackendApiClient api,
        ICurrentUserAccessService access,
        IMasterDataRecordsService records)
    {
        _templates = templates;
        _api = api;
        _access = access;
        _records = records;
    }

    public ObservableCollection<PartTemplateDefinition> Templates { get; } = [];
    public ObservableCollection<LinkedTemplateBrowseItem> LinkedTemplatesForBrowse { get; } = [];
    public ObservableCollection<RelationColumnPickItem> PreviewColumnPicks { get; } = [];
    public ObservableCollection<LinkedTemplatePreviewRow> PreviewRows { get; } = [];

    private readonly List<WorksheetRelationDto> _allRelations = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasBrowseSourceTemplate),
        nameof(BrowseSourceTemplateName),
        nameof(ShowNoLinksForBrowseMessage))]
    private PartTemplateDefinition? _browseSourceTemplate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(HasSelectedLinkedTemplate),
        nameof(SelectedLinkedTemplateName),
        nameof(ShowColumnEditor),
        nameof(ShowColumnEditorPlaceholder))]
    private LinkedTemplateBrowseItem? _selectedLinkedBrowseItem;

    [ObservableProperty]
    private string _previewTitle = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingPreview;

    [ObservableProperty]
    private bool _isSavingColumns;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool HasBrowseSourceTemplate => BrowseSourceTemplate is not null;
    public string BrowseSourceTemplateName => BrowseSourceTemplate?.Name ?? string.Empty;
    public bool HasLinkedTemplatesForBrowse => LinkedTemplatesForBrowse.Count > 0;
    public bool ShowNoLinksForBrowseMessage => HasBrowseSourceTemplate && !HasLinkedTemplatesForBrowse;
    public bool HasSelectedLinkedTemplate => SelectedLinkedBrowseItem is not null;
    public string SelectedLinkedTemplateName => SelectedLinkedBrowseItem?.TemplateName ?? string.Empty;
    public bool ShowColumnEditor => HasSelectedLinkedTemplate && PreviewColumnPicks.Count > 0;
    public bool ShowColumnEditorPlaceholder => HasBrowseSourceTemplate && !HasSelectedLinkedTemplate;
    public bool HasPreviewData => PreviewRows.Count > 0;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var list = await _templates.GetTemplatesAsync(ct).ConfigureAwait(true);
            var filtered = _access.FilterTemplatesForParts(list);
            Templates.Clear();
            foreach (var item in filtered)
            {
                Templates.Add(item);
            }

            await LoadAllRelationsAsync(ct).ConfigureAwait(true);
            BrowseSourceTemplate ??= Templates.FirstOrDefault();
            RefreshLinkedTemplatesForBrowse();
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

    partial void OnBrowseSourceTemplateChanged(PartTemplateDefinition? value)
    {
        RefreshLinkedTemplatesForBrowse();
    }

    partial void OnIsSavingColumnsChanged(bool value)
    {
        SaveViewColumnsCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsLoadingPreviewChanged(bool value)
    {
        RefreshPreviewCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLinkedBrowseItemChanged(LinkedTemplateBrowseItem? value)
    {
        StatusMessage = string.Empty;
        ErrorMessage = string.Empty;
        PreviewRows.Clear();
        PreviewTitle = string.Empty;
        OnPropertyChanged(nameof(HasPreviewData));

        if (value is null)
        {
            PreviewColumnPicks.Clear();
            OnPropertyChanged(nameof(ShowColumnEditor));
            return;
        }

        LoadColumnPicksForLinkedTemplate(value);
    }

    private async Task LoadAllRelationsAsync(CancellationToken ct = default)
    {
        _allRelations.Clear();
        var (ok, _, relations) = await _api.GetRelationsAsync(ct).ConfigureAwait(true);
        if (!ok)
        {
            return;
        }

        _allRelations.AddRange(relations);
    }

    private void RefreshLinkedTemplatesForBrowse()
    {
        LinkedTemplatesForBrowse.Clear();
        SelectedLinkedBrowseItem = null;
        PreviewColumnPicks.Clear();
        PreviewRows.Clear();
        PreviewTitle = string.Empty;
        OnPropertyChanged(nameof(HasLinkedTemplatesForBrowse));
        OnPropertyChanged(nameof(ShowNoLinksForBrowseMessage));
        OnPropertyChanged(nameof(HasPreviewData));
        OnPropertyChanged(nameof(ShowColumnEditor));

        if (BrowseSourceTemplate is null)
        {
            return;
        }

        var outbound = WorksheetRelationBidirectional.GetOutboundRelations(
            BrowseSourceTemplate.Id,
            _allRelations);
        foreach (var relation in outbound)
        {
            var lookup = Templates.FirstOrDefault(t => t.Id == relation.LookupTemplateId);
            var relationId = relation.Id.EndsWith(":mirror", StringComparison.OrdinalIgnoreCase)
                ? relation.Id[..^":mirror".Length]
                : relation.Id;
            LinkedTemplatesForBrowse.Add(new LinkedTemplateBrowseItem
            {
                RelationId = relationId,
                TemplateId = relation.LookupTemplateId,
                TemplateName = lookup?.Name ?? relation.LookupTemplateId,
                Relation = relation,
                LookupTemplate = lookup,
            });
        }

        OnPropertyChanged(nameof(HasLinkedTemplatesForBrowse));
        OnPropertyChanged(nameof(ShowNoLinksForBrowseMessage));
    }

    private void LoadColumnPicksForLinkedTemplate(LinkedTemplateBrowseItem item)
    {
        PreviewColumnPicks.Clear();

        var template = item.LookupTemplate
                         ?? Templates.FirstOrDefault(t => t.Id == item.TemplateId);
        if (template is null)
        {
            ErrorMessage = "Linked template columns could not be loaded.";
            OnPropertyChanged(nameof(ShowColumnEditor));
            return;
        }

        var displaySet = new HashSet<string>(item.Relation.DisplayColumns, StringComparer.OrdinalIgnoreCase);
        var matchKeys = new HashSet<string>(
            WorksheetRelationBidirectional.GetMatchKeyColumnLabels(item.Relation),
            StringComparer.OrdinalIgnoreCase);

        foreach (var field in template.Fields.OrderBy(f => f.DisplayOrder))
        {
            var isMatchKey = matchKeys.Contains(field.Label);
            var pick = new RelationColumnPickItem(field.Label, isPrimary: false)
            {
                IsChecked = displaySet.Contains(field.Label) || isMatchKey,
                IsDisabled = isMatchKey,
                IsAutoMatchKey = isMatchKey,
            };
            PreviewColumnPicks.Add(pick);
        }

        OnPropertyChanged(nameof(ShowColumnEditor));
    }

    [RelayCommand(CanExecute = nameof(CanSaveViewColumns))]
    private async Task SaveViewColumnsAsync(CancellationToken ct = default)
    {
        if (SelectedLinkedBrowseItem is null)
        {
            return;
        }

        var selectedColumns = GetSelectedViewColumnLabels();
        if (selectedColumns.Count == 0)
        {
            ErrorMessage = "Select at least one column to show when viewing linked data.";
            return;
        }

        var relation = SelectedLinkedBrowseItem.Relation;
        var request = new SaveWorksheetRelationRequest
        {
            PrimaryTemplateId = relation.PrimaryTemplateId,
            LookupTemplateId = relation.LookupTemplateId,
            Name = relation.Name,
            TriggerColumn = relation.TriggerColumn,
            MenuLabel = relation.MenuLabel,
            MatchKeys = relation.MatchKeys,
            DisplayColumns = selectedColumns,
        };

        IsSavingColumns = true;
        ErrorMessage = string.Empty;
        StatusMessage = string.Empty;
        try
        {
            var (ok, error, updated) = await _api.UpdateRelationAsync(relation.Id, request, ct).ConfigureAwait(true);
            if (!ok || updated is null)
            {
                ErrorMessage = error ?? "Could not save view columns.";
                return;
            }

            relation.DisplayColumns = updated.DisplayColumns;
            var index = _allRelations.FindIndex(r => string.Equals(r.Id, relation.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                _allRelations[index] = updated;
                SelectedLinkedBrowseItem.Relation = updated;
            }

            StatusMessage = "View columns saved.";
            await LoadLinkedPreviewDataAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            IsSavingColumns = false;
        }
    }

    private bool CanSaveViewColumns() => HasSelectedLinkedTemplate && !IsSavingColumns;

    [RelayCommand(CanExecute = nameof(CanRefreshPreview))]
    private Task RefreshPreviewAsync(CancellationToken ct = default) => LoadLinkedPreviewDataAsync(ct);

    private bool CanRefreshPreview() => HasSelectedLinkedTemplate && !IsLoadingPreview;

    public async Task LoadLinkedPreviewDataAsync(CancellationToken ct = default)
    {
        if (SelectedLinkedBrowseItem is null)
        {
            return;
        }

        var template = SelectedLinkedBrowseItem.LookupTemplate
                       ?? Templates.FirstOrDefault(t => t.Id == SelectedLinkedBrowseItem.TemplateId);
        if (template is null)
        {
            ErrorMessage = "Linked template not found.";
            return;
        }

        var selectedColumns = GetSelectedViewColumnLabels();
        if (selectedColumns.Count == 0)
        {
            ErrorMessage = "Select at least one column to preview.";
            return;
        }

        IsLoadingPreview = true;
        ErrorMessage = string.Empty;
        PreviewRows.Clear();
        PreviewTitle = $"{BrowseSourceTemplate?.Name} → {template.Name}";
        OnPropertyChanged(nameof(HasPreviewData));

        try
        {
            var rows = await _records.GetRowsAsync(template.Id, ct).ConfigureAwait(true);
            foreach (var row in rows.OrderBy(r => r.RowOrder))
            {
                var cells = selectedColumns
                    .Select(col => new PreviewColumnCell(col, GetRowCellByLabel(row, template, col)))
                    .ToList();
                PreviewRows.Add(new LinkedTemplatePreviewRow(cells));
            }

            if (PreviewRows.Count == 0)
            {
                StatusMessage = "No rows in this template yet.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingPreview = false;
            OnPropertyChanged(nameof(HasPreviewData));
            RefreshPreviewCommand.NotifyCanExecuteChanged();
            SaveViewColumnsCommand.NotifyCanExecuteChanged();
        }
    }

    private List<string> GetSelectedViewColumnLabels() =>
        PreviewColumnPicks
            .Where(c => c.IsChecked)
            .Select(c => c.ColumnName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string GetRowCellByLabel(MasterDataRowRecord row, PartTemplateDefinition template, string columnLabel)
    {
        var field = template.Fields.FirstOrDefault(
            f => string.Equals(f.Label, columnLabel, StringComparison.OrdinalIgnoreCase));
        var key = field?.Key ?? columnLabel;
        return row.Values.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }
}
