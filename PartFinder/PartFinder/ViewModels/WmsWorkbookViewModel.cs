using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel : ViewModelBase
{
    private readonly WmsApiClient _wms;
    private readonly ICurrentUserAccessService _access;

    public WmsWorkbookViewModel(WmsApiClient wms, ICurrentUserAccessService access)
    {
        _wms = wms;
        _access = access;
        Warehouses = new ObservableCollection<WmsWarehouseDto>();
        StockOwners = new ObservableCollection<WmsStockOwnerDto>();
        Zones = new ObservableCollection<WmsZoneDto>();
        Locations = new ObservableCollection<WmsLocationDto>();
        SkuRows = new ObservableCollection<WmsSkuDto>();
        BalanceRows = new ObservableCollection<WmsBalanceRowDto>();
        TransactionRows = new ObservableCollection<WmsTransactionRowDto>();
        MappingErrorRows = new ObservableCollection<string>();
        InitializeNavigation();
    }

    public ObservableCollection<WmsWarehouseDto> Warehouses { get; }
    public ObservableCollection<WmsStockOwnerDto> StockOwners { get; }
    public ObservableCollection<WmsZoneDto> Zones { get; }
    public ObservableCollection<WmsLocationDto> Locations { get; }
    public ObservableCollection<WmsSkuDto> SkuRows { get; }
    public ObservableCollection<WmsBalanceRowDto> BalanceRows { get; }
    public ObservableCollection<WmsTransactionRowDto> TransactionRows { get; }
    public ObservableCollection<string> MappingErrorRows { get; }

    public bool CanEditSetup => _access.IsOrganizationAdministrator;

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    private string _transactionProbeSummary = string.Empty;
    public string TransactionProbeSummary
    {
        get => _transactionProbeSummary;
        set => SetProperty(ref _transactionProbeSummary, value);
    }

    private WmsWarehouseDto? _selectedWarehouse;
    public WmsWarehouseDto? SelectedWarehouse
    {
        get => _selectedWarehouse;
        set
        {
            if (SetProperty(ref _selectedWarehouse, value))
            {
                _ = ReloadZonesAndLocationsAsync();
            }
        }
    }

    [ObservableProperty]
    private string _newWarehouseCode = string.Empty;

    [ObservableProperty]
    private string _newWarehouseName = string.Empty;

    [ObservableProperty]
    private string _newOwnerCode = string.Empty;

    [ObservableProperty]
    private string _newOwnerName = string.Empty;

    [ObservableProperty]
    private string _newZoneCode = string.Empty;

    [ObservableProperty]
    private string _newZoneName = string.Empty;

    [ObservableProperty]
    private string _newLocationPath = string.Empty;

    [ObservableProperty]
    private string _newLocationType = "storage";

    [ObservableProperty]
    private string _inventorySearch = string.Empty;

    [ObservableProperty]
    private string _openingDryRunSummary = string.Empty;

    [ObservableProperty]
    private string _paginationSummary = string.Empty;

    [ObservableProperty]
    private string _newSkuDisplayName = string.Empty;

    [ObservableProperty]
    private string _newSkuPrimaryUom = "EA";

    [ObservableProperty]
    private string _newSkuDescription = string.Empty;

    [ObservableProperty]
    private string _identifierNamespace = "manufacturerPn";

    [ObservableProperty]
    private string _identifierValue = string.Empty;

    [ObservableProperty]
    private string _identifierManufacturer = string.Empty;

    [ObservableProperty]
    private string _newUomCode = string.Empty;

    [ObservableProperty]
    private string _newUomName = string.Empty;

    [ObservableProperty]
    private string _receivingGateSummary =
        "Stock mutations require staging gate + server flags (not exposed in UI).";

    [ObservableProperty]
    private string _worksheetSearchText = string.Empty;

    [ObservableProperty]
    private string _draftExternalPoId = string.Empty;

    [ObservableProperty]
    private string _draftSupplierId = string.Empty;

    [ObservableProperty]
    private string _draftSupplierNote = string.Empty;

    [ObservableProperty]
    private double _qcQtyAccepted;

    [ObservableProperty]
    private double _qcQtyRejected;

    [ObservableProperty]
    private string _qcNote = string.Empty;

    [ObservableProperty]
    private string _grantOperatorEmail = string.Empty;

    [ObservableProperty]
    private bool _grantPermReceiving = true;

    [ObservableProperty]
    private bool _grantPermQc = true;

    [ObservableProperty]
    private bool _grantPermPutaway = true;

    [ObservableProperty]
    private bool _grantPermSupervise;

    [ObservableProperty]
    private string _receivingListSortKey = "date";

    [ObservableProperty]
    private bool _receivingListSortAscending;

    [ObservableProperty]
    private int _receivingColSku = 160;

    [ObservableProperty]
    private int _receivingColQty = 88;

    [ObservableProperty]
    private int _receivingColStatus = 100;

    [ObservableProperty]
    private double _dispositionQuantity;

    [ObservableProperty]
    private string _dispositionReason = string.Empty;

    [ObservableProperty]
    private string _dispositionType = "return_to_supplier";

    private WmsReceiptLineRowDto? _selectedReceiptLine;
    public WmsReceiptLineRowDto? SelectedReceiptLine
    {
        get => _selectedReceiptLine;
        set => SetProperty(ref _selectedReceiptLine, value);
    }

    [ObservableProperty]
    private bool _canOperateReceiving;

    [ObservableProperty]
    private bool _canOperateQc;

    [ObservableProperty]
    private bool _canOperatePutaway;

    [ObservableProperty]
    private bool _canManageOperatorGrants;

    public ObservableCollection<WmsExpectedLineDraftVm> ExpectedLineDrafts { get; } = new();
    public ObservableCollection<WmsGoodsReceiptLineDraftVm> GoodsReceiptLineDrafts { get; } = new();
    public ObservableCollection<WmsOperatorGrantDto> OperatorGrantRows { get; } = new();

    public ObservableCollection<WmsGrantScopeRowVm> GrantWarehouseScope { get; } = new();

    public ObservableCollection<WmsGrantScopeRowVm> GrantOwnerScope { get; } = new();

    [ObservableProperty]
    private bool _canRecordStagingPreflight;

    [ObservableProperty]
    private string _stagingPreflightNote = string.Empty;

    private WmsExpectedReceiptRowDto? _selectedExpectedReceipt;
    public WmsExpectedReceiptRowDto? SelectedExpectedReceipt
    {
        get => _selectedExpectedReceipt;
        set => SetProperty(ref _selectedExpectedReceipt, value);
    }

    private WmsGoodsReceiptRowDto? _selectedGoodsReceipt;
    public WmsGoodsReceiptRowDto? SelectedGoodsReceipt
    {
        get => _selectedGoodsReceipt;
        set
        {
            if (SetProperty(ref _selectedGoodsReceipt, value))
                _ = ReloadReceiptLinesForSelectedAsync();
        }
    }

    private WmsStockOwnerDto? _draftReceiptOwner;
    public WmsStockOwnerDto? DraftReceiptOwner
    {
        get => _draftReceiptOwner;
        set => SetProperty(ref _draftReceiptOwner, value);
    }

    private WmsLocationDto? _draftReceivingLocation;
    public WmsLocationDto? DraftReceivingLocation
    {
        get => _draftReceivingLocation;
        set => SetProperty(ref _draftReceivingLocation, value);
    }

    private WmsLocationDto? _draftPutawayToLocation;
    public WmsLocationDto? DraftPutawayToLocation
    {
        get => _draftPutawayToLocation;
        set => SetProperty(ref _draftPutawayToLocation, value);
    }

    private WmsQcRowDto? _selectedQcRow;
    public WmsQcRowDto? SelectedQcRow
    {
        get => _selectedQcRow;
        set => SetProperty(ref _selectedQcRow, value);
    }

    private WmsPutawayRowDto? _selectedPutawayTask;
    public WmsPutawayRowDto? SelectedPutawayTask
    {
        get => _selectedPutawayTask;
        set => SetProperty(ref _selectedPutawayTask, value);
    }

    private WmsSkuDto? _selectedSku;
    public WmsSkuDto? SelectedSku
    {
        get => _selectedSku;
        set
        {
            if (SetProperty(ref _selectedSku, value))
            {
                _ = ReloadSkuIdentifiersAsync();
            }
        }
    }

    private WmsStockOwnerDto? _filterStockOwner;
    public WmsStockOwnerDto? FilterStockOwner
    {
        get => _filterStockOwner;
        set
        {
            if (SetProperty(ref _filterStockOwner, value))
            {
                _ = ReloadBalancesAsync();
            }
        }
    }

    private WmsLocationDto? _filterLocation;
    public WmsLocationDto? FilterLocation
    {
        get => _filterLocation;
        set
        {
            if (SetProperty(ref _filterLocation, value))
            {
                _ = ReloadBalancesAsync();
            }
        }
    }

    private int _balancePage;
    private int _txnPage;
    private const int PageSize = 50;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var health = await _wms.GetHealthAsync(ct).ConfigureAwait(true);
            if (!health.Ok)
            {
                ApplyApiError(health.Error ?? "WMS API unavailable. Deploy backend with WMS routes.");
                return;
            }

            if (health.Data?.StockMutations == true)
            {
                StatusMessage = "Unexpected: stock mutations reported enabled.";
            }

            var probe = await _wms.ProbeTransactionsAsync(ct).ConfigureAwait(true);
            if (probe.Ok && probe.Data is not null)
            {
                TransactionProbeSummary = probe.Data.Supported
                    ? "Mongo transactions: supported (staging check)."
                    : $"Mongo transactions: NOT verified — {probe.Data.Detail}";
            }
            else
            {
                TransactionProbeSummary = probe.Error ?? "Transaction probe failed.";
            }

            await LoadSessionProfileAsync().ConfigureAwait(true);
            await RefreshStagingGateSummaryAsync().ConfigureAwait(true);
            await ReloadMastersAsync(ct).ConfigureAwait(true);
            await ReloadSkusAsync(ct).ConfigureAwait(true);
            await ReloadDryRunAsync(ct).ConfigureAwait(true);
            var catalog = await _wms.GetCatalogStatusAsync(ct).ConfigureAwait(true);
            CatalogPhaseSummary = catalog.Data?.Phase ?? health.Data?.Phase ?? "WMS";
            OnPropertyChanged(nameof(CatalogPhaseSummary));

            var phase = health.Data?.Phase ?? "WMS";
            StatusMessage =
                $"Connected ({phase}). Legacy partfinder_inventory is unchanged; WMS ledger is API-backed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync().ConfigureAwait(true);

    [RelayCommand]
    private async Task AddWarehouseAsync()
    {
        if (!CanEditSetup)
        {
            StatusMessage = "Admin permission required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewWarehouseCode) || string.IsNullOrWhiteSpace(NewWarehouseName))
        {
            StatusMessage = "Warehouse code and name are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateWarehouseAsync(new CreateWmsWarehouseRequest
            {
                Code = NewWarehouseCode.Trim(),
                Name = NewWarehouseName.Trim(),
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                StatusMessage = result.Error ?? "Create warehouse failed.";
                return;
            }

            Warehouses.Add(result.Data);
            NewWarehouseCode = string.Empty;
            NewWarehouseName = string.Empty;
            StatusMessage = $"Warehouse {result.Data.Code} created.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddStockOwnerAsync()
    {
        if (!CanEditSetup)
        {
            StatusMessage = "Admin permission required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewOwnerCode) || string.IsNullOrWhiteSpace(NewOwnerName))
        {
            StatusMessage = "Owner code and name are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateStockOwnerAsync(new CreateWmsStockOwnerRequest
            {
                Code = NewOwnerCode.Trim(),
                Name = NewOwnerName.Trim(),
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                StatusMessage = result.Error ?? "Create stock owner failed.";
                return;
            }

            StockOwners.Add(result.Data);
            NewOwnerCode = string.Empty;
            NewOwnerName = string.Empty;
            StatusMessage = $"Stock owner {result.Data.Code} created.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddZoneAsync()
    {
        if (!CanEditSetup || SelectedWarehouse is null)
        {
            StatusMessage = "Select a warehouse and use an admin account.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewZoneCode) || string.IsNullOrWhiteSpace(NewZoneName))
        {
            StatusMessage = "Zone code and name are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateZoneAsync(new CreateWmsZoneRequest
            {
                WarehouseId = SelectedWarehouse.Id,
                Code = NewZoneCode.Trim(),
                Name = NewZoneName.Trim(),
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                StatusMessage = result.Error ?? "Create zone failed.";
                return;
            }

            Zones.Add(result.Data);
            NewZoneCode = string.Empty;
            NewZoneName = string.Empty;
            StatusMessage = $"Zone {result.Data.Code} created.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddLocationAsync()
    {
        if (!CanEditSetup || SelectedWarehouse is null)
        {
            StatusMessage = "Select a warehouse and use an admin account.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewLocationPath))
        {
            StatusMessage = "Location path is required (e.g. A-01-02).";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateLocationAsync(new CreateWmsLocationRequest
            {
                WarehouseId = SelectedWarehouse.Id,
                Path = NewLocationPath.Trim(),
                Type = NewLocationType.Trim().ToLowerInvariant(),
                IsPickable = NewLocationType == "storage",
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                StatusMessage = result.Error ?? "Create location failed.";
                return;
            }

            Locations.Add(result.Data);
            NewLocationPath = string.Empty;
            StatusMessage = $"Location {result.Data.Path} created.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadMastersAsync(CancellationToken ct)
    {
        Warehouses.Clear();
        StockOwners.Clear();

        var wh = await _wms.ListWarehousesAsync(ct).ConfigureAwait(true);
        if (wh.Ok)
        {
            foreach (var w in wh.Items)
            {
                Warehouses.Add(w);
            }

            SelectedWarehouse ??= Warehouses.FirstOrDefault();
        }

        var owners = await _wms.ListStockOwnersAsync(ct).ConfigureAwait(true);
        if (owners.Ok)
        {
            foreach (var o in owners.Items)
            {
                StockOwners.Add(o);
            }
        }

        SyncGrantScopeRows();
        await ReloadZonesAndLocationsAsync(ct).ConfigureAwait(true);
    }

    private void SyncGrantScopeRows()
    {
        var prevWh = GrantWarehouseScope.Where(g => g.IsSelected).Select(g => g.Id).ToHashSet();
        var prevOwn = GrantOwnerScope.Where(g => g.IsSelected).Select(g => g.Id).ToHashSet();
        GrantWarehouseScope.Clear();
        GrantOwnerScope.Clear();
        foreach (var w in Warehouses)
        {
            var label = string.IsNullOrWhiteSpace(w.Name) ? w.Code : $"{w.Code} — {w.Name}";
            GrantWarehouseScope.Add(new WmsGrantScopeRowVm
            {
                Id = w.Id,
                Label = label,
                IsSelected = prevWh.Count == 0 || prevWh.Contains(w.Id),
            });
        }

        foreach (var o in StockOwners)
        {
            var label = string.IsNullOrWhiteSpace(o.Name) ? o.Code : $"{o.Code} — {o.Name}";
            GrantOwnerScope.Add(new WmsGrantScopeRowVm
            {
                Id = o.Id,
                Label = label,
                IsSelected = prevOwn.Count == 0 || prevOwn.Contains(o.Id),
            });
        }
    }

    [RelayCommand]
    private async Task NextBalancePageAsync()
    {
        _balancePage++;
        await ReloadBalancesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrevBalancePageAsync()
    {
        if (_balancePage > 0) _balancePage--;
        await ReloadBalancesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task NextTxnPageAsync()
    {
        _txnPage++;
        await ReloadTransactionsAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task PrevTxnPageAsync()
    {
        if (_txnPage > 0) _txnPage--;
        await ReloadTransactionsAsync().ConfigureAwait(true);
    }

    public string CatalogPhaseSummary { get; private set; } = string.Empty;

    private async Task ReloadBalancesAsync(CancellationToken ct = default)
    {
        BalanceRows.Clear();
        var whId = SelectedWarehouse?.Id;
        var ownerId = FilterStockOwner?.Id;
        var skuFilter = string.IsNullOrWhiteSpace(InventorySearch) ? null : InventorySearch.Trim();
        var result = await _wms.ListBalancesAsync(whId, ownerId, skuFilter, _balancePage * PageSize, PageSize, ct)
            .ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        if (result.Data?.Items is not null)
        {
            var items = result.Data.Items.AsEnumerable();
            if (FilterLocation is not null)
                items = items.Where(r => r.LocationId == FilterLocation.Id);
            foreach (var row in FilterBySearch(items, InventorySearch, x => $"{x.SkuId} {x.LocationId}"))
                BalanceRows.Add(row);
            PaginationSummary = $"Balances page {_balancePage + 1} ({result.Data.Total} total)";
        }
    }

    private async Task ReloadTransactionsAsync(CancellationToken ct = default)
    {
        TransactionRows.Clear();
        var whId = SelectedWarehouse?.Id;
        var result = await _wms.ListTransactionsAsync(whId, null, null, _txnPage * PageSize, PageSize, ct)
            .ConfigureAwait(true);
        if (result.Ok && result.Data?.Items is not null)
        {
            foreach (var row in FilterBySearch(result.Data.Items, InventorySearch, x => $"{x.TxnId} {x.Type}"))
                TransactionRows.Add(row);
        }
    }

    private static IEnumerable<T> FilterBySearch<T>(
        IEnumerable<T> source,
        string search,
        Func<T, string> text)
    {
        var q = search.Trim();
        if (string.IsNullOrWhiteSpace(q)) return source;
        return source.Where(x => text(x).Contains(q, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ReloadZonesAndLocationsAsync(CancellationToken ct = default)
    {
        Zones.Clear();
        Locations.Clear();
        if (SelectedWarehouse is null)
        {
            return;
        }

        var zones = await _wms.ListZonesAsync(SelectedWarehouse.Id, ct).ConfigureAwait(true);
        if (zones.Ok)
        {
            foreach (var z in zones.Items)
            {
                Zones.Add(z);
            }
        }

        var locs = await _wms.ListLocationsAsync(SelectedWarehouse.Id, ct).ConfigureAwait(true);
        if (locs.Ok)
        {
            foreach (var l in locs.Items)
            {
                Locations.Add(l);
            }
        }
    }
}