using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Services;
using PartFinder.ViewModels;
using PartFinder.Views.Components;

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
        if (sender is Button btn && btn.DataContext is InventoryItem item)
        {
            await ShowEditItemDialogAsync(item);
        }
    }

    private XamlRoot? ResolveDialogXamlRoot()
    {
        if (XamlRoot is not null)
        {
            return XamlRoot;
        }

        if (App.MainAppWindow?.Content is FrameworkElement rootFe)
        {
            return rootFe.XamlRoot;
        }

        return null;
    }

    private async Task ShowAddNewItemDialogAsync()
    {
        var xamlRoot = ResolveDialogXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var form = new InventoryItemFormPanel();
        form.PrepareForAdd();

        var dialog = new ContentDialog
        {
            Title = "Add New Inventory Item",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Content = form,
            RequestedTheme = ElementTheme.Light,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        var partName = form.PartName;
        if (string.IsNullOrWhiteSpace(partName))
        {
            await ShowErrorAsync("Part name is required.");
            return;
        }

        if (!int.TryParse(form.QuantityText, out var qty) || qty < 0)
        {
            await ShowErrorAsync("Quantity must be a valid non-negative number.");
            return;
        }

        int.TryParse(form.MinStockText, out var minStock);
        double.TryParse(form.UnitPriceText, out var unitPrice);

        var doc = new InventoryDoc
        {
            PartName = partName,
            PartId = form.PartId,
            Category = form.Category,
            Supplier = form.Supplier,
            Location = form.Location,
            Quantity = qty,
            MinStock = minStock > 0 ? minStock : 10,
            UnitPrice = unitPrice,
            Notes = form.Notes,
            LastUpdated = DateTime.UtcNow,
        };

        try
        {
            await _inventoryService.UpsertAsync(doc).ConfigureAwait(true);

            _session.Load();
            await _auditService.LogAsync(new AuditDoc
            {
                EventType = "Stock Change",
                Action = "Item Added",
                Details = $"{partName} — {qty} units added to inventory",
                User = _session.Email ?? "Admin",
                Timestamp = DateTime.UtcNow,
            }).ConfigureAwait(true);

            await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            await ShowSuccessAsync("Item Added", $"{partName} ({qty} units) saved to inventory.");
        }
        catch (Exception ex)
        {
            await ShowErrorAsync($"Failed to save: {ex.Message}");
        }
    }

    private async Task ShowEditItemDialogAsync(InventoryItem item)
    {
        var xamlRoot = ResolveDialogXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var form = new InventoryItemFormPanel();
        form.PrepareForEdit(item);

        var dialog = new ContentDialog
        {
            Title = $"Edit: {item.PartName}",
            PrimaryButtonText = "Update",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            Content = form,
            RequestedTheme = ElementTheme.Light,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (!int.TryParse(form.QuantityText, out var newQty) || newQty < 0)
        {
            await ShowErrorAsync("Quantity must be a valid non-negative number.");
            return;
        }

        int.TryParse(form.MinStockText, out var newMinStock);
        double.TryParse(form.UnitPriceText, out var newUnitPrice);

        var allDocs = await _inventoryService.GetAllAsync().ConfigureAwait(true);
        var doc = allDocs.FirstOrDefault(d => d.MongoId.ToString() == item.Id);
        if (doc is null)
        {
            await ShowErrorAsync("Item not found in database.");
            return;
        }

        var oldQty = doc.Quantity;
        doc.Quantity = newQty;
        doc.MinStock = newMinStock > 0 ? newMinStock : doc.MinStock;
        doc.UnitPrice = newUnitPrice > 0 ? newUnitPrice : doc.UnitPrice;
        doc.Location = string.IsNullOrWhiteSpace(form.Location) ? doc.Location : form.Location;
        doc.Supplier = string.IsNullOrWhiteSpace(form.Supplier) ? doc.Supplier : form.Supplier;
        doc.LastUpdated = DateTime.UtcNow;

        try
        {
            await _inventoryService.UpsertAsync(doc).ConfigureAwait(true);

            _session.Load();
            var change = newQty > oldQty ? $"+{newQty - oldQty}" : $"{newQty - oldQty}";
            await _auditService.LogAsync(new AuditDoc
            {
                EventType = "Stock Change",
                Action = "Stock Updated",
                Details = $"{item.PartName} — {oldQty} → {newQty} units ({change})",
                User = _session.Email ?? "Admin",
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

    private async Task ShowSuccessAsync(string title, string message)
    {
        var xamlRoot = ResolveDialogXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Light,
        };
        await dialog.ShowAsync();
    }

    private async Task ShowErrorAsync(string message)
    {
        var xamlRoot = ResolveDialogXamlRoot();
        if (xamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = new TextBlock { Text = message, FontSize = 13, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot,
            RequestedTheme = ElementTheme.Light,
        };
        await dialog.ShowAsync();
    }
}
