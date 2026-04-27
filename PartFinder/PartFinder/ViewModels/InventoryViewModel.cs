using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class InventoryViewModel : ViewModelBase
{
    private readonly MongoInventoryService _inventoryService;
    private readonly ActivityLogger _activityLogger;

    public InventoryViewModel(MongoInventoryService inventoryService, ActivityLogger activityLogger)
    {
        _inventoryService = inventoryService;
        _activityLogger = activityLogger;
    }

    public ObservableCollection<InventoryItem> InventoryItems { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _totalItems;
    [ObservableProperty] private int _lowStockItems;
    [ObservableProperty] private int _outOfStockItems;
    [ObservableProperty] private double _totalValue;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private InventoryItem? _selectedItem;

    public string TotalValueFormatted => TotalValue >= 1_000_000
        ? $"${TotalValue / 1_000_000:F1}M"
        : TotalValue >= 1_000
            ? $"${TotalValue / 1_000:F1}K"
            : $"${TotalValue:N0}";

    public async Task LoadInventoryAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var docs = await _inventoryService.GetAllAsync(cancellationToken).ConfigureAwait(true);

            InventoryItems.Clear();
            TotalItems = 0;
            LowStockItems = 0;
            OutOfStockItems = 0;
            TotalValue = 0;

            foreach (var doc in docs)
            {
                var status = doc.Quantity == 0 ? "Out of Stock"
                           : doc.Quantity < doc.MinStock ? "Low Stock"
                           : "In Stock";

                if (doc.Quantity == 0) OutOfStockItems++;
                else if (doc.Quantity < doc.MinStock) LowStockItems++;

                TotalValue += doc.Quantity * doc.UnitPrice;

                InventoryItems.Add(new InventoryItem
                {
                    Id = doc.MongoId.ToString(),
                    PartName = string.IsNullOrWhiteSpace(doc.PartName) ? "(No name)" : doc.PartName,
                    Category = string.IsNullOrWhiteSpace(doc.Category) ? "Uncategorized" : doc.Category,
                    Quantity = doc.Quantity,
                    MinStock = doc.MinStock,
                    UnitPrice = doc.UnitPrice,
                    TotalValue = doc.Quantity * doc.UnitPrice,
                    Location = doc.Location,
                    Supplier = doc.Supplier,
                    Status = status,
                    LastUpdated = doc.LastUpdated,
                });

                TotalItems++;
            }

            StatusMessage = TotalItems == 0
                ? "No inventory items yet. Add stock to get started."
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

    // Called from code-behind dialogs
    public async Task<bool> AddStockAsync(string partId, int quantityToAdd, CancellationToken ct = default)
    {
        var item = InventoryItems.FirstOrDefault(x => x.Id == partId);
        if (item is null) return false;

        var docs = await _inventoryService.GetAllAsync(ct).ConfigureAwait(true);
        var doc = docs.FirstOrDefault(d => d.MongoId.ToString() == partId);
        if (doc is null) return false;

        doc.Quantity += quantityToAdd;
        doc.LastUpdated = DateTime.UtcNow;
        await _inventoryService.UpsertAsync(doc, ct).ConfigureAwait(true);

        item.Quantity = doc.Quantity;
        item.TotalValue = item.Quantity * item.UnitPrice;
        item.Status = item.Quantity == 0 ? "Out of Stock"
                    : item.Quantity < item.MinStock ? "Low Stock"
                    : "In Stock";
        item.LastUpdated = doc.LastUpdated;

        StatusMessage = $"Added {quantityToAdd} units to {item.PartName}";
        _activityLogger.LogStockChange("Stock Added",
            $"Added {quantityToAdd} units to \"{item.PartName}\" (new qty: {item.Quantity})");
        return true;
    }

    public async Task<bool> UpdateStockAsync(string partId, int newQuantity, CancellationToken ct = default)
    {
        var item = InventoryItems.FirstOrDefault(x => x.Id == partId);
        if (item is null) return false;

        var docs = await _inventoryService.GetAllAsync(ct).ConfigureAwait(true);
        var doc = docs.FirstOrDefault(d => d.MongoId.ToString() == partId);
        if (doc is null) return false;

        doc.Quantity = newQuantity;
        doc.LastUpdated = DateTime.UtcNow;
        await _inventoryService.UpsertAsync(doc, ct).ConfigureAwait(true);

        item.Quantity = newQuantity;
        item.TotalValue = item.Quantity * item.UnitPrice;
        item.Status = item.Quantity == 0 ? "Out of Stock"
                    : item.Quantity < item.MinStock ? "Low Stock"
                    : "In Stock";
        item.LastUpdated = doc.LastUpdated;

        StatusMessage = $"Updated {item.PartName} to {newQuantity} units";
        _activityLogger.LogStockChange("Stock Updated",
            $"Updated \"{item.PartName}\" to {newQuantity} units");
        return true;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadInventoryAsync();
}

public sealed partial class InventoryItem : ObservableObject
{
    public required string Id { get; init; }
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

    public string StatusColor => Status switch
    {
        "Out of Stock" => "#FF5F57",
        "Low Stock"    => "#FFB781",
        _              => "#2ABD8F"
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
