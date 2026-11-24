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
    }

    public ObservableCollection<WmsWarehouseDto> Warehouses { get; }
    public ObservableCollection<WmsStockOwnerDto> StockOwners { get; }
    public ObservableCollection<WmsZoneDto> Zones { get; }
    public ObservableCollection<WmsLocationDto> Locations { get; }

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

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var health = await _wms.GetHealthAsync(ct).ConfigureAwait(true);
            if (!health.Ok)
            {
                StatusMessage = health.Error ?? "WMS API unavailable. Deploy backend with WMS routes.";
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

            await ReloadMastersAsync(ct).ConfigureAwait(true);
            StatusMessage = "Connected to WMS setup API (Phase 02A). Legacy Inventory is unchanged.";
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

        await ReloadZonesAndLocationsAsync(ct).ConfigureAwait(true);
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