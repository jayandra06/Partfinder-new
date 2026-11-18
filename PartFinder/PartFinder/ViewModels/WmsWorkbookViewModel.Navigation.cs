using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel
{
    public ObservableCollection<WmsWorkbookNavItem> NavItems { get; } = new();

    private string _selectedSectionId = WmsWorkbookSections.Dashboard;
    public string SelectedSectionId
    {
        get => _selectedSectionId;
        set => SetProperty(ref _selectedSectionId, value);
    }

    private bool _isRoadmapSection;
    public bool IsRoadmapSection
    {
        get => _isRoadmapSection;
        set => SetProperty(ref _isRoadmapSection, value);
    }

    public string RoadmapTitle { get; private set; } = string.Empty;
    public string RoadmapPhase { get; private set; } = string.Empty;
    public string RoadmapDescription { get; private set; } = string.Empty;

    private void InitializeNavigation()
    {
        NavItems.Clear();
        AddNavGroup("Overview");
        AddNav(WmsWorkbookSections.Dashboard, "Dashboard", "Connection & diagnostics", true);

        AddNavGroup("Warehouse Setup");
        AddNav(WmsWorkbookSections.WarehouseMaster, "Warehouse Master", "Sites & codes", true);
        AddNav(WmsWorkbookSections.Locations, "Zones & locations", "Aisles, racks, bins paths", true);
        AddNav(WmsWorkbookSections.StockOwners, "3PL stock owners", "Owning companies", true);
        AddNav(WmsWorkbookSections.SkuRegistry, "SKU registry", "Permanent SKU identity", true);
        AddNav(WmsWorkbookSections.SkuIdentifiers, "SKU identifier mapping", "MPN, ERP, barcodes", true);
        AddNav(WmsWorkbookSections.UnitsOfMeasure, "Units of measure", "EA, KG, custom", true);

        AddNavGroup("Master Data");
        _roadmapCopy[WmsWorkbookSections.MasterDataGuide] = (
            "PartFinder master data",
            "Use main navigation",
            "Templates, catalogs and imports remain in Master Data and legacy Inventory. " +
            "Register permanent SKUs here and link rows using SKU identifier mapping.");
        AddNav(
            WmsWorkbookSections.MasterDataGuide,
            "PartFinder master data",
            "Templates & catalogs (main app)",
            true);

        AddNavGroup("Inventory");
        AddNav(WmsWorkbookSections.StockBalances, "Stock balances", "Read-only on-hand", true);
        AddNav(WmsWorkbookSections.LocationStock, "Location stock", "By bin path", true);
        AddNav(WmsWorkbookSections.MovementLedger, "Movement ledger", "Posted transactions", true);
        AddNav(WmsWorkbookSections.OpeningPreview, "Opening balance preview", "Dry-run mapping", true);
        AddNav(WmsWorkbookSections.MappingErrors, "SKU mapping errors", "Unmapped legacy rows", true);

        AddNavGroup("Receiving (03A)");
        AddNav(WmsWorkbookSections.ExpectedReceipts, "Expected receipts", "ASN / PO expected", true);
        AddNav(WmsWorkbookSections.GoodsReceiving, "Goods receiving", "Physical receipt", true);
        AddNav(WmsWorkbookSections.ReceivedItemDetails, "Received item details", "Lots & serials", true);
        AddNav(WmsWorkbookSections.QualityControl, "Quality control", "Pending / accept / reject", true);
        AddNav(WmsWorkbookSections.PutawayTasks, "Put-away tasks", "Receiving → storage", true);
        AddNav(WmsWorkbookSections.LocationAssignment, "Location assignment", "Bin targets", true);
        AddNav(WmsWorkbookSections.ReceivingHistory, "Receiving history", "Audit trail", true);
        AddNav(WmsWorkbookSections.ReceivingDiscrepancies, "Discrepancies", "Short / over / damage", true);

        AddNavGroup("Operations (roadmap)");
        AddNav(WmsWorkbookSections.Orders, "Orders", "", false, "Phase 03B");
        AddNav(WmsWorkbookSections.Picking, "Picking", "", false, "Phase 03B");
        AddNav(WmsWorkbookSections.Packing, "Packing", "", false, "Phase 04");
        AddNav(WmsWorkbookSections.Printing, "Printing", "", false, "Phase 04");
        AddNav(WmsWorkbookSections.Dispatch, "Dispatch", "", false, "Phase 05");
        AddNav(WmsWorkbookSections.Procurement, "Procurement", "", false, "Phase 05");
        AddNav(WmsWorkbookSections.Reports, "Reports", "", false, "Phase 06");

        AddNavGroup("Administration");
        AddNav(WmsWorkbookSections.Administration, "Administration", "Health & access (Phase 07)", true);
    }

    private void AddNavGroup(string title) =>
        NavItems.Add(new WmsWorkbookNavItem { IsGroupHeader = true, GroupTitle = title });

    private void AddNav(
        string id,
        string title,
        string subtitle,
        bool operational,
        string? roadmapPhase = null,
        string? description = null)
    {
        NavItems.Add(new WmsWorkbookNavItem
        {
            SectionId = id,
            Title = title,
            Subtitle = subtitle,
            IsOperational = operational,
            RoadmapPhase = roadmapPhase,
        });
        if (!operational && roadmapPhase is not null)
        {
            _roadmapCopy[id] = (
                title,
                roadmapPhase,
                description ?? $"{title} requires backend workflows not yet released.");
        }
    }

    private readonly Dictionary<string, (string Title, string Phase, string Desc)> _roadmapCopy = new();

    [RelayCommand]
    private async Task SelectSectionAsync(WmsWorkbookNavItem? item)
    {
        if (item is null || item.IsGroupHeader || string.IsNullOrEmpty(item.SectionId))
        {
            return;
        }

        SelectedSectionId = item.SectionId;
        if (!item.IsOperational)
        {
            IsRoadmapSection = true;
            if (_roadmapCopy.TryGetValue(item.SectionId, out var copy))
            {
                RoadmapTitle = copy.Title;
                RoadmapPhase = copy.Phase;
                RoadmapDescription = copy.Desc;
            }
            else
            {
                RoadmapTitle = item.Title;
                RoadmapPhase = item.RoadmapPhase ?? "Roadmap";
                RoadmapDescription = "Not operational in this release.";
            }

            OnPropertyChanged(nameof(RoadmapTitle));
            OnPropertyChanged(nameof(RoadmapPhase));
            OnPropertyChanged(nameof(RoadmapDescription));
            OnPropertyChanged(nameof(ShowOperationalWorksheet));
            return;
        }

        IsRoadmapSection = false;
        OnPropertyChanged(nameof(ShowOperationalWorksheet));
        await LoadSectionAsync(item.SectionId).ConfigureAwait(true);
    }

    public bool ShowOperationalWorksheet => !IsRoadmapSection;

    private async Task LoadSectionAsync(string sectionId)
    {
        switch (sectionId)
        {
            case WmsWorkbookSections.StockBalances:
            case WmsWorkbookSections.LocationStock:
                await ReloadBalancesAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.MovementLedger:
                await ReloadTransactionsAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.SkuRegistry:
                await ReloadSkusAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.SkuIdentifiers:
                await ReloadSkuIdentifiersAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.UnitsOfMeasure:
                await ReloadUomAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.OpeningPreview:
            case WmsWorkbookSections.MappingErrors:
                await ReloadDryRunAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.ExpectedReceipts:
                await ReloadExpectedReceiptsAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.GoodsReceiving:
            case WmsWorkbookSections.ReceivedItemDetails:
                await ReloadGoodsReceiptsAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.QualityControl:
                await ReloadQcAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.PutawayTasks:
            case WmsWorkbookSections.LocationAssignment:
                await ReloadPutawayAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.ReceivingHistory:
                await ReloadReceivingHistoryAsync().ConfigureAwait(true);
                break;
            case WmsWorkbookSections.ReceivingDiscrepancies:
                await ReloadDiscrepanciesAsync().ConfigureAwait(true);
                break;
        }
    }

    private void ApplyApiError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            StatusMessage = "Request failed.";
            return;
        }

        if (error.Contains("wms", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("Forbidden", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage =
                "Warehouse Management is not enabled for this organisation or your role cannot access it.";
            return;
        }

        StatusMessage = error;
    }
}