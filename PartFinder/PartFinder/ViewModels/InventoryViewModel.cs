using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public enum InventoryFormMode
{
    None,
    Add,
    Edit,
}

public partial class InventoryViewModel : PaginatedViewModelBase
{
    private readonly MongoInventoryService _inventoryService;
    private readonly BulkInventoryImportService _bulkImport;
    private readonly ActivityLogger _activityLogger;
    private readonly ICurrentUserAccessService _access;
    private readonly List<InventoryItem> _allInventory = [];
    private readonly List<InventoryItem> _filteredInventory = [];

    public InventoryViewModel(
        MongoInventoryService inventoryService,
        BulkInventoryImportService bulkImport,
        ActivityLogger activityLogger,
        ICurrentUserAccessService access)
    {
        _inventoryService = inventoryService;
        _bulkImport = bulkImport;
        _activityLogger = activityLogger;
        _access = access;
    }

    /// <summary>Export, import, add stock, and row edit actions (admin only).</summary>
    public bool ShowInventoryAdminChrome => _access.Capabilities.CanManageInventory;

    /// <summary>KPI summary cards (viewers and admins).</summary>
    public bool ShowInventoryKpiRow => _access.Capabilities.CanViewInventory;

    /// <summary>Warehouse quick action visible to employees only.</summary>
    public bool ShowWarehouseSuggestAction =>
        _access.Capabilities.CanViewInventory && !_access.Capabilities.CanManageInventory;

    public string InventoryPageTitle =>
        ShowInventoryAdminChrome ? "Warehouse Management" : "Warehouse";

    public string InventoryPageDescription =>
        ShowInventoryAdminChrome
            ? "Track stock levels, monitor part availability, and manage warehouse inventory across your organisation."
            : "Browse stock levels and part availability in your warehouse.";

    private static string DemoInventoryPurgeFlagPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "flags",
            "inventory-demo-purge-v1.flag");

    public void RefreshRoleChrome()
    {
        OnPropertyChanged(nameof(ShowInventoryAdminChrome));
        OnPropertyChanged(nameof(ShowInventoryKpiRow));
        OnPropertyChanged(nameof(ShowWarehouseSuggestAction));
        OnPropertyChanged(nameof(InventoryPageTitle));
        OnPropertyChanged(nameof(InventoryPageDescription));
    }

    public ObservableCollection<InventoryItem> FilteredInventoryItems { get; } = [];

    public ObservableCollection<string> CategoryFilterOptions { get; } = ["All categories"];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _totalItems;
    [ObservableProperty] private int _lowStockItems;
    [ObservableProperty] private int _outOfStockItems;
    [ObservableProperty] private double _totalValue;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private InventoryItem? _selectedItem;
    [ObservableProperty] private string _selectedCategoryFilter = "All categories";
    [ObservableProperty] private string _selectedStatusFilter = "All statuses";

    [ObservableProperty] private bool _showPartNameColumnSearch;
    [ObservableProperty] private bool _showPartIdColumnSearch;
    [ObservableProperty] private bool _showQuantityColumnSearch;
    [ObservableProperty] private bool _showCategoryColumnSearch;
    [ObservableProperty] private bool _showStatusColumnSearch;

    [ObservableProperty] private string _columnFilterPartName = string.Empty;
    [ObservableProperty] private string _columnFilterPartId = string.Empty;
    [ObservableProperty] private string _columnFilterQuantity = string.Empty;
    [ObservableProperty] private string _columnFilterCategory = string.Empty;
    [ObservableProperty] private string _columnFilterStatus = string.Empty;

    [ObservableProperty] private InventoryFormMode _formMode = InventoryFormMode.None;

    [ObservableProperty] private string _formTitle = string.Empty;

    [ObservableProperty] private string _formStatusMessage = string.Empty;

    public bool IsListVisible => FormMode == InventoryFormMode.None;

    public bool IsFormVisible => FormMode != InventoryFormMode.None;

    public IReadOnlyList<string> StatusFilterOptions { get; } =
        ["All statuses", "In Stock", "Low Stock", "Out of Stock"];

    public string TotalValueFormatted => TotalValue >= 1_000_000
        ? $"${TotalValue / 1_000_000:F1}M"
        : TotalValue >= 1_000
            ? $"${TotalValue / 1_000:F1}K"
            : $"${TotalValue:N0}";

    partial void OnFormModeChanged(InventoryFormMode value)
    {
        OnPropertyChanged(nameof(IsListVisible));
        OnPropertyChanged(nameof(IsFormVisible));
        FormStatusMessage = string.Empty;
    }

    partial void OnSearchQueryChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnSelectedCategoryFilterChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnSelectedStatusFilterChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnColumnFilterPartNameChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnColumnFilterPartIdChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnColumnFilterQuantityChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnColumnFilterCategoryChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    partial void OnColumnFilterStatusChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilters();
    }

    [RelayCommand]
    private void TogglePartNameHeaderSearch()
    {
        ShowPartNameColumnSearch = !ShowPartNameColumnSearch;
        if (!ShowPartNameColumnSearch)
        {
            ColumnFilterPartName = string.Empty;
        }
    }

    [RelayCommand]
    private void TogglePartIdHeaderSearch()
    {
        ShowPartIdColumnSearch = !ShowPartIdColumnSearch;
        if (!ShowPartIdColumnSearch)
        {
            ColumnFilterPartId = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleQuantityHeaderSearch()
    {
        ShowQuantityColumnSearch = !ShowQuantityColumnSearch;
        if (!ShowQuantityColumnSearch)
        {
            ColumnFilterQuantity = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleCategoryHeaderSearch()
    {
        ShowCategoryColumnSearch = !ShowCategoryColumnSearch;
        if (!ShowCategoryColumnSearch)
        {
            ColumnFilterCategory = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleStatusHeaderSearch()
    {
        ShowStatusColumnSearch = !ShowStatusColumnSearch;
        if (!ShowStatusColumnSearch)
        {
            ColumnFilterStatus = string.Empty;
        }
    }

    protected override void OnPaginationRefreshRequested() =>
        RefreshPage(FilteredInventoryItems, _filteredInventory);

    public void BeginAddForm()
    {
        if (!ShowInventoryAdminChrome)
        {
            return;
        }

        FormTitle = "Add inventory item";
        FormMode = InventoryFormMode.Add;
    }

    public void BeginEditForm(InventoryItem item)
    {
        if (!ShowInventoryAdminChrome)
        {
            return;
        }

        SelectedItem = item;
        FormTitle = $"Edit — {item.PartName}";
        FormMode = InventoryFormMode.Edit;
    }

    public void CloseForm()
    {
        FormMode = InventoryFormMode.None;
        FormStatusMessage = string.Empty;
        SelectedItem = null;
    }

    public IReadOnlyList<InventoryItem> GetAllItemsForExport() => _allInventory.ToList();

    public async Task LoadInventoryAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            await TryPurgeLegacyDemoInventoryOnceAsync(cancellationToken).ConfigureAwait(true);

            var docs = await _inventoryService.GetAllAsync(cancellationToken).ConfigureAwait(true);

            _allInventory.Clear();
            foreach (var doc in docs)
            {
                _allInventory.Add(MapDocToItem(doc));
            }

            RecomputeAnalytics();
            RefreshCategoryFilterOptions();
            ApplyFilters();

            StatusMessage = TotalItems == 0
                ? ShowInventoryAdminChrome
                    ? "No inventory items yet. Add stock to get started."
                    : "No inventory items to display."
                : $"{TotalItems} items loaded";

            OnPropertyChanged(nameof(TotalValueFormatted));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TryPurgeLegacyDemoInventoryOnceAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(DemoInventoryPurgeFlagPath))
        {
            return;
        }

        try
        {
            await _inventoryService.DeleteAllAsync(cancellationToken).ConfigureAwait(true);
            var dir = Path.GetDirectoryName(DemoInventoryPurgeFlagPath)!;
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(
                DemoInventoryPurgeFlagPath,
                DateTime.UtcNow.ToString("O"),
                cancellationToken).ConfigureAwait(true);
        }
        catch
        {
            // Non-fatal — inventory still loads on the next attempt.
        }
    }

    private void RecomputeAnalytics()
    {
        TotalItems = _allInventory.Count;
        LowStockItems = 0;
        OutOfStockItems = 0;
        TotalValue = 0;

        foreach (var item in _allInventory)
        {
            item.Status = ComputeStatus(item.Quantity, item.MinStock);

            if (item.Quantity == 0)
            {
                OutOfStockItems++;
            }
            else if (item.Quantity < item.MinStock)
            {
                LowStockItems++;
            }

            item.TotalValue = item.Quantity * item.UnitPrice;
            TotalValue += item.TotalValue;
        }
    }

    private void RefreshCategoryFilterOptions()
    {
        var previous = SelectedCategoryFilter;
        CategoryFilterOptions.Clear();
        CategoryFilterOptions.Add("All categories");

        foreach (var cat in _allInventory
                     .Select(i => i.Category.Trim())
                     .Where(c => !string.IsNullOrWhiteSpace(c))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
        {
            CategoryFilterOptions.Add(cat);
        }

        SelectedCategoryFilter = CategoryFilterOptions.Contains(previous) ? previous : "All categories";
    }

    private void ApplyFilters()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;

        IEnumerable<InventoryItem> items = _allInventory;

        if (!string.IsNullOrEmpty(query))
        {
            items = items.Where(i =>
                i.PartName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || i.PartId.Contains(query, StringComparison.OrdinalIgnoreCase)
                || i.Id.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedCategoryFilter, "All categories", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(i =>
                string.Equals(i.Category, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedStatusFilter, "All statuses", StringComparison.OrdinalIgnoreCase))
        {
            items = items.Where(i =>
                string.Equals(i.Status, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
        }

        var partNameCol = ColumnFilterPartName?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(partNameCol))
        {
            items = items.Where(i =>
                i.PartName.Contains(partNameCol, StringComparison.OrdinalIgnoreCase));
        }

        var partIdCol = ColumnFilterPartId?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(partIdCol))
        {
            items = items.Where(i =>
                i.PartId.Contains(partIdCol, StringComparison.OrdinalIgnoreCase)
                || i.PartIdDisplay.Contains(partIdCol, StringComparison.OrdinalIgnoreCase));
        }

        var quantityCol = ColumnFilterQuantity?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(quantityCol))
        {
            items = items.Where(i =>
                i.Quantity.ToString().Contains(quantityCol, StringComparison.OrdinalIgnoreCase));
        }

        var categoryCol = ColumnFilterCategory?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(categoryCol))
        {
            items = items.Where(i =>
                i.Category.Contains(categoryCol, StringComparison.OrdinalIgnoreCase));
        }

        var statusCol = ColumnFilterStatus?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(statusCol))
        {
            items = items.Where(i =>
                i.Status.Contains(statusCol, StringComparison.OrdinalIgnoreCase));
        }

        var partNameFilterActive = !string.IsNullOrEmpty(partNameCol);
        items = partNameFilterActive
            ? items
                .OrderByDescending(i => i.PartName.StartsWith(partNameCol, StringComparison.OrdinalIgnoreCase))
                .ThenBy(i => i.PartName, StringComparer.OrdinalIgnoreCase)
            : items
                .OrderBy(i => PartIdSortKey(i.PartId), StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.PartName, StringComparer.OrdinalIgnoreCase);

        _filteredInventory.Clear();
        foreach (var item in items)
        {
            _filteredInventory.Add(item);
        }

        CommitFilterPage(_filteredInventory, FilteredInventoryItems);
    }

    private static string PartIdSortKey(string partId)
    {
        var trimmed = partId?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(trimmed) ? "\uFFFF" : trimmed;
    }

    private static string ComputeStatus(int quantity, int minStock) =>
        quantity == 0 ? "Out of Stock"
        : quantity < minStock ? "Low Stock"
        : "In Stock";

    private static InventoryItem MapDocToItem(InventoryDoc doc)
    {
        var minStock = doc.MinStock > 0 ? doc.MinStock : 10;
        var qty = doc.Quantity;
        return new InventoryItem
        {
            Id = doc.MongoId.ToString(),
            PartId = string.IsNullOrWhiteSpace(doc.PartId) ? string.Empty : doc.PartId.Trim(),
            PartName = string.IsNullOrWhiteSpace(doc.PartName) ? "(No name)" : doc.PartName,
            Category = string.IsNullOrWhiteSpace(doc.Category) ? "Uncategorized" : doc.Category.Trim(),
            Quantity = qty,
            MinStock = minStock,
            UnitPrice = doc.UnitPrice,
            TotalValue = qty * doc.UnitPrice,
            Location = doc.Location ?? string.Empty,
            Supplier = doc.Supplier ?? string.Empty,
            Status = ComputeStatus(qty, minStock),
            LastUpdated = doc.LastUpdated,
        };
    }

    public async Task<bool> AddStockAsync(string partId, int quantityToAdd, CancellationToken ct = default)
    {
        var item = _allInventory.FirstOrDefault(x => x.Id == partId);
        if (item is null) return false;

        var docs = await _inventoryService.GetAllAsync(ct).ConfigureAwait(true);
        var doc = docs.FirstOrDefault(d => d.MongoId.ToString() == partId);
        if (doc is null) return false;

        doc.Quantity += quantityToAdd;
        doc.LastUpdated = DateTime.UtcNow;
        await _inventoryService.UpsertAsync(doc, ct).ConfigureAwait(true);

        item.Quantity = doc.Quantity;
        item.TotalValue = item.Quantity * item.UnitPrice;
        item.Status = ComputeStatus(item.Quantity, item.MinStock);
        item.LastUpdated = doc.LastUpdated;

        RecomputeAnalytics();
        ApplyFilters();
        StatusMessage = $"Added {quantityToAdd} units to {item.PartName}";
        _activityLogger.LogStockChange("Stock Added",
            $"Added {quantityToAdd} units to \"{item.PartName}\" (new qty: {item.Quantity})");
        return true;
    }

    public async Task<bool> UpdateStockAsync(string partId, int newQuantity, CancellationToken ct = default)
    {
        var item = _allInventory.FirstOrDefault(x => x.Id == partId);
        if (item is null) return false;

        var docs = await _inventoryService.GetAllAsync(ct).ConfigureAwait(true);
        var doc = docs.FirstOrDefault(d => d.MongoId.ToString() == partId);
        if (doc is null) return false;

        doc.Quantity = newQuantity;
        doc.LastUpdated = DateTime.UtcNow;
        await _inventoryService.UpsertAsync(doc, ct).ConfigureAwait(true);

        item.Quantity = newQuantity;
        item.TotalValue = item.Quantity * item.UnitPrice;
        item.Status = ComputeStatus(item.Quantity, item.MinStock);
        item.LastUpdated = doc.LastUpdated;

        RecomputeAnalytics();
        ApplyFilters();
        StatusMessage = $"Updated {item.PartName} to {newQuantity} units";
        _activityLogger.LogStockChange("Stock Updated",
            $"Updated \"{item.PartName}\" to {newQuantity} units");
        return true;
    }

    public async Task<bool> DeleteItemAsync(string mongoId, CancellationToken ct = default)
    {
        if (!ShowInventoryAdminChrome)
        {
            return false;
        }

        await _inventoryService.DeleteAsync(mongoId, ct).ConfigureAwait(true);
        var item = _allInventory.FirstOrDefault(i => i.Id == mongoId);
        if (item is not null)
        {
            _allInventory.Remove(item);
            _activityLogger.LogStockChange("Item Deleted", $"Removed \"{item.PartName}\" from inventory");
        }

        RecomputeAnalytics();
        RefreshCategoryFilterOptions();
        ApplyFilters();
        return true;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectedCategoryFilter = "All categories";
        SelectedStatusFilter = "All statuses";
        ColumnFilterPartName = string.Empty;
        ColumnFilterPartId = string.Empty;
        ColumnFilterQuantity = string.Empty;
        ColumnFilterCategory = string.Empty;
        ColumnFilterStatus = string.Empty;
        ShowPartNameColumnSearch = false;
        ShowPartIdColumnSearch = false;
        ShowQuantityColumnSearch = false;
        ShowCategoryColumnSearch = false;
        ShowStatusColumnSearch = false;
        ApplyFilters();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadInventoryAsync();

    public async Task<BulkInventoryImportResult> ImportBulkFromFileAsync(
        string filePath,
        IProgress<BulkImportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!ShowInventoryAdminChrome)
        {
            return new BulkInventoryImportResult
            {
                Errors = ["You do not have permission to import inventory."],
            };
        }

        return await _bulkImport
            .ImportFromFileAsync(filePath, progress, cancellationToken)
            .ConfigureAwait(true);
    }
}

public sealed partial class InventoryItem : ObservableObject
{
    public required string Id { get; init; }
    public required string PartId { get; init; }
    public required string PartName { get; init; }
    public required string Category { get; init; }
    public required string Location { get; init; }
    public required string Supplier { get; init; }

    [ObservableProperty] private int _quantity;
    [ObservableProperty] private int _minStock;
    [ObservableProperty] private double _unitPrice;
    [ObservableProperty] private double _totalValue;
    [ObservableProperty] private string _status = "In Stock";
    [ObservableProperty] private DateTime _lastUpdated;

    public string PartIdDisplay => string.IsNullOrWhiteSpace(PartId) ? "—" : PartId;

    public string StatusColor => Status switch
    {
        "Out of Stock" => "#FF5F57",
        "Low Stock"    => "#FFB781",
        _              => "#2ABD8F"
    };

    public string StatusBackgroundColor => Status switch
    {
        "Out of Stock" => "#25FF5F57",
        "Low Stock"    => "#25FFB781",
        _              => "#252ABD8F"
    };

    public string StatusIcon => Status switch
    {
        "Out of Stock" => "\uEA39",
        "Low Stock"    => "\uE7BA",
        _              => "\uE73E"
    };

    public string FormattedPrice => $"${UnitPrice:N2}";
    public string FormattedValue => $"${TotalValue:N2}";
    public string FormattedLastUpdated => LastUpdated.ToString("MMM dd, yyyy");
}
