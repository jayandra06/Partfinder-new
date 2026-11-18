namespace PartFinder.Models;

/// <summary>Worksheet / section ids for the Warehouse Workbook shell.</summary>
public static class WmsWorkbookSections
{
    public const string Dashboard = "dashboard";
    public const string WarehouseMaster = "warehouse_master";
    public const string Locations = "locations";
    public const string StockOwners = "stock_owners";
    public const string SkuRegistry = "sku_registry";
    public const string SkuIdentifiers = "sku_identifiers";
    public const string UnitsOfMeasure = "uom";
    public const string StockBalances = "stock_balances";
    public const string LocationStock = "location_stock";
    public const string MovementLedger = "movement_ledger";
    public const string OpeningPreview = "opening_preview";
    public const string MappingErrors = "mapping_errors";
    public const string MasterDataGuide = "master_data_guide";
    public const string Administration = "administration";

    // Phase 03A — Receiving
    public const string ExpectedReceipts = "expected_receipts";
    public const string GoodsReceiving = "goods_receiving";
    public const string ReceivedItemDetails = "received_item_details";
    public const string QualityControl = "quality_control";
    public const string PutawayTasks = "putaway_tasks";
    public const string LocationAssignment = "location_assignment";
    public const string ReceivingHistory = "receiving_history";
    public const string ReceivingDiscrepancies = "receiving_discrepancies";

    // Roadmap — future phases
    public const string Receiving = "receiving";
    public const string Orders = "orders";
    public const string Picking = "picking";
    public const string Packing = "packing";
    public const string Printing = "printing";
    public const string Dispatch = "dispatch";
    public const string Procurement = "procurement";
    public const string Reports = "reports";
}