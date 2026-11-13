using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
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
    private CancellationTokenSource? _catalogAutoSaveCts;
    private int _catalogAutoSaveVersion;
    private bool _catalogAutoSaveRunning;
    private bool _catalogImportInProgress;

    private readonly ICurrentUserAccessService _access;
    private readonly CatalogBrowseSessionStore _catalogSession;
    private readonly IFavouriteStore _favouriteStore;
    private readonly WorksheetRelationCleanupService _relationCleanup;
    private readonly MasterDataCatalogCascadeService _catalogCascade;
    private readonly IShellNavCoordinator _shellNav;
    private readonly DispatcherQueue? _dispatcherQueue;

    public event EventHandler? ColumnFiltersChanged;

    public MasterDataViewModel(
        ITemplateSchemaService templateSchema,
        IMasterDataRecordsService records,
        IContextActionsService contextActions,
        BackendApiClient api,
        ActivityLogger activityLogger,
        ICurrentUserAccessService access,
        CatalogBrowseSessionStore catalogSession,
        IFavouriteStore favouriteStore,
        WorksheetRelationCleanupService relationCleanup,
        MasterDataCatalogCascadeService catalogCascade,
        IShellNavCoordinator shellNav)
    {
        _templateSchema = templateSchema;
        _records = records;
        _contextActions = contextActions;
        _api = api;
        _activityLogger = activityLogger;
        _access = access;
        _catalogSession = catalogSession;
        _favouriteStore = favouriteStore;
        _relationCleanup = relationCleanup;
        _catalogCascade = catalogCascade;
        _shellNav = shellNav;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _favouriteStore.FavouritesChanged += (_, _) => BumpFavouritesStoreVersion();
    }

    public int FavouritesStoreVersion { get; private set; }

    private void BumpFavouritesStoreVersion()
    {
        if (_dispatcherQueue is not null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(BumpFavouritesStoreVersion);
            return;
        }

        FavouritesStoreVersion++;
        OnPropertyChanged(nameof(FavouritesStoreVersion));
    }

    public bool IsFavouriteFor(string templateId) => _favouriteStore.IsFavourite(templateId);

    public Task LoadFavouriteStoreAsync() => _favouriteStore.LoadAsync();

    public async Task ToggleFavouritePublicAsync(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        await _favouriteStore.ToggleAsync(templateId).ConfigureAwait(true);
        BumpFavouritesStoreVersion();
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
    [NotifyPropertyChangedFor(nameof(ShowEmptyMasterDataAddButton))]
    [NotifyPropertyChangedFor(nameof(ShowNoMasterDataCatalogsInList))]
    private bool showNoTemplateHint;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyMasterDataAddButton))]
    [NotifyPropertyChangedFor(nameof(ShowNoMasterDataCatalogsInList))]
    private bool showNoDatabaseHint;

    [ObservableProperty]
    private bool showTemplatePicker;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCatalogCanvasChrome))]
    [NotifyPropertyChangedFor(nameof(ShowCatalogCanvasToolbar))]
    [NotifyPropertyChangedFor(nameof(ShowImportExportToolbar))]
    [NotifyPropertyChangedFor(nameof(UseExcelStyleCatalogGrid))]
    private bool isCatalogCanvasMode;

    [ObservableProperty]
    private bool isAddColumnPanelVisible;

    private const int CanvasMinColumns = 12;
    private const int CanvasMinRows = 40;
    private const int CanvasTrailingRows = 25;
    private const int CanvasGrowBatch = 20;
    private const int LargeImportRowThreshold = 400;

    /// <summary>Skip building the on-screen canvas when the file has very many columns.</summary>
    private const int LargeImportColumnThreshold = 80;
    private const int SaveBatchSize = 250;

    [ObservableProperty]
    private bool _showOperationProgress;

    [ObservableProperty]
    private double _operationProgressPercent;

    [ObservableProperty]
    private string _operationProgressHeadline = string.Empty;

    [ObservableProperty]
    private string _operationProgressDetail = string.Empty;

    [ObservableProperty]
    private string _operationProgressPercentLabel = string.Empty;

    [ObservableProperty]
    private MasterDataSurfaceMode surfaceMode = MasterDataSurfaceMode.ReadOnlyCatalog;

    /// <summary>Worksheet relation hyperlinks (Linked Templates browse only).</summary>
    public bool EnableWorksheetHyperlinks => SurfaceMode == MasterDataSurfaceMode.LinkedBrowse;

    public bool IsCatalogReadOnly => SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog;

    private int _catalogTotalRowCount;

    /// <summary>Total rows in the catalog when browsing read-only with server paging.</summary>
    public int CatalogTotalRowCount
    {
        get => _catalogTotalRowCount;
        set => SetProperty(ref _catalogTotalRowCount, value);
    }

    public int GetPagerTotalRowCount(int visibleRowCount) =>
        IsCatalogReadOnly && CatalogTotalRowCount > 0
            ? CatalogTotalRowCount
            : visibleRowCount;

    public bool ShowSpreadsheetEditChrome => SurfaceMode == MasterDataSurfaceMode.TemplateEdit;

    public bool ShowCatalogCanvasChrome => IsCatalogCanvasMode;

    public bool ShowCatalogCanvasToolbar => IsCatalogCanvasMode;

    public bool UseExcelStyleCatalogGrid => IsCatalogCanvasMode && IsEditMode;

    public bool ShowCatalogPager => !IsCatalogCanvasMode;

    public bool ShowImportExportToolbar =>
        SelectedDataTemplate is not null
        && CanViewMasterData
        && (IsCatalogCanvasMode
            || SurfaceMode is MasterDataSurfaceMode.ReadOnlyCatalog or MasterDataSurfaceMode.LinkedBrowse);

    public bool IsEmbeddedLinkedBrowse => SurfaceMode == MasterDataSurfaceMode.LinkedBrowse;

    public bool ShowTemplatePickerInChrome =>
        !IsEmbeddedLinkedBrowse
        && (SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog
            ? DataTemplates.Count > 0
            : ShowTemplatePicker);

    public string EditModeButtonText => IsEditMode ? "Stop Editing" : "Edit Data";

    public bool CanEditMasterData => _access.Capabilities.CanEditMasterData;
    public bool CanAddMasterData => _access.Capabilities.CanAddMasterData;

    public bool CanImportMasterData =>
        CanAddMasterData || CanEditCatalogCanvas || _access.IsOrganizationAdministrator;
    public bool CanCopyMasterData => _access.Capabilities.CanCopyMasterData;
    public bool CanViewMasterData => _access.Capabilities.CanViewMasterData;
    public bool CanDeleteMasterData => _access.Capabilities.CanDeleteMasterData;

    /// <summary>Org admins may purge all rows for the selected catalog template from the browse toolbar.</summary>
    public bool CanPurgeMasterDataForTemplate =>
        _access.IsOrganizationAdministrator
        && ShowImportExportToolbar
        && SelectedDataTemplate is not null;

    public bool CanAddMasterDataCatalog =>
        !IsEmbeddedLinkedBrowse
        && SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog
        && _access.Capabilities.CanAddTemplate;

    public bool ShowNoMasterDataCatalogsInList => DataTemplates.Count == 0;

    public bool ShowEmptyMasterDataAddButton =>
        ShowNoMasterDataCatalogsInList && CanAddMasterDataCatalog && !ShowNoDatabaseHint;

    public bool ShowMasterDataReadOnlyHint =>
        IsCatalogReadOnly && SelectedDataTemplate is null;

    /// <summary>Master Data catalog pages save in the background (no Save button).</summary>
    public bool ShowMasterDataManualSaveButtons =>
        SurfaceMode != MasterDataSurfaceMode.ReadOnlyCatalog;

    public bool ShowSpreadsheetManualSaveButton =>
        ShowSpreadsheetEditChrome && ShowMasterDataManualSaveButtons;

    public bool AutoSaveMasterDataCatalog =>
        SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog
        && IsEditMode
        && SelectedDataTemplate is not null
        && Columns.Count > 0
        && (CanEditMasterData || CanAddMasterData);

    public bool ShowCatalogManagementChrome => CanAddMasterDataCatalog || CanDeleteMasterDataCatalogItems;

    public bool CanDeleteMasterDataCatalogItems => _access.IsOrganizationAdministrator;

    public bool CanDeleteCatalogTemplate(PartTemplateDefinition template) =>
        CanDeleteMasterDataCatalogItems
        && !string.Equals(
            template.Id,
            MongoTemplateSchemaService.MasterDataTemplateId,
            StringComparison.OrdinalIgnoreCase);

    public string? LastCreatedCatalogId { get; private set; }

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

    public bool CanEditCatalogCanvas =>
        IsCatalogCanvasMode
        && (_access.Capabilities.CanAddTemplate || _access.IsOrganizationAdministrator);

    public bool CanAddRows =>
        IsEditMode
        && Columns.Count > 0
        && (CanEditCatalogCanvas || CanEditMasterData || CanAddMasterData);

    partial void OnIsEditModeChanged(bool value)
    {
        OnPropertyChanged(nameof(AutoSaveMasterDataCatalog));
        OnPropertyChanged(nameof(EditModeButtonText));
        OnPropertyChanged(nameof(UseExcelStyleCatalogGrid));
        OnPropertyChanged(nameof(CanAddRows));
        OnPropertyChanged(nameof(CanDeleteRows));
        OnPropertyChanged(nameof(CanManageColumns));
        OnPropertyChanged(nameof(CanRemoveColumns));
        AddRowCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsCatalogCanvasModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowCatalogCanvasChrome));
        OnPropertyChanged(nameof(ShowCatalogCanvasToolbar));
        OnPropertyChanged(nameof(ShowImportExportToolbar));
        OnPropertyChanged(nameof(ShowCatalogPager));
        OnPropertyChanged(nameof(UseExcelStyleCatalogGrid));
        OnPropertyChanged(nameof(CanEditCatalogCanvas));
        OnPropertyChanged(nameof(CanAddRows));
        OnPropertyChanged(nameof(CanDeleteRows));
        OnPropertyChanged(nameof(CanManageColumns));
        OnPropertyChanged(nameof(CanRemoveColumns));
    }

    partial void OnSurfaceModeChanged(MasterDataSurfaceMode value)
    {
        if (value is MasterDataSurfaceMode.ReadOnlyCatalog or MasterDataSurfaceMode.LinkedBrowse
            && !IsCatalogCanvasMode)
        {
            IsEditMode = false;
        }

        OnPropertyChanged(nameof(EnableWorksheetHyperlinks));
        OnPropertyChanged(nameof(IsCatalogReadOnly));
        OnPropertyChanged(nameof(IsEmbeddedLinkedBrowse));
        OnPropertyChanged(nameof(ShowImportExportToolbar));
        OnPropertyChanged(nameof(ShowSpreadsheetEditChrome));
        OnPropertyChanged(nameof(ShowTemplatePickerInChrome));
        OnPropertyChanged(nameof(CanAddRows));
        OnPropertyChanged(nameof(CanManageColumns));
        OnPropertyChanged(nameof(CanRemoveColumns));
        OnPropertyChanged(nameof(CanAddMasterDataCatalog));
        OnPropertyChanged(nameof(ShowCatalogManagementChrome));
        OnPropertyChanged(nameof(CanDeleteMasterDataCatalogItems));
    }

    partial void OnSelectedDataTemplateChanged(PartTemplateDefinition? value)
    {
        OnPropertyChanged(nameof(ShowMasterDataReadOnlyHint));
        if (_suppressTemplateSelectionChange || value is null)
        {
            return;
        }

        _catalogSession.GetGridStateFor(SurfaceMode).TemplateId = value.Id;
        if (SurfaceMode == MasterDataSurfaceMode.LinkedBrowse)
        {
            _catalogSession.LinkedTemplatesBrowseTemplateId = value.Id;
        }

        OnPropertyChanged(nameof(ShowTemplatePickerInChrome));
        OnPropertyChanged(nameof(ShowImportExportToolbar));
        OnPropertyChanged(nameof(CanPurgeMasterDataForTemplate));
        _ = LoadForTemplateAsync(value.Id);
    }

    public async Task InitializeSurfaceAsync(
        MasterDataNavigationArgs? args = null,
        CancellationToken cancellationToken = default)
    {
        args ??= new MasterDataNavigationArgs();
        SurfaceMode = args.Mode;
        if (SurfaceMode is MasterDataSurfaceMode.ReadOnlyCatalog or MasterDataSurfaceMode.LinkedBrowse)
        {
            IsEditMode = false;
        }

        switch (args.Mode)
        {
            case MasterDataSurfaceMode.TemplateEdit:
                if (string.IsNullOrWhiteSpace(args.TemplateId))
                {
                    StatusMessage = "No template selected for editing.";
                    return;
                }

                await LoadForTemplateEditAsync(args.TemplateId, args.StartInEditMode, cancellationToken).ConfigureAwait(true);
                return;
            case MasterDataSurfaceMode.LinkedBrowse:
                await InitializeLinkedBrowseSurfaceAsync(cancellationToken).ConfigureAwait(true);
                return;
            default:
                var deferDefaultGrid = !string.IsNullOrWhiteSpace(args.TemplateId);
                await LoadCatalogAsync(cancellationToken, loadInitialGrid: !deferDefaultGrid).ConfigureAwait(true);
                if (!string.IsNullOrWhiteSpace(args.TemplateId))
                {
                    await SelectCatalogByIdAsync(args.TemplateId, cancellationToken).ConfigureAwait(true);
                }

                return;
        }
    }

    public async Task SelectCatalogByIdAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var pick = DataTemplates.FirstOrDefault(t => t.Id == templateId)
                   ?? _cachedTemplates.FirstOrDefault(t => t.Id == templateId);
        if (pick is null)
        {
            return;
        }

        _suppressTemplateSelectionChange = true;
        SelectedDataTemplate = pick;
        _suppressTemplateSelectionChange = false;
        _catalogSession.GetGridStateFor(SurfaceMode).TemplateId = templateId;
        await LoadForTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
    }

    private async Task LoadCatalogRowsPageCoreAsync(
        int pageIndex,
        int pageSize,
        string templateId,
        CancellationToken loadCt)
    {
        var skip = pageIndex * pageSize;
        var (pageRows, total) = await _records
            .GetRowsPageAsync(templateId, skip, pageSize, loadCt)
            .ConfigureAwait(true);

        if (!IsLoadStillCurrent(templateId))
        {
            return;
        }

        CatalogTotalRowCount = total;
        Rows.Clear();
        foreach (var rec in pageRows)
        {
            Rows.Add(MapRecordToRow(rec));
        }

        if (Rows.Count == 0)
        {
            Rows.Add(CreateEmptyRow());
        }

        await ResolveRecordLinkLabelsAsync(loadCt).ConfigureAwait(true);
    }

    public async Task LoadCatalogRowsPageAsync(int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        if (SelectedDataTemplate is null || pageSize <= 0)
        {
            return;
        }

        var templateId = SelectedDataTemplate.Id;
        var reuseActiveLoad = _templateLoadCts is not null
                              && cancellationToken != default
                              && cancellationToken == _templateLoadCts.Token;

        CancellationToken loadCt;
        if (reuseActiveLoad)
        {
            loadCt = _templateLoadCts!.Token;
        }
        else
        {
            _templateLoadCts?.Cancel();
            _templateLoadCts?.Dispose();
            _templateLoadCts = cancellationToken == default
                ? new CancellationTokenSource()
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            loadCt = _templateLoadCts.Token;
        }

        var showLoading = !reuseActiveLoad;
        if (showLoading)
        {
            IsLoading = true;
        }

        try
        {
            await LoadCatalogRowsPageCoreAsync(pageIndex, pageSize, templateId, loadCt).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (loadCt.IsCancellationRequested)
        {
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

    /// <summary>Initial load: all templates + optional first grid selection (catalog / linked browse).</summary>
    public async Task LoadCatalogAsync(CancellationToken cancellationToken = default, bool loadInitialGrid = true) =>
        await LoadAsync(cancellationToken, loadInitialGrid).ConfigureAwait(true);

    /// <summary>Embedded linked-templates grid — analytics template rows only (not master data catalogs).</summary>
    public async Task InitializeLinkedBrowseSurfaceAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = null;
        ShowNoTemplateHint = false;
        ShowNoDatabaseHint = false;
        DataTemplates.Clear();
        Columns.Clear();
        Rows.Clear();

        try
        {
            var templateId = _catalogSession.LinkedTemplatesBrowseTemplateId
                             ?? _catalogSession.LinkedTemplatesGrid.TemplateId;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                ShowNoTemplateHint = true;
                return;
            }

            await ApplyLinkedBrowseTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
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

    public async Task ApplyLinkedBrowseTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        SurfaceMode = MasterDataSurfaceMode.LinkedBrowse;
        IsEditMode = false;

        var template = await _templateSchema
            .GetTemplateAsync(templateId, cancellationToken)
            .ConfigureAwait(true);
        if (template is null || template.IsMasterDataCatalog)
        {
            ShowNoTemplateHint = true;
            StatusMessage = "Template not found.";
            Columns.Clear();
            Rows.Clear();
            SelectedDataTemplate = null;
            return;
        }

        ShowNoTemplateHint = false;
        _catalogSession.LinkedTemplatesBrowseTemplateId = templateId;
        _catalogSession.LinkedTemplatesGrid.TemplateId = templateId;

        _suppressTemplateSelectionChange = true;
        SelectedDataTemplate = template;
        _suppressTemplateSelectionChange = false;

        await LoadForTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
    }

    public async Task ClearLinkedBrowseSurfaceAsync(CancellationToken cancellationToken = default)
    {
        _templateLoadCts?.Cancel();
        Columns.Clear();
        Rows.Clear();
        SelectedDataTemplate = null;
        ShowNoTemplateHint = true;
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task LoadForTemplateEditAsync(
        string templateId,
        bool startInEditMode,
        CancellationToken cancellationToken = default)
    {
        SurfaceMode = MasterDataSurfaceMode.TemplateEdit;
        ShowTemplatePicker = false;
        IsEditMode = false;

        try
        {
            _cachedTemplates = await _templateSchema
                .GetTemplatesAsync(cancellationToken)
                .ConfigureAwait(true);
            DataTemplates.Clear();
            foreach (var t in _cachedTemplates.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                DataTemplates.Add(t);
            }
        }
        catch
        {
            // Link pickers may fall back to schema refresh later.
        }

        var pick = _cachedTemplates.FirstOrDefault(t => t.Id == templateId)
                   ?? DataTemplates.FirstOrDefault(t => t.Id == templateId);
        if (pick is null)
        {
            StatusMessage = "Template not found.";
            return;
        }

        _suppressTemplateSelectionChange = true;
        SelectedDataTemplate = pick;
        _suppressTemplateSelectionChange = false;

        await LoadForTemplateAsync(
            templateId,
            cancellationToken,
            restoreEditModeAfterLoad: startInEditMode && CanEditMasterData).ConfigureAwait(true);

        if (startInEditMode && CanEditMasterData)
        {
            IsEditMode = true;
        }
    }

    /// <summary>Initial load: templates list + optional default grid.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default, bool loadInitialGrid = true)
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
            _cachedTemplates = TemplateCatalogFilters.MasterDataCatalogs(
                await _templateSchema.GetTemplatesAsync(cancellationToken).ConfigureAwait(true));

            if (_cachedTemplates.Count == 0)
            {
                ShowNoTemplateHint = true;
                NotifyMasterDataCatalogListChrome();
                return;
            }

            var ordered = CatalogBrowseSessionStore.OrderForCatalogSidebar(_cachedTemplates);
            foreach (var t in ordered)
            {
                DataTemplates.Add(t);
            }

            ShowTemplatePicker = DataTemplates.Count > 0;
            NotifyMasterDataCatalogListChrome();

            if (!loadInitialGrid)
            {
                OnPropertyChanged(nameof(ShowTemplatePickerInChrome));
                return;
            }

            var savedId = _catalogSession.GetGridStateFor(SurfaceMode).TemplateId;
            var pick = CatalogBrowseSessionStore.ResolveInitialTemplate(ordered, savedId);
            if (pick is null)
            {
                return;
            }

            _suppressTemplateSelectionChange = true;
            SelectedDataTemplate = pick;
            _suppressTemplateSelectionChange = false;

            _catalogSession.GetGridStateFor(SurfaceMode).TemplateId = pick.Id;
            if (SurfaceMode == MasterDataSurfaceMode.LinkedBrowse)
            {
                _catalogSession.LinkedTemplatesBrowseTemplateId = pick.Id;
            }

            await LoadForTemplateAsync(pick.Id, cancellationToken).ConfigureAwait(true);
            OnPropertyChanged(nameof(ShowTemplatePickerInChrome));
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
        var preserveCanvasEdit = IsCatalogCanvasMode
            && string.Equals(templateId, SelectedDataTemplate?.Id, StringComparison.OrdinalIgnoreCase);

        IsLoading = true;
        StatusMessage = null;
        ShowNoTemplateHint = false;
        if (!(preserveEditMode && SurfaceMode == MasterDataSurfaceMode.TemplateEdit) && !preserveCanvasEdit)
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
            if (!IsCatalogReadOnly
                && IsCatalogBrowseSurface()
                && _catalogSession.TryGetSheetCache(SurfaceMode, loadForId, out var cached))
            {
                ApplySheetCache(cached);
                return;
            }

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

            if (template is null)
            {
                ShowNoTemplateHint = true;
                return;
            }

            if (template.Fields.Count == 0)
            {
                if (IsCatalogCanvasMode && string.Equals(template.Id, loadForId, StringComparison.Ordinal))
                {
                    InitializeCatalogCanvasGrid();
                    ShowNoTemplateHint = false;
                    return;
                }

                if (SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog && template.IsMasterDataCatalog)
                {
                    _suppressTemplateSelectionChange = true;
                    SelectedDataTemplate = template;
                    _suppressTemplateSelectionChange = false;
                    IsCatalogCanvasMode = true;
                    IsEditMode = true;
                    InitializeCatalogCanvasGrid();
                    ShowNoTemplateHint = false;
                    return;
                }

                ShowNoTemplateHint = true;
                return;
            }

            ShowNoTemplateHint = false;

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
                    WorksheetRelationBidirectional.GetOutboundRelations(template.Id, relations));
            }

            loadCt.ThrowIfCancellationRequested();
            if (!IsLoadStillCurrent(loadForId))
            {
                return;
            }

            if (IsCatalogReadOnly)
            {
                await LoadCatalogRowsPageAsync(0, 50, loadCt).ConfigureAwait(true);
            }
            else
            {
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
            }

            loadCt.ThrowIfCancellationRequested();
            if (!IsLoadStillCurrent(loadForId))
            {
                return;
            }

            if (!IsCatalogReadOnly)
            {
                await ResolveRecordLinkLabelsAsync(loadCt).ConfigureAwait(true);
            }

            if (IsCatalogBrowseSurface() && !IsCatalogReadOnly)
            {
                SaveSheetCache(loadForId);
            }
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
                if (preserveEditMode
                    || (SurfaceMode == MasterDataSurfaceMode.TemplateEdit && restoreEditModeAfterLoad))
                {
                    IsEditMode = true;
                }

                OnPropertyChanged(nameof(CanAddRows));
                OnPropertyChanged(nameof(CanDeleteRows));
                OnPropertyChanged(nameof(CanManageColumns));
                OnPropertyChanged(nameof(CanRemoveColumns));
                AddRowCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private bool IsLoadStillCurrent(string templateId) =>
        string.Equals(SelectedDataTemplate?.Id, templateId, StringComparison.Ordinal);

    private bool IsCatalogBrowseSurface() =>
        SurfaceMode is MasterDataSurfaceMode.ReadOnlyCatalog or MasterDataSurfaceMode.LinkedBrowse;

    private void ApplySheetCache(MasterDataSheetCacheEntry cached)
    {
        Columns.Clear();
        foreach (var f in cached.Fields.OrderBy(x => x.DisplayOrder))
        {
            Columns.Add(f);
        }

        Rows.Clear();
        foreach (var row in cached.Rows)
        {
            var rowVm = new MasterDataRowViewModel { RowId = row.RowId };
            foreach (var cell in row.Cells)
            {
                rowVm.Cells.Add(new MasterDataCellViewModel
                {
                    FieldKey = cell.FieldKey,
                    FieldType = cell.FieldType,
                    LinkedTemplateId = cell.LinkedTemplateId,
                    LinkedDisplayFieldKey = cell.LinkedDisplayFieldKey,
                    Text = cell.Text,
                    LinkedRowId = cell.LinkedRowId,
                });
            }

            Rows.Add(rowVm);
        }

        _loadedContextActions = cached.ContextActions.ToList();
        _worksheetRelations.Clear();
        _worksheetRelations.AddRange(cached.WorksheetRelations);
    }

    private void SaveSheetCache(string templateId)
    {
        var fields = Columns.ToList();
        var rows = Rows.Select(row => new MasterDataRowCacheRow
        {
            RowId = row.RowId,
            Cells = row.Cells.Select(cell => new MasterDataCellCacheCell
            {
                FieldKey = cell.FieldKey,
                FieldType = cell.FieldType,
                LinkedTemplateId = cell.LinkedTemplateId,
                LinkedDisplayFieldKey = cell.LinkedDisplayFieldKey,
                Text = cell.Text,
                LinkedRowId = cell.LinkedRowId,
            }).ToList(),
        }).ToList();

        _catalogSession.SetSheetCache(
            SurfaceMode,
            templateId,
            new MasterDataSheetCacheEntry
            {
                TemplateId = templateId,
                SurfaceMode = SurfaceMode,
                Fields = fields,
                Rows = rows,
                ContextActions = _loadedContextActions.ToList(),
                WorksheetRelations = _worksheetRelations.ToList(),
            });
    }

    private void InvalidateCurrentSheetCache()
    {
        if (SelectedDataTemplate is null)
        {
            return;
        }

        _catalogSession.InvalidateSheetCache(SurfaceMode, SelectedDataTemplate.Id);
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

    public bool CanDeleteRows =>
        IsEditMode
        && SelectedDataTemplate is not null
        && (IsCatalogCanvasMode
            ? CanEditCatalogCanvas
            : ShowSpreadsheetEditChrome && CanDeleteMasterData);

    public async Task<bool> DeleteRowAsync(
        MasterDataRowViewModel row,
        CancellationToken cancellationToken = default)
    {
        if (!CanDeleteRows || SelectedDataTemplate is null)
        {
            StatusMessage = "You do not have permission to delete rows.";
            return false;
        }

        var templateId = SelectedDataTemplate.Id;
        Rows.Remove(row);
        if (!string.IsNullOrWhiteSpace(row.RowId))
        {
            try
            {
                await _records.DeleteRowAsync(templateId, row.RowId, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error deleting row: " + ex.Message;
                return false;
            }
        }

        StatusMessage = "Row removed.";
        if (IsCatalogBrowseSurface())
        {
            SaveSheetCache(templateId);
        }
        else
        {
            InvalidateCurrentSheetCache();
        }

        return true;
    }

    public async Task<string?> CreateMasterDataCatalogAsync(string name, CancellationToken cancellationToken = default)
    {
        LastCreatedCatalogId = null;
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Enter a catalog name.";
        }

        if (!CanAddMasterDataCatalog)
        {
            return "You do not have permission to add a catalog.";
        }

        if (_cachedTemplates.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return "A catalog with this name already exists.";
        }

        var templateId = Guid.NewGuid().ToString("N");
        var def = new PartTemplateDefinition
        {
            Id = templateId,
            Name = name,
            Version = 1,
            IsPublished = true,
            IsMasterDataCatalog = true,
            Fields = Array.Empty<TemplateFieldDefinition>(),
        };

        try
        {
            await _templateSchema.SaveTemplateAsync(def, cancellationToken).ConfigureAwait(true);
            LastCreatedCatalogId = templateId;
            _activityLogger.LogTemplateChange(
                "Master Data Catalog Created",
                $"Catalog \"{name}\" created — open canvas to define columns and data");
            await LoadAsync(cancellationToken, loadInitialGrid: false).ConfigureAwait(true);
            var created = DataTemplates.FirstOrDefault(t => t.Id == templateId)
                          ?? def;
            _suppressTemplateSelectionChange = true;
            SelectedDataTemplate = created;
            _suppressTemplateSelectionChange = false;
            _cachedTemplates = DataTemplates.ToList();
            IsCatalogCanvasMode = true;
            IsEditMode = true;
            ShowNoTemplateHint = false;
            InitializeCatalogCanvasGrid();
            StatusMessage = "Name your columns in the highlighted header row, enter data in the cells, or import — changes save automatically.";

            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }

    public async Task<(bool Ok, string? Error)> DeleteMasterDataCatalogAsync(
        PartTemplateDefinition template,
        CancellationToken cancellationToken = default)
    {
        if (!CanDeleteCatalogTemplate(template))
        {
            return (false, "You do not have permission to delete this catalog.");
        }

        try
        {
            await _relationCleanup
                .DeleteAllRelationsInvolvingTemplateAsync(template.Id, cancellationToken)
                .ConfigureAwait(true);
            _ = await _records
                .DeleteAllRowsForTemplateAsync(template.Id, cancellationToken)
                .ConfigureAwait(true);
            await _templateSchema.DeleteTemplateAsync(template.Id, cancellationToken).ConfigureAwait(true);
            var cascade = await _catalogCascade
                .OnMasterDataCatalogDeletedAsync(template.Id, cancellationToken)
                .ConfigureAwait(true);
            if (cascade.DeletedAllAnalyticsTemplates)
            {
                _activityLogger.LogTemplateChange(
                    "Analytics Templates Removed",
                    $"All {cascade.AnalyticsTemplatesDeleted} analytics template(s) removed because no master data catalogs remain.");
                await _shellNav.NotifyTemplatesChangedAsync().ConfigureAwait(true);
            }
            else if (cascade.ColumnsRemoved > 0)
            {
                await _shellNav.NotifyTemplatesChangedAsync().ConfigureAwait(true);
            }

            _activityLogger.LogTemplateChange(
                "Master Data Catalog Deleted",
                $"Catalog \"{template.Name}\" removed");

            var wasSelected = string.Equals(
                SelectedDataTemplate?.Id,
                template.Id,
                StringComparison.OrdinalIgnoreCase);
            var item = DataTemplates.FirstOrDefault(t => t.Id == template.Id);
            if (item is not null)
            {
                DataTemplates.Remove(item);
            }

            _cachedTemplates = DataTemplates.ToList();
            if (wasSelected)
            {
                SelectedDataTemplate = DataTemplates.FirstOrDefault();
                if (SelectedDataTemplate is not null)
                {
                    await LoadForTemplateAsync(SelectedDataTemplate.Id, cancellationToken).ConfigureAwait(true);
                }
                else
                {
                    Columns.Clear();
                    Rows.Clear();
                    ShowNoTemplateHint = DataTemplates.Count == 0;
                }
            }
            else if (DataTemplates.Count == 0)
            {
                ShowNoTemplateHint = true;
            }

            OnPropertyChanged(nameof(ShowTemplatePickerInChrome));
            OnPropertyChanged(nameof(ShowCatalogManagementChrome));
            NotifyMasterDataCatalogListChrome();
            var cascadeNote = cascade.DeletedAllAnalyticsTemplates
                ? $" Removed {cascade.AnalyticsTemplatesDeleted} analytics template(s) (no master data left)."
                : cascade.ColumnsRemoved > 0
                    ? $" Removed {cascade.ColumnsRemoved} linked column(s) from {cascade.AnalyticsTemplatesUpdated} analytics template(s)."
                    : string.Empty;
            StatusMessage = $"Catalog \"{template.Name}\" deleted.{cascadeNote}";
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool Ok, long DeletedCount, string? Error)> PurgeAllRowsForCurrentTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanPurgeMasterDataForTemplate || SelectedDataTemplate is null)
        {
            return (false, 0, "You do not have permission to delete all master data.");
        }

        var templateId = SelectedDataTemplate.Id;
        try
        {
            var deleted = await _records.DeleteAllRowsForTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(true);
            Rows.Clear();
            CatalogTotalRowCount = 0;
            InvalidateCurrentSheetCache();
            await LoadForTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
            StatusMessage = deleted > 0
                ? $"Removed {deleted} row(s) from \"{SelectedDataTemplate.Name}\"."
                : "No data rows were stored for this template.";
            return (true, deleted, null);
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
            return (false, 0, ex.Message);
        }
    }

    public void InitializeCatalogCanvasGrid()
    {
        Columns.Clear();
        Rows.Clear();
        for (var i = 0; i < CanvasMinColumns; i++)
        {
            Columns.Add(new TemplateFieldDefinition
            {
                Key = $"column_{i + 1}",
                Label = string.Empty,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = i,
            });
        }

        EnsureCanvasRowBuffer();
        ShowNoTemplateHint = false;
    }

    public void EnsureCanvasRowBuffer()
    {
        var target = Math.Max(CanvasMinRows, Rows.Count + CanvasTrailingRows);
        while (Rows.Count < target)
        {
            Rows.Add(CreateEmptyRow());
        }
    }

    public void MaybeGrowCanvas(int rowIndex, int columnIndex)
    {
        if (!IsCatalogCanvasMode || !IsEditMode)
        {
            return;
        }

        if (rowIndex >= Rows.Count - 3)
        {
            for (var i = 0; i < CanvasGrowBatch; i++)
            {
                Rows.Add(CreateEmptyRow());
            }
        }

        if (columnIndex >= Columns.Count - 1)
        {
            var number = Columns.Count + 1;
            Columns.Add(new TemplateFieldDefinition
            {
                Key = $"column_{number}",
                Label = string.Empty,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = Columns.Count,
            });
            ColumnFiltersChanged?.Invoke(this, EventArgs.Empty);
        }

        RequestCatalogAutoSave();
    }

    public void SetColumnLabelAt(int columnIndex, string? label)
    {
        if (columnIndex < 0 || columnIndex >= Columns.Count)
        {
            return;
        }

        var field = Columns[columnIndex];
        Columns[columnIndex] = new TemplateFieldDefinition
        {
            Key = field.Key,
            Label = label?.Trim() ?? string.Empty,
            Type = field.Type,
            IsRequired = field.IsRequired,
            DisplayOrder = field.DisplayOrder,
            ValidationPattern = field.ValidationPattern,
            Options = field.Options,
            LinkedTemplateId = field.LinkedTemplateId,
            LinkedDisplayFieldKey = field.LinkedDisplayFieldKey,
        };

        RequestCatalogAutoSave();
    }

    private void NotifyMasterDataCatalogListChrome()
    {
        OnPropertyChanged(nameof(ShowNoMasterDataCatalogsInList));
        OnPropertyChanged(nameof(ShowEmptyMasterDataAddButton));
    }

    public void RequestCatalogAutoSave()
    {
        if (_catalogImportInProgress || !AutoSaveMasterDataCatalog)
        {
            return;
        }

        _catalogAutoSaveCts?.Cancel();
        _catalogAutoSaveCts?.Dispose();
        _catalogAutoSaveCts = new CancellationTokenSource();
        var token = _catalogAutoSaveCts.Token;
        var version = Interlocked.Increment(ref _catalogAutoSaveVersion);
        _ = ScheduleCatalogAutoSaveDebouncedAsync(version, token);
    }

    private async Task ScheduleCatalogAutoSaveDebouncedAsync(int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(750, token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }

        if (version != _catalogAutoSaveVersion || token.IsCancellationRequested)
        {
            return;
        }

        await FlushCatalogAutoSaveAsync(token).ConfigureAwait(true);
    }

    public Task FlushCatalogAutoSaveAsync(CancellationToken cancellationToken = default)
    {
        if (!AutoSaveMasterDataCatalog || _catalogAutoSaveRunning)
        {
            return Task.CompletedTask;
        }

        return SaveGridCoreAsync(null, cancellationToken, autoSave: true);
    }

    public void ApplyOperationProgress(MasterDataOperationProgress progress)
    {
        ShowOperationProgress = true;
        OperationProgressPercent = Math.Clamp(progress.Percent, 0, 100);
        OperationProgressHeadline = progress.Headline;
        OperationProgressDetail = progress.Detail;
        OperationProgressPercentLabel = progress.PercentLabel;
    }

    public void ClearOperationProgress()
    {
        ShowOperationProgress = false;
        OperationProgressPercent = 0;
        OperationProgressHeadline = string.Empty;
        OperationProgressDetail = string.Empty;
        OperationProgressPercentLabel = string.Empty;
    }

    private static void ReportProgress(
        IProgress<MasterDataOperationProgress>? progress,
        double percent,
        string headline,
        string detail)
    {
        progress?.Report(new MasterDataOperationProgress(
            percent,
            headline,
            detail,
            $"{(int)Math.Round(percent)}%"));
    }

    public async Task ApplySchemaImportAsync(
        ExcelSchemaImportParseResult parsed,
        IProgress<MasterDataOperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (parsed.Fields.Count == 0)
        {
            StatusMessage = parsed.Warnings.FirstOrDefault() ?? "No columns detected in the file.";
            return;
        }

        if (IsCatalogCanvasMode
            && SelectedDataTemplate is not null
            && (parsed.Rows.Count > LargeImportRowThreshold
                || parsed.Fields.Count > LargeImportColumnThreshold))
        {
            await ImportLargeCatalogFromFileAsync(parsed, progress, cancellationToken).ConfigureAwait(true);
            return;
        }

        ReportProgress(progress, 5, "Importing", "Preparing columns…");
        Columns.Clear();
        foreach (var field in parsed.Fields.OrderBy(f => f.DisplayOrder))
        {
            Columns.Add(field);
        }

        Rows.Clear();
        var total = parsed.Rows.Count;
        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = parsed.Rows[index];
            var row = new MasterDataRowViewModel();
            foreach (var col in Columns)
            {
                var text = source.TryGetValue(col.Key, out var v) ? v ?? string.Empty : string.Empty;
                row.Cells.Add(CreateCell(col, text, null));
            }

            Rows.Add(row);
            if (index % 25 == 0 || index == total - 1)
            {
                var pct = total == 0 ? 40 : 10 + 80.0 * (index + 1) / total;
                ReportProgress(progress, pct, "Importing", $"Loaded {index + 1} of {total} rows…");
                if (index % 100 == 0)
                {
                    await Task.Yield();
                }
            }
        }

        if (Rows.Count < CanvasMinRows)
        {
            EnsureCanvasRowBuffer();
        }

        IsEditMode = true;
        ReportProgress(progress, 100, "Import complete", total > 0 ? $"{total} rows loaded into the grid." : "Columns ready.");
        StatusMessage = parsed.Rows.Count > 0
            ? $"Loaded {parsed.Rows.Count} row(s) from file. Saving to catalog…"
            : "Columns loaded from file header row.";
        if (parsed.Warnings.Count > 0)
        {
            StatusMessage += " " + string.Join(" ", parsed.Warnings);
        }

        await CompleteSchemaImportPersistenceAsync(progress, cancellationToken).ConfigureAwait(true);
    }

    private async Task CompleteSchemaImportPersistenceAsync(
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (SelectedDataTemplate is null || Columns.Count == 0)
        {
            ClearOperationProgress();
            return;
        }

        _catalogImportInProgress = true;
        try
        {
            ReportProgress(progress, 92, "Importing", "Saving to catalog…");
            var canvasMode = IsCatalogCanvasMode;
            if (canvasMode)
            {
                var commitError = await CommitCanvasSchemaAsync(cancellationToken).ConfigureAwait(true);
                if (commitError is not null)
                {
                    StatusMessage = commitError;
                    return;
                }
            }

            var templateId = SelectedDataTemplate.Id;
            var rowsToSave = canvasMode ? TrimCanvasRowsForSave() : Rows.ToList();
            await PersistRowsBatchedAsync(templateId, rowsToSave, progress, cancellationToken).ConfigureAwait(true);

            IsCatalogCanvasMode = false;
            IsEditMode = false;
            ShowNoTemplateHint = false;
            ReportProgress(progress, 100, "Import complete", "Catalog updated.");
            StatusMessage = "Import saved.";
            await LoadCatalogRowsPageAsync(0, 50, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusMessage = "Import save failed: " + ex.Message;
        }
        finally
        {
            _catalogImportInProgress = false;
            ClearOperationProgress();
        }
    }

    private async Task ImportLargeCatalogFromFileAsync(
        ExcelSchemaImportParseResult parsed,
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (SelectedDataTemplate is null)
        {
            return;
        }

        var templateId = SelectedDataTemplate.Id;
        var templateName = SelectedDataTemplate.Name ?? "catalog";
        var importRows = parsed.Rows ?? Array.Empty<IReadOnlyDictionary<string, string>>();
        var totalRows = Math.Max(1, importRows.Count);

        if (string.IsNullOrWhiteSpace(templateId))
        {
            StatusMessage = "This catalog has no id. Save the catalog name first, then import again.";
            return;
        }

        IsLoading = true;
        _suppressTemplateSelectionChange = true;
        try
        {
            ReportProgress(progress, 8, "Importing", "Saving column layout…");
            var commitError = await CommitCatalogSchemaFieldsAsync(parsed.Fields, cancellationToken).ConfigureAwait(true);
            if (commitError is not null)
            {
                StatusMessage = commitError;
                return;
            }

            templateId = SelectedDataTemplate?.Id ?? templateId;
            templateName = SelectedDataTemplate?.Name ?? templateName;
            if (string.IsNullOrWhiteSpace(templateId))
            {
                StatusMessage = "Catalog was cleared while importing. Please select the catalog and try again.";
                return;
            }

            ReportProgress(progress, 20, "Importing", "Writing rows to the database…");
            var rowProgress = new Progress<int>(inserted =>
            {
                var pct = 20 + 75.0 * inserted / totalRows;
                ReportProgress(progress, pct, "Importing", $"Stored {inserted} of {totalRows} rows…");
            });

            var inserted = await _records
                .AppendRowsAsync(templateId, importRows, rowProgress, cancellationToken)
                .ConfigureAwait(true);

            IsCatalogCanvasMode = false;
            IsEditMode = false;
            ReportProgress(progress, 100, "Import complete", $"Stored {inserted} row(s).");
            StatusMessage = $"Imported {inserted} row(s) into \"{templateName}\".";
            ShowNoTemplateHint = false;
            await LoadCatalogRowsPageAsync(0, 50, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            _suppressTemplateSelectionChange = false;
            IsLoading = false;
            ClearOperationProgress();
        }
    }

    private async Task<string?> CommitCatalogSchemaFieldsAsync(
        IReadOnlyList<TemplateFieldDefinition> fields,
        CancellationToken cancellationToken)
    {
        if (SelectedDataTemplate is null || fields.Count == 0)
        {
            return "No catalog or columns to save.";
        }

        var patched = new PartTemplateDefinition
        {
            Id = SelectedDataTemplate.Id,
            Name = SelectedDataTemplate.Name,
            Version = SelectedDataTemplate.Version + 1,
            IsPublished = true,
            IsMasterDataCatalog = true,
            Fields = fields.OrderBy(f => f.DisplayOrder).ToList(),
        };

        await _templateSchema.SaveTemplateAsync(patched, cancellationToken).ConfigureAwait(true);
        var suppressReload = _suppressTemplateSelectionChange;
        if (!suppressReload)
        {
            _suppressTemplateSelectionChange = true;
        }

        try
        {
            SelectedDataTemplate = patched;
        }
        finally
        {
            if (!suppressReload)
            {
                _suppressTemplateSelectionChange = false;
            }
        }

        Columns.Clear();
        foreach (var field in patched.Fields.OrderBy(f => f.DisplayOrder))
        {
            Columns.Add(field);
        }

        var index = DataTemplates.ToList().FindIndex(t => t.Id == patched.Id);
        if (index >= 0)
        {
            DataTemplates[index] = patched;
        }

        return null;
    }

    public bool ShouldInferImportSchema() =>
        IsCatalogCanvasMode
        || SelectedDataTemplate?.Fields.Count == 0
        || Columns.All(c => string.IsNullOrWhiteSpace(c.Label));

    [RelayCommand(CanExecute = nameof(CanAddRows))]
    private void AddRow()
    {
        Rows.Add(CreateEmptyRow());
        StatusMessage = SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog
            ? $"Added row {Rows.Count}."
            : $"Added row {Rows.Count}. Click Save when finished.";
        RequestCatalogAutoSave();
    }

    public void InsertRowAt(int index)
    {
        if (!IsEditMode || Columns.Count == 0)
        {
            return;
        }

        var clamped = Math.Clamp(index, 0, Rows.Count);
        Rows.Insert(clamped, CreateEmptyRow());
        RequestCatalogAutoSave();
    }

    public bool CanManageColumns =>
        IsEditMode
        && Columns.Count > 0
        && (CanEditCatalogCanvas || (ShowSpreadsheetEditChrome && (CanEditMasterData || CanAddMasterData)));

    public bool CanRemoveColumns =>
        IsEditMode
        && Columns.Count > 1
        && (CanEditCatalogCanvas || (ShowSpreadsheetEditChrome && CanEditMasterData));

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
            await LoadForTemplateAsync(
                SelectedDataTemplate.Id,
                cancellationToken,
                restoreEditModeAfterLoad: true).ConfigureAwait(true);
            IsEditMode = true;
            IsAddColumnPanelVisible = false;
            StatusMessage = $"Column \"{trimmed}\" added. You can edit cells in the new column, then Save.";
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

    private async Task<string?> CommitCanvasSchemaAsync(CancellationToken cancellationToken)
    {
        if (SelectedDataTemplate is null)
        {
            return "No catalog selected.";
        }

        var fields = BuildFieldsFromCanvasColumns();
        if (fields.Count == 0)
        {
            return "Add at least one column name in the header row, enter data, or import a file.";
        }

        var patched = new PartTemplateDefinition
        {
            Id = SelectedDataTemplate.Id,
            Name = SelectedDataTemplate.Name,
            Version = SelectedDataTemplate.Version + 1,
            IsPublished = true,
            IsMasterDataCatalog = true,
            Fields = fields,
        };

        await _templateSchema.SaveTemplateAsync(patched, cancellationToken).ConfigureAwait(true);
        SelectedDataTemplate = patched;
        var oldByIndex = Columns.ToList();
        Columns.Clear();
        foreach (var field in fields)
        {
            Columns.Add(field);
        }

        RemapRowCellsAfterSchemaCommit(fields, oldByIndex);
        var index = DataTemplates.ToList().FindIndex(t => t.Id == patched.Id);
        if (index >= 0)
        {
            DataTemplates[index] = patched;
        }

        return null;
    }

    private List<TemplateFieldDefinition> BuildFieldsFromCanvasColumns()
    {
        var fields = new List<TemplateFieldDefinition>();
        var usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            var hasData = Rows.Any(row =>
            {
                if (i >= row.Cells.Count)
                {
                    return false;
                }

                return !string.IsNullOrWhiteSpace(row.Cells[i].Text);
            });
            var label = col.Label.Trim();
            if (string.IsNullOrWhiteSpace(label) && !hasData)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                label = $"Column {fields.Count + 1}";
            }

            var key = !string.IsNullOrWhiteSpace(col.Key) && !usedKeys.Contains(col.Key)
                ? col.Key
                : SlugFieldKeyFromLabel(label, usedKeys);
            usedKeys.Add(key);
            fields.Add(new TemplateFieldDefinition
            {
                Key = key,
                Label = label,
                Type = TemplateFieldType.Text,
                IsRequired = false,
                DisplayOrder = fields.Count,
            });
        }

        return fields;
    }

    private static string SlugFieldKeyFromLabel(string label, HashSet<string> usedKeys)
    {
        var slug = new string(label
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray())
            .Trim('_');
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "column";
        }

        var key = slug;
        var n = 2;
        while (usedKeys.Contains(key))
        {
            key = $"{slug}_{n++}";
        }

        return key;
    }

    private void RemapRowCellsAfterSchemaCommit(
        IReadOnlyList<TemplateFieldDefinition> fields,
        IReadOnlyList<TemplateFieldDefinition> oldColumns)
    {
        foreach (var row in Rows)
        {
            var oldTexts = row.Cells.Select(c => c.Text ?? string.Empty).ToList();
            row.Cells.Clear();
            for (var i = 0; i < fields.Count; i++)
            {
                var text = i < oldTexts.Count ? oldTexts[i] : string.Empty;
                row.Cells.Add(CreateCell(fields[i], text, row.RowId));
            }
        }
    }

    private List<MasterDataRowViewModel> TrimCanvasRowsForSave()
    {
        var lastUsed = -1;
        for (var r = 0; r < Rows.Count; r++)
        {
            if (Rows[r].Cells.Any(c => !string.IsNullOrWhiteSpace(c.Text)))
            {
                lastUsed = r;
            }
        }

        return lastUsed < 0 ? new List<MasterDataRowViewModel>() : Rows.Take(lastUsed + 1).ToList();
    }

    [RelayCommand]
    private Task SaveGrid(CancellationToken cancellationToken = default) =>
        SaveGridCoreAsync(null, cancellationToken);

    public Task SaveGridWithProgressAsync(
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken = default) =>
        SaveGridCoreAsync(progress, cancellationToken);

    private async Task SaveGridCoreAsync(
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken,
        bool autoSave = false)
    {
        var catalog = SelectedDataTemplate;
        if (!IsEditMode || Columns.Count == 0 || catalog is null)
        {
            return;
        }

        if (autoSave && !AutoSaveMasterDataCatalog)
        {
            return;
        }

        var canvasMode = IsCatalogCanvasMode;
        var masterDataAutoSave = autoSave && SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog;
        if (!masterDataAutoSave)
        {
            IsLoading = true;
        }
        else
        {
            _catalogAutoSaveRunning = true;
        }

        try
        {
            ReportProgress(progress, 5, "Saving catalog", "Validating columns…");
            if (canvasMode)
            {
                var commitError = await CommitCanvasSchemaAsync(cancellationToken).ConfigureAwait(true);
                if (commitError is not null)
                {
                    if (!masterDataAutoSave)
                    {
                        StatusMessage = commitError;
                    }

                    return;
                }

                catalog = SelectedDataTemplate ?? catalog;
                if (catalog is null)
                {
                    if (!masterDataAutoSave)
                    {
                        StatusMessage = "Catalog could not be saved (template missing after commit).";
                    }

                    return;
                }
            }

            var templateId = catalog.Id;
            var templateName = catalog.Name;
            var rowsToSave = canvasMode ? TrimCanvasRowsForSave() : Rows.ToList();
            await PersistRowsBatchedAsync(templateId, rowsToSave, progress, cancellationToken).ConfigureAwait(true);

            await ResolveRecordLinkLabelsAsync(cancellationToken).ConfigureAwait(true);
            IsAddColumnPanelVisible = false;

            if (masterDataAutoSave)
            {
                StatusMessage = "All changes saved.";
            }
            else if (canvasMode)
            {
                IsCatalogCanvasMode = false;
                IsEditMode = false;
                await LoadForTemplateAsync(templateId, cancellationToken).ConfigureAwait(true);
                catalog = SelectedDataTemplate ?? catalog;
                StatusMessage = "Catalog saved.";
            }
            else if (SurfaceMode == MasterDataSurfaceMode.TemplateEdit)
            {
                StatusMessage = "Saved.";
            }
            else
            {
                IsEditMode = false;
                StatusMessage = "Saved. You're back in view mode.";
            }

            if (!masterDataAutoSave)
            {
                ReportProgress(progress, 100, "Save complete", "Your catalog is ready.");
                _activityLogger.LogUserAction(
                    "Master Data Saved",
                    $"Saved master data for template \"{templateName}\"");
            }

            if (IsCatalogBrowseSurface())
            {
                SaveSheetCache(templateId);
            }
            else if (!masterDataAutoSave)
            {
                InvalidateCurrentSheetCache();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Error: " + ex.Message;
        }
        finally
        {
            if (!masterDataAutoSave)
            {
                IsLoading = false;
            }
            else
            {
                _catalogAutoSaveRunning = false;
            }
        }
    }

    private async Task PersistRowsBatchedAsync(
        string templateId,
        IReadOnlyList<MasterDataRowViewModel> rowsToSave,
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var payloads = new List<(MasterDataRowViewModel Row, string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)>();
        foreach (var row in rowsToSave)
        {
            if (row?.Cells is null)
            {
                continue;
            }

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var cell in row.Cells)
            {
                if (cell is null)
                {
                    continue;
                }

                dict[cell.FieldKey] = cell.IsRecordLink
                    ? (!string.IsNullOrWhiteSpace(cell.LinkedRowId) ? cell.LinkedRowId : cell.Text)
                    : cell.Text ?? string.Empty;
            }

            if (dict.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            payloads.Add((row, row.RowId, payloads.Count, dict));
        }

        if (payloads.Count == 0)
        {
            ReportProgress(progress, 95, "Saving catalog", "No data rows to store.");
            return;
        }

        var total = payloads.Count;
        for (var offset = 0; offset < total; offset += SaveBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = payloads.Skip(offset).Take(SaveBatchSize).ToList();
            var chunk = slice
                .Select(p => (p.Id, p.RowOrder, p.Values))
                .ToList();
            var ids = await _records
                .SaveRowsAsync(templateId, chunk, Array.Empty<string>(), cancellationToken)
                .ConfigureAwait(true);

            for (var i = 0; i < ids.Count && i < slice.Count; i++)
            {
                slice[i].Row.RowId = ids[i];
            }

            var done = Math.Min(offset + slice.Count, total);
            var pct = 15 + 80.0 * done / total;
            ReportProgress(progress, pct, "Saving catalog", $"Stored {done} of {total} rows…");
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
            ? SurfaceMode == MasterDataSurfaceMode.ReadOnlyCatalog
                ? "Edit mode: changes save automatically as you work."
                : "Edit mode: use Add row / Add column below the table, then Save."
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

    /// <summary>
    /// Worksheet links configured on this primary template for the clicked column (trigger or match source).
    /// </summary>
    public IReadOnlyList<WorksheetRelationDto> GetWorksheetRelationsForField(TemplateFieldDefinition field) =>
        _worksheetRelations
            .Where(r => RelationAppliesToFieldColumn(r, field.Label))
            .ToList();

    private static bool RelationAppliesToFieldColumn(WorksheetRelationDto relation, string fieldLabel)
    {
        foreach (var label in WorksheetRelationBidirectional.GetMatchKeyColumnLabels(relation))
        {
            if (string.Equals(label, fieldLabel, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

        var clickedCell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase));
        var clickedValue = clickedCell?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clickedValue))
        {
            return [];
        }

        var sourceMap = BuildSourceRowMapForRelationMatching(row, Columns);
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

            var lookupRows = await _records.GetRowsAsync(relation.LookupTemplateId, cancellationToken)
                .ConfigureAwait(true);
            if (lookupRows.Count == 0)
            {
                lookupRows = await _records
                    .GetStoredRowsAsync(relation.LookupTemplateId, cancellationToken)
                    .ConfigureAwait(true);
            }

            var matches = lookupRows
                .Where(r => RowMatchesWorksheetRelation(
                    ExpandRowValuesForTemplate(r.Values, lookup),
                    lookup,
                    relation,
                    sourceMap,
                    SelectedDataTemplate,
                    field.Label))
                .ToList();

            if (matches.Count == 0)
            {
                continue;
            }

            var header = !string.IsNullOrWhiteSpace(relation.MenuLabel)
                ? relation.MenuLabel
                : lookup.Name;
            results.Add(new WorksheetRelationMatchResult(header, lookup, relation, matches));
        }

        return results;
    }

    /// <summary>
    /// Row values used to match worksheet relations (display text, not linked row ids).
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildSourceRowMapForRelationMatching(
        MasterDataRowViewModel row,
        IReadOnlyList<TemplateFieldDefinition>? columnDefs = null)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.Cells)
        {
            d[cell.FieldKey] = cell.Text?.Trim() ?? string.Empty;
        }

        if (columnDefs is not null)
        {
            foreach (var col in columnDefs)
            {
                var cell = row.Cells.FirstOrDefault(c =>
                    string.Equals(c.FieldKey, col.Key, StringComparison.OrdinalIgnoreCase));
                if (cell is null)
                {
                    continue;
                }

                var text = cell.Text?.Trim() ?? string.Empty;
                d[col.Label] = text;
                d[col.Key] = text;
            }
        }

        return d;
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

    public Task<int> ImportRowsAsync(
        IReadOnlyList<IReadOnlyDictionary<string, string>> importedRows,
        CancellationToken cancellationToken = default) =>
        ImportRowsAsync(importedRows, progress: null, cancellationToken);

    public async Task<int> ImportRowsAsync(
        IReadOnlyList<IReadOnlyDictionary<string, string>> importedRows,
        IProgress<MasterDataOperationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        if (SelectedDataTemplate is null || importedRows.Count == 0)
        {
            return 0;
        }

        var templateId = SelectedDataTemplate.Id;
        var normalized = new List<IReadOnlyDictionary<string, string>>(importedRows.Count);
        foreach (var source in importedRows)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var col in Columns)
            {
                values[col.Key] = source.TryGetValue(col.Key, out var value)
                    ? value?.Trim() ?? string.Empty
                    : string.Empty;
            }

            if (values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                normalized.Add(values);
            }
        }

        if (normalized.Count == 0)
        {
            return 0;
        }

        ReportProgress(progress, 10, "Importing", "Writing rows…");
        var rowProgress = new Progress<int>(inserted =>
        {
            var total = Math.Max(1, normalized.Count);
            var pct = 10 + 85.0 * inserted / total;
            ReportProgress(progress, pct, "Importing", $"Stored {inserted} of {total} rows…");
        });

        var importedCount = await _records
            .AppendRowsAsync(templateId, normalized, rowProgress, cancellationToken)
            .ConfigureAwait(true);

        ReportProgress(progress, 98, "Importing", "Refreshing grid…");
        ShowNoTemplateHint = false;
        await LoadCatalogRowsPageAsync(0, 50, cancellationToken).ConfigureAwait(true);
        ReportProgress(progress, 100, "Import complete", $"{importedCount} row(s) stored.");
        ClearOperationProgress();

        if (importedCount > 0)
        {
            _activityLogger.LogUserAction(
                "Rows Imported",
                $"Imported {importedCount} rows into template \"{SelectedDataTemplate.Name}\"");
        }

        return importedCount;
    }

    private static bool RowMatchesWorksheetRelation(
        IReadOnlyDictionary<string, string> targetValues,
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto relation,
        IReadOnlyDictionary<string, string> sourceValues,
        PartTemplateDefinition sourceTemplate,
        string? clickedColumnLabel = null)
    {
        var pairs = GetEffectiveMatchPairs(relation);
        if (pairs.Count == 0)
        {
            return false;
        }

        var comparedAny = false;
        foreach (var pair in pairs)
        {
            if (!string.IsNullOrWhiteSpace(clickedColumnLabel)
                && !string.Equals(pair.SourceColumn, clickedColumnLabel, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceValue = GetRowValueForColumn(sourceValues, sourceTemplate, pair.SourceColumn);
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                continue;
            }

            comparedAny = true;

            var targetValue = GetRowValueForColumn(targetValues, lookupTemplate, pair.TargetColumn);
            if (string.IsNullOrWhiteSpace(targetValue))
            {
                return false;
            }

            if (!string.Equals(sourceValue.Trim(), targetValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return comparedAny;
    }

    private static List<RelationMatchPairDto> GetEffectiveMatchPairs(WorksheetRelationDto relation)
    {
        WorksheetRelationBidirectional.EnsureMatchKeysFromTrigger(relation);

        var pairs = new List<RelationMatchPairDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in relation.MatchKeys)
        {
            var source = pair.SourceColumn?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(source))
            {
                continue;
            }

            var target = string.IsNullOrWhiteSpace(pair.TargetColumn) ? source : pair.TargetColumn.Trim();
            var signature = $"{source}\0{target}";
            if (!seen.Add(signature))
            {
                continue;
            }

            pairs.Add(new RelationMatchPairDto { SourceColumn = source, TargetColumn = target });
        }

        if (pairs.Count == 0 && !string.IsNullOrWhiteSpace(relation.TriggerColumn))
        {
            var trigger = relation.TriggerColumn.Trim();
            pairs.Add(new RelationMatchPairDto { SourceColumn = trigger, TargetColumn = trigger });
        }

        return pairs;
    }

    private static IReadOnlyDictionary<string, string> ExpandRowValuesForTemplate(
        IReadOnlyDictionary<string, string> raw,
        PartTemplateDefinition template)
    {
        var expanded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
        {
            expanded[kv.Key] = kv.Value ?? string.Empty;
        }

        foreach (var field in template.Fields)
        {
            var resolved = GetRowValueForColumn(raw, template, field.Label)
                           ?? GetRowValueForColumn(raw, template, field.Key)
                           ?? string.Empty;
            expanded[field.Key] = resolved;
            expanded[field.Label] = resolved;
        }

        return expanded;
    }

    private static string? GetRowValueForColumn(
        IReadOnlyDictionary<string, string> values,
        PartTemplateDefinition template,
        string columnLabel)
    {
        if (string.IsNullOrWhiteSpace(columnLabel))
        {
            return null;
        }

        var label = columnLabel.Trim();
        var field = template.Fields.FirstOrDefault(f =>
                        string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase))
                    ?? template.Fields.FirstOrDefault(f =>
                        string.Equals(f.Key, label, StringComparison.OrdinalIgnoreCase));

        if (field is not null)
        {
            if (values.TryGetValue(field.Key, out var byKey) && !string.IsNullOrWhiteSpace(byKey))
            {
                return byKey;
            }

            if (values.TryGetValue(field.Label, out var byLabel) && !string.IsNullOrWhiteSpace(byLabel))
            {
                return byLabel;
            }
        }

        foreach (var kv in values)
        {
            if (string.Equals(kv.Key, label, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
            {
                return kv.Value;
            }
        }

        return null;
    }

    public static IReadOnlyList<TemplateFieldDefinition> ResolveRelationDisplayFields(
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto? relation)
    {
        if (relation is null || relation.DisplayColumns.Count == 0)
        {
            return [];
        }

        var ordered = lookupTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
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

        return picked;
    }

    public static IReadOnlyList<TemplateFieldDefinition> GetAllBrowsableFields(PartTemplateDefinition lookupTemplate) =>
        lookupTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();

    public static bool IsInternalBrowseField(TemplateFieldDefinition field) =>
        field.Key.StartsWith("_", StringComparison.Ordinal)
        || string.Equals(field.Key, "org_code", StringComparison.OrdinalIgnoreCase);

    /// <summary>Initial checked columns in the linked-data panel (user can change via checkboxes).</summary>
    public static IReadOnlyCollection<string> GetDefaultSelectedFieldKeysForBrowse(
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto? relation)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = lookupTemplate.Fields;

        if (relation?.DisplayColumns is { Count: > 0 } displayLabels)
        {
            foreach (var label in displayLabels)
            {
                var field = fields.FirstOrDefault(f =>
                    string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase));
                if (field is not null)
                {
                    keys.Add(field.Key);
                }
            }
        }

        if (relation?.MatchKeys is { Count: > 0 } pairs)
        {
            foreach (var pair in pairs)
            {
                var field = fields.FirstOrDefault(f =>
                    string.Equals(f.Label, pair.TargetColumn, StringComparison.OrdinalIgnoreCase));
                if (field is not null)
                {
                    keys.Add(field.Key);
                }
            }
        }

        if (keys.Count == 0)
        {
            foreach (var field in fields.OrderBy(f => f.DisplayOrder))
            {
                if (!IsInternalBrowseField(field))
                {
                    keys.Add(field.Key);
                }
            }
        }

        if (keys.Count == 0 && fields.Count > 0)
        {
            keys.Add(fields.OrderBy(f => f.DisplayOrder).First().Key);
        }

        return keys;
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

public readonly record struct MasterDataOperationProgress(
    double Percent,
    string Headline,
    string Detail,
    string PercentLabel);

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
