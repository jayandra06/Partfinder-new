using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder;
using ClosedXML.Excel;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel
{
    [RelayCommand]
    private async Task LoadSessionProfileAsync()
    {
        var profile = await _wms.GetSessionProfileAsync().ConfigureAwait(true);
        if (!profile.Ok || profile.Data is null) return;
        CanOperateReceiving = profile.Data.IsWmsAdmin || profile.Data.OperatorPermissions.Receiving;
        CanOperateQc = profile.Data.IsWmsAdmin || profile.Data.OperatorPermissions.Qc;
        CanOperatePutaway = profile.Data.IsWmsAdmin || profile.Data.OperatorPermissions.Putaway;
        CanManageOperatorGrants = profile.Data.IsWmsAdmin || _access.IsOrganizationAdministrator;
        CanRecordStagingPreflight = profile.Data.IsWmsAdmin || _access.IsOrganizationAdministrator;
    }

    [RelayCommand]
    private void AddExpectedLineDraftRow()
    {
        ExpectedLineDrafts.Add(new WmsExpectedLineDraftVm { PrimaryUom = "EA" });
    }

    [RelayCommand]
    private void RemoveExpectedLineDraftRow(WmsExpectedLineDraftVm? row)
    {
        if (row is not null) ExpectedLineDrafts.Remove(row);
    }

    [RelayCommand]
    private async Task SaveExpectedReceiptAsync()
    {
        if (!CanOperateReceiving || SelectedWarehouse is null || DraftReceiptOwner is null)
        {
            StatusMessage = "Select warehouse, stock owner, and ensure receiving permission.";
            return;
        }

        IsBusy = true;
        try
        {
            var create = await _wms.CreateExpectedReceiptAsync(new CreateWmsExpectedReceiptRequest
            {
                OwnerId = DraftReceiptOwner.Id,
                WarehouseId = SelectedWarehouse.Id,
                ExternalPoId = DraftExternalPoId,
                SupplierId = DraftSupplierId,
                SupplierDeliveryNote = DraftSupplierNote,
            }).ConfigureAwait(true);
            if (!create.Ok || create.Data is null)
            {
                ApplyApiError(create.Error);
                return;
            }

            if (ExpectedLineDrafts.Count > 0)
            {
                var lines = await _wms.AddExpectedReceiptLinesAsync(create.Data.ExpectedReceiptId, new AddWmsExpectedLinesRequest
                {
                    Lines = ExpectedLineDrafts.Select(d => new WmsExpectedLineInput
                    {
                        SkuId = d.SkuId.Trim(),
                        QtyExpected = d.QtyExpected,
                        PrimaryUom = d.PrimaryUom,
                    }).ToArray(),
                }).ConfigureAwait(true);
                if (!lines.Ok)
                {
                    ApplyApiError(lines.Error);
                    return;
                }
            }

            ExpectedLineDrafts.Clear();
            DraftExternalPoId = string.Empty;
            DraftSupplierId = string.Empty;
            DraftSupplierNote = string.Empty;
            StatusMessage = $"Expected receipt {create.Data.ExpectedReceiptId} saved.";
            await ReloadExpectedReceiptsAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportExpectedLinesFromExcelAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        if (App.MainAppWindow is not MainWindow main) return;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(main);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xlsm");
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        using var stream = await file.OpenStreamForReadAsync().ConfigureAwait(true);
        using var book = new XLWorkbook(stream);
        var sheet = book.Worksheets.First();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            var sku = row.Cell(1).GetString().Trim();
            if (string.IsNullOrWhiteSpace(sku)) continue;
            var qty = row.Cell(2).TryGetValue(out double q) ? q : 0;
            var uom = row.Cell(3).GetString().Trim();
            ExpectedLineDrafts.Add(new WmsExpectedLineDraftVm
            {
                SkuId = sku,
                QtyExpected = qty,
                PrimaryUom = string.IsNullOrWhiteSpace(uom) ? "EA" : uom,
            });
        }

        StatusMessage = $"Imported {ExpectedLineDrafts.Count} draft expected lines (not saved until Save).";
    }

    [RelayCommand]
    private void AddGoodsReceiptLineDraftRow() =>
        GoodsReceiptLineDrafts.Add(new WmsGoodsReceiptLineDraftVm());

    [RelayCommand]
    private async Task ConfirmGoodsReceiptAsync()
    {
        if (!CanOperateReceiving || SelectedWarehouse is null || DraftReceiptOwner is null || DraftReceivingLocation is null)
        {
            StatusMessage = "Warehouse, owner, receiving location and permission are required.";
            return;
        }

        if (GoodsReceiptLineDrafts.Count == 0)
        {
            StatusMessage = "Add at least one receipt line.";
            return;
        }

        IsBusy = true;
        try
        {
            var idem = Guid.NewGuid().ToString("N");
            var result = await _wms.ConfirmGoodsReceiptAsync(new ConfirmWmsGoodsReceiptRequest
            {
                IdempotencyKey = idem,
                OwnerId = DraftReceiptOwner.Id,
                WarehouseId = SelectedWarehouse.Id,
                ReceivingLocationId = DraftReceivingLocation.Id,
                ExpectedReceiptId = SelectedExpectedReceipt?.ExpectedReceiptId,
                ExternalPoId = DraftExternalPoId,
                SupplierDeliveryNote = DraftSupplierNote,
                Lines = GoodsReceiptLineDrafts.Select(l => new WmsGoodsReceiptLineInput
                {
                    SkuId = l.SkuId.Trim(),
                    QtyReceived = l.QtyReceived,
                    LotId = l.LotId,
                    SerialId = l.SerialId,
                    Damaged = l.Damaged,
                    Shortage = l.Shortage,
                    OverDelivery = l.OverDelivery,
                }).ToArray(),
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                ApplyApiError(result.Error);
                return;
            }

            StatusMessage = result.Data.Duplicate
                ? $"Duplicate submission ignored ({result.Data.GoodsReceiptId})."
                : $"Goods receipt {result.Data.GoodsReceiptId} posted.";
            GoodsReceiptLineDrafts.Clear();
            await ReloadGoodsReceiptsAsync().ConfigureAwait(true);
            await ReloadExpectedReceiptsAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyQcDecisionAsync()
    {
        if (!CanOperateQc || SelectedQcRow is null)
        {
            StatusMessage = "Select a QC row and ensure QC permission.";
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _wms.ApplyQcDecisionAsync(SelectedQcRow.ReceiptLineId, new WmsQcDecisionRequest
            {
                Status = QcQtyAccepted > 0 && QcQtyRejected > 0 ? "partial" : QcQtyAccepted > 0 ? "accepted" : "rejected",
                QtyAccepted = QcQtyAccepted,
                QtyRejected = QcQtyRejected,
                Note = QcNote,
                ApprovalNote = QcNote,
            }).ConfigureAwait(true);
            if (!res.Ok || res.Data is null)
            {
                ApplyApiError(res.Error);
                return;
            }

            StatusMessage = $"QC {res.Data.Status}: accepted {res.Data.QtyAccepted}, rejected {res.Data.QtyRejected}.";
            await ReloadQcAsync().ConfigureAwait(true);
            await ReloadPutawayAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CompletePutawayTaskAsync()
    {
        if (!CanOperatePutaway || SelectedPutawayTask is null || DraftPutawayToLocation is null)
        {
            StatusMessage = "Select put-away task and target location.";
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _wms.CompletePutawayAsync(SelectedPutawayTask.PutawayTaskId, new CompleteWmsPutawayRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                ToLocationId = DraftPutawayToLocation.Id,
            }).ConfigureAwait(true);
            if (!res.Ok)
            {
                ApplyApiError(res.Error);
                return;
            }

            StatusMessage = $"Put-away completed to {DraftPutawayToLocation.Path}.";
            await ReloadPutawayAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveOperatorGrantAsync()
    {
        if (!CanManageOperatorGrants || string.IsNullOrWhiteSpace(GrantOperatorEmail))
        {
            StatusMessage = "Administrator access required to manage operator grants.";
            return;
        }

        var warehouseIds = GrantWarehouseScope.Where(g => g.IsSelected).Select(g => g.Id).ToArray();
        var ownerIds = GrantOwnerScope.Where(g => g.IsSelected).Select(g => g.Id).ToArray();
        if (warehouseIds.Length == 0 || ownerIds.Length == 0)
        {
            StatusMessage = "Select at least one warehouse and one stock owner for the grant scope.";
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _wms.UpsertOperatorGrantAsync(new UpsertWmsOperatorGrantRequest
            {
                Email = GrantOperatorEmail.Trim(),
                WarehouseIds = warehouseIds,
                OwnerIds = ownerIds,
                Permissions = new WmsOperatorPermissionsDto
                {
                    Receiving = GrantPermReceiving,
                    Qc = GrantPermQc,
                    Putaway = GrantPermPutaway,
                    Supervise = GrantPermSupervise,
                },
            }).ConfigureAwait(true);
            if (!res.Ok)
            {
                ApplyApiError(res.Error);
                return;
            }

            StatusMessage = $"Operator grant saved for {GrantOperatorEmail}.";
            await ReloadOperatorGrantsAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshReceivingWorksheetAsync()
    {
        await LoadSessionProfileAsync().ConfigureAwait(true);
        switch (SelectedSectionId)
        {
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
            case WmsWorkbookSections.Administration:
                await ReloadOperatorGrantsAsync().ConfigureAwait(true);
                break;
        }
    }

    private async Task ReloadReceiptLinesForSelectedAsync()
    {
        if (SelectedGoodsReceipt is null) return;
        ReceiptLineRows.Clear();
        var lines = await _wms.ListReceiptLinesAsync(SelectedGoodsReceipt.GoodsReceiptId).ConfigureAwait(true);
        if (!lines.Ok) return;
        foreach (var l in lines.Items) ReceiptLineRows.Add(l);
    }

    [RelayCommand]
    private async Task ConfirmDispositionAsync()
    {
        if (SelectedReceiptLine is null || string.IsNullOrWhiteSpace(DispositionReason) || DispositionQuantity <= 0)
        {
            StatusMessage = "Select a receipt line, quantity, and disposition reason.";
            return;
        }

        IsBusy = true;
        try
        {
            var res = await _wms.ConfirmDispositionAsync(SelectedReceiptLine.ReceiptLineId, new WmsDispositionRequest
            {
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Quantity = DispositionQuantity,
                DispositionReason = DispositionReason.Trim(),
                DispositionType = DispositionType,
            }).ConfigureAwait(true);
            if (!res.Ok)
            {
                ApplyApiError(res.Error);
                return;
            }

            StatusMessage =
                $"Disposition recorded ({res.Data?.DispositionType} qty {res.Data?.Quantity}). Stock written off only after explicit confirmation.";
            await ReloadReceiptLinesForSelectedAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RecordStagingPreflightAsync()
    {
        if (!CanRecordStagingPreflight)
        {
            StatusMessage = "WMS administrator required to record staging preflight.";
            return;
        }

        IsBusy = true;
        try
        {
            var note = string.IsNullOrWhiteSpace(StagingPreflightNote)
                ? null
                : StagingPreflightNote.Trim();
            var res = await _wms.RecordStagingPreflightAsync(note).ConfigureAwait(true);
            if (!res.Ok)
            {
                ApplyApiError(res.Error);
                return;
            }

            StatusMessage = "Staging gate preflight recorded on tenant.";
            await RefreshStagingGateSummaryAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task RefreshStagingGateSummaryAsync()
    {
        var latest = await _wms.GetStagingGateLatestAsync().ConfigureAwait(true);
        if (!latest.Ok || latest.Data is null)
        {
            ReceivingGateSummary =
                "No staging gate evidence on tenant. Admin must run transaction preflight on staging API.";
            return;
        }

        var d = latest.Data;
        ReceivingGateSummary =
            $"Gate: supported={d.Supported}, commit={d.CommitProbe}, rollback={d.RollbackProbe}, " +
            $"db={d.TenantDatabase ?? "?"}, replSet={d.ReplicaSetName ?? "n/a"}, " +
            $"stagingPost={d.StagingStockPosting}, livePost={d.LiveStockPosting}";
    }

    private async Task ReloadOperatorGrantsAsync()
    {
        OperatorGrantRows.Clear();
        if (!CanManageOperatorGrants) return;
        var res = await _wms.ListOperatorGrantsAsync().ConfigureAwait(true);
        if (!res.Ok) return;
        foreach (var g in res.Items) OperatorGrantRows.Add(g);
    }
}