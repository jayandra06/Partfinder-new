using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PartFinder.Views.Pages;

public sealed partial class InventoryPage : Page
{
    public InventoryViewModel ViewModel { get; }

    private readonly MongoInventoryService _inventoryService;
    private readonly MongoAuditService _auditService;
    private readonly AdminSessionStore _session;
    private readonly ActivityLogger _activity;

    private bool _bulkUploadRunning;
    private bool _acceptBulkProgress;
    private int _bulkUploaded;
    private int _bulkUploadTotal;

    public InventoryPage()
    {
        ViewModel = App.Services.GetRequiredService<InventoryViewModel>();
        _inventoryService = App.Services.GetRequiredService<MongoInventoryService>();
        _auditService = App.Services.GetRequiredService<MongoAuditService>();
        _session = App.Services.GetRequiredService<AdminSessionStore>();
        _activity = App.Services.GetRequiredService<ActivityLogger>();
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var access = App.Services.GetRequiredService<ICurrentUserAccessService>();
        await access.RefreshAsync().ConfigureAwait(true);
        ViewModel.RefreshRoleChrome();

        if (ViewModel.FormMode == InventoryFormMode.None)
        {
            await ViewModel.LoadInventoryAsync();
        }
    }

    private void OnAddStockClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ShowInventoryAdminChrome)
        {
            return;
        }

        ItemFormPanel.PrepareForAdd();
        ViewModel.BeginAddForm();
    }

    private void OnEditInventoryClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ShowInventoryAdminChrome)
        {
            return;
        }

        if (sender is not Button btn)
        {
            return;
        }

        var item = btn.Tag as InventoryItem ?? btn.DataContext as InventoryItem;
        if (item is null)
        {
            return;
        }

        ItemFormPanel.PrepareForEdit(item);
        ViewModel.BeginEditForm(item);
    }

    private void OnCancelFormClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseForm();
    }

    private async void OnSaveFormClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.ShowInventoryAdminChrome)
        {
            return;
        }

        ViewModel.FormStatusMessage = string.Empty;

        if (ViewModel.FormMode == InventoryFormMode.Add)
        {
            await SaveNewItemAsync();
        }
        else if (ViewModel.FormMode == InventoryFormMode.Edit && ViewModel.SelectedItem is not null)
        {
            await SaveEditedItemAsync(ViewModel.SelectedItem);
        }
    }

    private async Task SaveNewItemAsync()
    {
        var partName = ItemFormPanel.PartName;
        if (string.IsNullOrWhiteSpace(partName))
        {
            ViewModel.FormStatusMessage = "Part name is required.";
            return;
        }

        if (!int.TryParse(ItemFormPanel.QuantityText, out var qty) || qty < 0)
        {
            ViewModel.FormStatusMessage = "Quantity must be a valid non-negative number.";
            return;
        }

        int.TryParse(ItemFormPanel.MinStockText, out var minStock);
        double.TryParse(ItemFormPanel.UnitPriceText, out var unitPrice);

        var doc = new InventoryDoc
        {
            PartName = partName,
            PartId = ItemFormPanel.PartId,
            Category = ItemFormPanel.Category,
            Supplier = ItemFormPanel.Supplier,
            Location = ItemFormPanel.Location,
            Quantity = qty,
            MinStock = minStock > 0 ? minStock : 10,
            UnitPrice = unitPrice,
            Notes = ItemFormPanel.Notes,
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

            ViewModel.CloseForm();
            await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            ViewModel.StatusMessage = $"{partName} ({qty} units) saved to inventory.";
        }
        catch (Exception ex)
        {
            ViewModel.FormStatusMessage = $"Failed to save: {ex.Message}";
        }
    }

    private async Task SaveEditedItemAsync(InventoryItem item)
    {
        if (!int.TryParse(ItemFormPanel.QuantityText, out var newQty) || newQty < 0)
        {
            ViewModel.FormStatusMessage = "Quantity must be a valid non-negative number.";
            return;
        }

        int.TryParse(ItemFormPanel.MinStockText, out var newMinStock);
        double.TryParse(ItemFormPanel.UnitPriceText, out var newUnitPrice);

        var allDocs = await _inventoryService.GetAllAsync().ConfigureAwait(true);
        var doc = allDocs.FirstOrDefault(d => d.MongoId.ToString() == item.Id);
        if (doc is null)
        {
            ViewModel.FormStatusMessage = "Item not found in database.";
            return;
        }

        var partName = ItemFormPanel.PartName?.Trim();
        if (!string.IsNullOrWhiteSpace(partName))
        {
            doc.PartName = partName;
        }

        doc.PartId = ItemFormPanel.PartId?.Trim() ?? doc.PartId;
        var category = ItemFormPanel.Category?.Trim();
        if (!string.IsNullOrWhiteSpace(category))
        {
            doc.Category = category;
        }

        var oldQty = doc.Quantity;
        doc.Quantity = newQty;
        doc.MinStock = newMinStock > 0 ? newMinStock : doc.MinStock;
        doc.UnitPrice = newUnitPrice > 0 ? newUnitPrice : doc.UnitPrice;
        doc.Location = string.IsNullOrWhiteSpace(ItemFormPanel.Location) ? doc.Location : ItemFormPanel.Location;
        doc.Supplier = string.IsNullOrWhiteSpace(ItemFormPanel.Supplier) ? doc.Supplier : ItemFormPanel.Supplier;
        doc.Notes = ItemFormPanel.Notes ?? doc.Notes;
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

            ViewModel.CloseForm();
            await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            ViewModel.StatusMessage = $"{item.PartName} updated to {newQty} units.";
        }
        catch (Exception ex)
        {
            ViewModel.FormStatusMessage = $"Failed to update: {ex.Message}";
        }
    }

    private async void OnBulkUploadClick(object sender, RoutedEventArgs e)
    {
        if (_bulkUploadRunning || XamlRoot is null || App.MainAppWindow is null)
        {
            return;
        }

        _bulkUploadRunning = true;
        BulkUploadButton.IsEnabled = false;
        _acceptBulkProgress = false;
        _bulkUploaded = 0;
        _bulkUploadTotal = 0;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xlsm");
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

        string? tempPath = null;
        try
        {
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            SetImportStatus("Reading file…", file.Name, isActive: true);
            var path = await ResolvePickerFilePathAsync(file).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetImportStatus("Import failed.", "Could not access the selected file.", isActive: false);
                await ShowBulkImportDialogAsync("Could not access the selected file.", XamlRoot).ConfigureAwait(true);
                return;
            }

            tempPath = path != file.Path ? path : null;
            _acceptBulkProgress = true;
            var progress = new Progress<BulkImportProgress>(ApplyBulkUploadProgress);
            var result = await ViewModel.ImportBulkFromFileAsync(path, progress).ConfigureAwait(true);
            _acceptBulkProgress = false;

            var uploaded = result.TotalCount > 0 ? result.UploadedCount : _bulkUploaded;
            var total = result.TotalCount > 0 ? result.TotalCount : _bulkUploadTotal;
            var percent = total <= 0
                ? 0
                : (int)Math.Clamp(Math.Round(uploaded * 100.0 / total, MidpointRounding.AwayFromZero), 0, 100);

            var summaryParts = new List<string>();
            if (total > 0)
            {
                summaryParts.Add($"{uploaded}/{total} is uploaded ({percent}%).");
            }

            if (result.InsertedCount > 0)
            {
                summaryParts.Add($"Added {result.InsertedCount} new item(s).");
            }

            if (result.UpdatedCount > 0)
            {
                summaryParts.Add($"Updated {result.UpdatedCount} existing item(s).");
            }

            if (result.SkippedCount > 0)
            {
                summaryParts.Add($"Skipped {result.SkippedCount} row(s).");
            }

            var summary = summaryParts.Count > 0
                ? string.Join(" ", summaryParts)
                : "No inventory rows were imported.";

            if (result.Errors.Count > 0)
            {
                var detail = string.Join("\n", result.Errors.Take(8));
                if (result.Errors.Count > 8)
                {
                    detail += $"\n…and {result.Errors.Count - 8} more.";
                }

                SetImportStatus(
                    "Import finished with notes.",
                    summary,
                    isActive: false,
                    completed: total > 0 ? uploaded : null,
                    total: total > 0 ? total : null);
                await ShowBulkImportDialogAsync($"{summary}\n\n{detail}", XamlRoot).ConfigureAwait(true);
            }
            else
            {
                SetImportStatus(
                    "Import complete.",
                    summary,
                    isActive: false,
                    completed: total > 0 ? uploaded : null,
                    total: total > 0 ? total : null);
                await ShowBulkImportDialogAsync(summary, XamlRoot).ConfigureAwait(true);
            }

            if (result.HasWork)
            {
                _activity.LogUserAction(
                    "Inventory Bulk Upload",
                    summary);
                await ViewModel.LoadInventoryAsync().ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            SetImportStatus(
                "Import failed.",
                ex.Message,
                isActive: false,
                completed: _bulkUploadTotal > 0 ? _bulkUploaded : null,
                total: _bulkUploadTotal > 0 ? _bulkUploadTotal : null);
            await ShowBulkImportDialogAsync($"Import failed:\n{ex.Message}", XamlRoot).ConfigureAwait(true);
        }
        finally
        {
            _acceptBulkProgress = false;
            SetImportStatus(null);
            BulkUploadButton.ClearValue(Control.IsEnabledProperty);
            _bulkUploadRunning = false;
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort temp cleanup.
                }
            }
        }
    }

    private void ApplyBulkUploadProgress(BulkImportProgress progress)
    {
        void Apply()
        {
            if (!_acceptBulkProgress)
            {
                return;
            }

            _bulkUploaded = progress.Completed;
            _bulkUploadTotal = progress.Total;
            var headline = progress.Total > 0 ? $"Uploading… {progress.Percent}%" : "Uploading…";
            SetImportStatus(headline, progress.Detail, isActive: true, completed: progress.Completed, total: progress.Total);
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Apply();
            return;
        }

        DispatcherQueue.TryEnqueue(Apply);
    }

    private static async Task ShowBulkImportDialogAsync(string message, XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            Title = "Bulk upload",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
            XamlRoot = xamlRoot,
        };
        _ = await dialog.ShowAsync();
    }

    private static async Task<string?> ResolvePickerFilePathAsync(Windows.Storage.StorageFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
        {
            return file.Path;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "PartFinderBulkImport");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_{file.Name}");
        using (var source = await file.OpenStreamForReadAsync().ConfigureAwait(true))
        await using (var dest = File.Create(tempPath))
        {
            await source.CopyToAsync(dest).ConfigureAwait(true);
        }

        return tempPath;
    }

    private void SetImportStatus(
        string? headline,
        string? detail = null,
        bool isActive = false,
        int? completed = null,
        int? total = null)
    {
        if (ImportStatusPanel is null || ImportProgressRing is null || ImportStatusText is null || ImportStatusDetailText is null
            || ImportProgressBar is null || ImportPercentText is null || ImportCountText is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            ImportStatusPanel.Visibility = Visibility.Collapsed;
            ImportProgressRing.IsActive = false;
            ImportProgressRing.Visibility = Visibility.Collapsed;
            ImportProgressBar.Visibility = Visibility.Collapsed;
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Value = 0;
            ImportPercentText.Visibility = Visibility.Collapsed;
            ImportPercentText.Text = string.Empty;
            ImportCountText.Visibility = Visibility.Collapsed;
            ImportCountText.Text = string.Empty;
            ImportStatusText.Text = string.Empty;
            ImportStatusDetailText.Text = string.Empty;
            return;
        }

        var hasCount = completed is int && total is int overall && overall > 0;
        var percent = hasCount ? FormatUploadPercent(completed!.Value, total!.Value) : 0;

        ImportStatusPanel.Visibility = Visibility.Visible;
        ImportStatusText.Text = headline;
        ImportStatusDetailText.Text = detail ?? string.Empty;

        if (hasCount)
        {
            ImportProgressRing.IsActive = false;
            ImportProgressRing.Visibility = Visibility.Collapsed;
            ImportProgressBar.Visibility = Visibility.Visible;
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Value = percent;
            ImportPercentText.Visibility = Visibility.Visible;
            ImportPercentText.Text = $"{percent}%";
            ImportCountText.Visibility = Visibility.Visible;
            ImportCountText.Text = $"{completed}/{total} is uploaded";
            return;
        }

        ImportProgressBar.Visibility = Visibility.Collapsed;
        ImportProgressBar.Value = 0;
        ImportPercentText.Visibility = Visibility.Collapsed;
        ImportCountText.Visibility = Visibility.Collapsed;
        ImportProgressRing.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        ImportProgressRing.IsActive = isActive;
    }

    private static int FormatUploadPercent(int completed, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero), 0, 100);
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var items = ViewModel.GetAllItemsForExport();
        if (items.Count == 0)
        {
            await ShowSimpleDialogAsync("Nothing to export", "There are no inventory items to export.");
            return;
        }

        if (XamlRoot is null)
        {
            return;
        }

        try
        {
            var fileName = $"PartFinder_Inventory_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            var downloadsDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");
            Directory.CreateDirectory(downloadsDir);

            var savePath = Path.Combine(downloadsDir, fileName);
            if (File.Exists(savePath))
            {
                savePath = Path.Combine(
                    downloadsDir,
                    $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:HHmmss}.xlsx");
            }

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Inventory");
            var headers = new[]
            {
                "Part Name", "Part ID", "Category", "Quantity", "Min Stock", "Unit Price", "Total Value",
                "Location", "Supplier", "Status", "Last Updated (UTC)",
            };
            for (var c = 0; c < headers.Length; c++)
            {
                sheet.Cell(1, c + 1).Value = headers[c];
            }

            var row = 2;
            foreach (var item in items.OrderBy(i => i.PartId, StringComparer.OrdinalIgnoreCase))
            {
                sheet.Cell(row, 1).Value = item.PartName;
                sheet.Cell(row, 2).Value = string.IsNullOrWhiteSpace(item.PartId) ? item.Id : item.PartId;
                sheet.Cell(row, 3).Value = item.Category;
                sheet.Cell(row, 4).Value = item.Quantity;
                sheet.Cell(row, 5).Value = item.MinStock;
                sheet.Cell(row, 6).Value = item.UnitPrice;
                sheet.Cell(row, 7).Value = item.TotalValue;
                sheet.Cell(row, 8).Value = item.Location;
                sheet.Cell(row, 9).Value = item.Supplier;
                sheet.Cell(row, 10).Value = item.Status;
                sheet.Cell(row, 11).Value = item.LastUpdated;
                row++;
            }

            sheet.Columns().AdjustToContents();
            workbook.SaveAs(savePath);

            _activity.LogUserAction("Inventory Export", $"Exported {items.Count} row(s) to {Path.GetFileName(savePath)}");
            await ShowSimpleDialogAsync("Export complete", $"Saved to:\n{savePath}");
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync("Export failed", ex.Message);
        }
    }

    private void OnInventoryRowActionsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        var item = btn.Tag as InventoryItem ?? btn.DataContext as InventoryItem;
        if (item is null)
        {
            return;
        }

        var flyout = new MenuFlyout { ShouldConstrainToRootBounds = false };

        var edit = new MenuFlyoutItem { Text = "Edit" };
        edit.Click += (_, _) =>
        {
            flyout.Hide();
            EnqueueAfterFlyoutClosed(() =>
            {
                ItemFormPanel.PrepareForEdit(item);
                ViewModel.BeginEditForm(item);
            });
        };
        flyout.Items.Add(edit);

        var delete = new MenuFlyoutItem { Text = "Delete" };
        if (Application.Current.Resources.TryGetValue("DangerBrush", out var dangerObj) && dangerObj is Brush dangerBrush)
        {
            delete.Foreground = dangerBrush;
        }

        delete.Click += (_, _) =>
        {
            flyout.Hide();
            EnqueueAfterFlyoutClosed(() => _ = ConfirmDeleteSingleItemAsync(item));
        };
        flyout.Items.Add(delete);

        flyout.ShowAt(btn, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
    }

    private async Task ConfirmDeleteSingleItemAsync(InventoryItem item)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete item",
            Content = $"Delete \"{item.PartName}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _session.Load();
        await ViewModel.DeleteItemAsync(item.Id).ConfigureAwait(true);
        await _auditService.LogAsync(new AuditDoc
        {
            EventType = "Stock Change",
            Action = "Item Deleted",
            Details = $"{item.PartName} removed from inventory",
            User = _session.Email ?? "Admin",
            Timestamp = DateTime.UtcNow,
        }).ConfigureAwait(true);
        ViewModel.StatusMessage = $"Deleted {item.PartName}.";
    }

    private void EnqueueAfterFlyoutClosed(Action action)
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            action();
            return;
        }

        _ = dq.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    private async Task ShowSimpleDialogAsync(string title, string message)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        _ = await dialog.ShowAsync();
    }
}
