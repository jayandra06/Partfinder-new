using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel
{
    public ObservableCollection<WmsExpectedReceiptRowDto> ExpectedReceiptRows { get; } = new();
    public ObservableCollection<WmsGoodsReceiptRowDto> GoodsReceiptRows { get; } = new();
    public ObservableCollection<WmsQcRowDto> QcRows { get; } = new();
    public ObservableCollection<WmsPutawayRowDto> PutawayRows { get; } = new();
    public ObservableCollection<string> ReceivingHistoryRows { get; } = new();
    public ObservableCollection<WmsReceiptLineRowDto> ReceiptLineRows { get; } = new();
    public ObservableCollection<WmsReceiptLineRowDto> DiscrepancyRows { get; } = new();

    [RelayCommand]
    private async Task ExportCurrentWorksheetAsync()
    {
        if (SelectedSectionId == WmsWorkbookSections.SkuRegistry && SkuRows.Count > 0)
        {
            var path = WmsWorksheetExportService.ExportToTempFile(
                "SKU",
                ["SkuId", "DisplayName", "UOM", "Status"],
                SkuRows.Select(s => new[] { s.SkuId, s.DisplayName, s.PrimaryUom, s.Status }));
            StatusMessage = $"Exported to {path}";
            return;
        }

        if (SelectedSectionId == WmsWorkbookSections.ExpectedReceipts && ExpectedReceiptRows.Count > 0)
        {
            var path = WmsWorksheetExportService.ExportToTempFile(
                "ExpectedReceipts",
                ["ExpectedReceiptId", "Status", "OwnerId", "WarehouseId", "ExternalPoId"],
                ExpectedReceiptRows.Select(r =>
                    new[] { r.ExpectedReceiptId, r.Status, r.OwnerId, r.WarehouseId, r.ExternalPoId ?? string.Empty }));
            StatusMessage = $"Exported to {path}";
            return;
        }

        if (SelectedSectionId == WmsWorkbookSections.GoodsReceiving && GoodsReceiptRows.Count > 0)
        {
            var path = WmsWorksheetExportService.ExportToTempFile(
                "GoodsReceipts",
                ["GoodsReceiptId", "Status", "OwnerId", "WarehouseId", "CreatedBy"],
                GoodsReceiptRows.Select(r =>
                    new[] { r.GoodsReceiptId, r.Status, r.OwnerId, r.WarehouseId, r.CreatedBy }));
            StatusMessage = $"Exported to {path}";
            return;
        }

        if (SelectedSectionId == WmsWorkbookSections.ReceivedItemDetails && ReceiptLineRows.Count > 0)
        {
            var path = WmsWorksheetExportService.ExportToTempFile(
                "ReceiptLines",
                ["ReceiptLineId", "SkuId", "QtyReceived", "QcStatus", "Damaged", "OverDelivery"],
                ReceiptLineRows.Select(r =>
                    new[]
                    {
                        r.ReceiptLineId,
                        r.SkuId,
                        r.QtyReceived.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        r.QcStatus,
                        r.Damaged.ToString(),
                        r.OverDelivery.ToString(),
                    }));
            StatusMessage = $"Exported to {path}";
            return;
        }

        StatusMessage = "Nothing to export for this worksheet.";
        await Task.CompletedTask;
    }

    private async Task ReloadExpectedReceiptsAsync(CancellationToken ct = default)
    {
        ExpectedReceiptRows.Clear();
        var wh = SelectedWarehouse?.Id;
        var result = await _wms.ListExpectedReceiptsAsync(wh, ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        ApplyReceivingSearchOnReload(
            ExpectedReceiptRows,
            result.Items,
            r => $"{r.ExpectedReceiptId} {r.Status} {r.ExternalPoId}");
    }

    private async Task ReloadGoodsReceiptsAsync(CancellationToken ct = default)
    {
        GoodsReceiptRows.Clear();
        ReceiptLineRows.Clear();
        var wh = SelectedWarehouse?.Id;
        var result = await _wms.ListGoodsReceiptsAsync(wh, ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        ApplyReceivingSearchOnReload(
            GoodsReceiptRows,
            result.Items,
            r => $"{r.GoodsReceiptId} {r.Status} {r.CreatedBy}");

        if (GoodsReceiptRows.Count > 0)
        {
            var lines = await _wms.ListReceiptLinesAsync(GoodsReceiptRows[0].GoodsReceiptId, ct)
                .ConfigureAwait(true);
            if (lines.Ok)
            {
                foreach (var l in lines.Items)
                    ReceiptLineRows.Add(l);
            }
        }
    }

    private async Task ReloadQcAsync(CancellationToken ct = default)
    {
        QcRows.Clear();
        var result = await _wms.ListQcAsync(null, ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        foreach (var row in result.Items)
            QcRows.Add(row);
    }

    private async Task ReloadPutawayAsync(CancellationToken ct = default)
    {
        PutawayRows.Clear();
        var wh = SelectedWarehouse?.Id;
        var result = await _wms.ListPutawayTasksAsync(wh, ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        foreach (var row in result.Items)
            PutawayRows.Add(row);
    }

    private async Task ReloadReceivingHistoryAsync(CancellationToken ct = default)
    {
        ReceivingHistoryRows.Clear();
        var result = await _wms.ListReceivingHistoryAsync(ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        foreach (var row in result.Items)
            ReceivingHistoryRows.Add(row);
    }

    private async Task ReloadDiscrepanciesAsync(CancellationToken ct = default)
    {
        DiscrepancyRows.Clear();
        var result = await _wms.ListReceivingDiscrepanciesAsync(ct).ConfigureAwait(true);
        if (!result.Ok)
        {
            ApplyApiError(result.Error);
            return;
        }

        foreach (var row in result.Items)
            DiscrepancyRows.Add(row);
    }
}