using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; }

    private readonly MongoInventoryService _inventoryService;
    private readonly MongoAuditService _auditService;
    private readonly AdminSessionStore _session;

    public InventoryPage()
    {
        ViewModel = App.Services.GetRequiredService<InventoryViewModel>();
        _inventoryService = App.Services.GetRequiredService<MongoInventoryService>();
        _auditService = App.Services.GetRequiredService<MongoAuditService>();
        _session = App.Services.GetRequiredService<AdminSessionStore>();
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadInventoryAsync();
    }

    private async void OnAddStockClick(object sender, RoutedEventArgs e)
    {
        await ShowAddNewItemDialogAsync();
    }

    private async void OnEditInventoryClick(object sender, RoutedEventArgs e)
    {
        // Get the clicked item from button's DataContext
        if (sender is Button btn && btn.DataContext is InventoryItem item)
        {
            await ShowEditItemDialogAsync(item);
        }
    }

    // ── Add New Inventory Item ────────────────────────────────────────────────
    private async Task ShowAddNewItemDialogAsync()
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 440 };

        var partNameBox  = AddField(content, "PART NAME *", "e.g. Turbocharger Assembly");
        var partIdBox    = AddField(content, "PART ID", "e.g. TRB-2024-001");
        var categoryBox  = AddField(content, "CATEGORY", "e.g. Engine Parts");
        var supplierBox  = AddField(content, "SUPPLIER", "e.g. Global Parts Inc");
        var locationBox  = AddField(content, "LOCATION", "e.g. Warehouse A, Shelf 3");
        var qtyBox       = AddField(content, "QUANTITY *", "e.g. 100");
        var minStockBox  = AddField(content, "MIN STOCK THRESHOLD", "e.g. 10");
        var unitPriceBox = AddField(content, "UNIT PRICE ($)", "e.g. 1250.00");
        var notesBox     = AddField(content, "NOTES (OPTIONAL)", "Any additional notes...", multiline: true);

        var errorText = new TextBlock
        {
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["DangerBrush"],
            FontSize = 12,
            Visibility = Visibility.Collapsed,
        };
        content.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = "Add New Inventory Item",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new ScrollViewer { Content = content, MaxHeight = 520 },
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        // Validate
        var partName = partNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(partName))
        {
            await ShowErrorAsync("Part name is required.");
            return;
        }

        if (!int.TryParse(qtyBox.Text.Trim(), out var qty) || qty < 0)
        {
            await ShowErrorAsync("Quantity must be a valid non-negative number.");
            return;
        }

        int.TryParse(minStockBox.Text.Trim(), out var minStock);
        double.TryParse(unitPriceBox.Text.Trim(), out var unitPrice);

        // Save to MongoDB
        var doc = new InventoryDoc
        {
            PartName  = partName,
            PartId    = partIdBox.Text.Trim(),
            Category  = categoryBox.Text.Trim(),
            Supplier  = supplierBox.Text.Trim(),
            Location  = locationBox.Text.Trim(),
            Quantity  = qty,
            MinStock  = minStock > 0 ? minStock : 10,
            UnitPrice = unitPrice,
            Notes     = notesBox.Text.Trim(),
            LastUpdated = DateTime.UtcNow,
        };

        try
        {
            await _inventoryService.UpsertAsync(doc).ConfigureAwait(true);

            // Log audit event
            _session.Load();
            await _auditService.LogAsync(new AuditDoc
            {
                EventType = "Stock Change",
                Action    = "Item Added",
                Details   = $"{partName} — {qty} units added to inventory",
                User      = _session.Email ?? "Admin",
                Timestamp = DateTime.UtcNow,
            }).ConfigureAwait(true);

            // Refresh list
            await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            await ShowSuccessAsync("Item Added", $"{partName} ({qty} units) saved to inventory.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to save: {ex.Message}");
        }
    }

    // ── Edit Existing Inventory Item ─────────────────────────────────────────
    private async Task ShowEditItemDialogAsync(InventoryItem item)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 440 };

        var partNameBox  = AddField(content, "PART NAME", item.PartName, readOnly: true);
        var qtyBox       = AddField(content, "NEW QUANTITY *", item.Quantity.ToString());
        var minStockBox  = AddField(content, "MIN STOCK THRESHOLD", item.MinStock.ToString());
        var unitPriceBox = AddField(content, "UNIT PRICE ($)", item.UnitPrice.ToString("F2"));
        var locationBox  = AddField(content, "LOCATION", item.Location);
        var supplierBox  = AddField(content, "SUPPLIER", item.Supplier);
        var notesBox     = AddField(content, "NOTES", string.Empty, multiline: true);

        var dialog = new ContentDialog
        {
            Title = $"Edit: {item.PartName}",
            PrimaryButtonText = "Update",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = new ScrollViewer { Content = content, MaxHeight = 480 },
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        if (!int.TryParse(qtyBox.Text.Trim(), out var newQty) || newQty < 0)
        {
            await ShowErrorAsync("Quantity must be a valid non-negative number.");
            return;
        }

        int.TryParse(minStockBox.Text.Trim(), out var newMinStock);
        double.TryParse(unitPriceBox.Text.Trim(), out var newUnitPrice);

        // Load the original doc from DB to update it
        var allDocs = await _inventoryService.GetAllAsync().ConfigureAwait(true);
        var doc = allDocs.FirstOrDefault(d => d.MongoId.ToString() == item.Id);
        if (doc is null)
        {
            await ShowErrorAsync("Item not found in database.");
            return;
        }

        var oldQty = doc.Quantity;
        doc.Quantity    = newQty;
        doc.MinStock    = newMinStock > 0 ? newMinStock : doc.MinStock;
        doc.UnitPrice   = newUnitPrice > 0 ? newUnitPrice : doc.UnitPrice;
        doc.Location    = string.IsNullOrWhiteSpace(locationBox.Text) ? doc.Location : locationBox.Text.Trim();
        doc.Supplier    = string.IsNullOrWhiteSpace(supplierBox.Text) ? doc.Supplier : supplierBox.Text.Trim();
        doc.LastUpdated = DateTime.UtcNow;

        try
        {
            await _inventoryService.UpsertAsync(doc).ConfigureAwait(true);

            // Log audit
            _session.Load();
            var change = newQty > oldQty ? $"+{newQty - oldQty}" : $"{newQty - oldQty}";
            await _auditService.LogAsync(new AuditDoc
            {
                EventType = "Stock Change",
                Action    = "Stock Updated",
                Details   = $"{item.PartName} — {oldQty} → {newQty} units ({change})",
                User      = _session.Email ?? "Admin",
                Timestamp = DateTime.UtcNow,
            }).ConfigureAwait(true);

            await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            await ShowSuccessAsync("Updated", $"{item.PartName} updated to {newQty} units.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to update: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private TextBox AddField(
        StackPanel parent,
        string label,
        string value = "",
        bool readOnly = false,
        bool multiline = false)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"],
        });

        var box = new TextBox
        {
            Text = value,
            FontSize = 13,
            Padding = new Thickness(12, 10, 12, 10),
            IsReadOnly = readOnly,
        };

        if (multiline)
        {
            box.AcceptsReturn = true;
            box.TextWrapping = TextWrapping.Wrap;
            box.MinHeight = 70;
        }

        panel.Children.Add(box);
        parent.Children.Add(panel);
        return box;
    }

    private async Task ShowSuccessAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
