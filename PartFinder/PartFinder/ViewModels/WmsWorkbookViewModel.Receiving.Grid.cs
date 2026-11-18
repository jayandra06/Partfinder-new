using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel
{
    [RelayCommand]
    private void SortReceivingList(string key)
    {
        ReceivingListSortKey = key;
        ReceivingListSortAscending = !ReceivingListSortAscending;

        IEnumerable<WmsExpectedReceiptRowDto> expected = ExpectedReceiptRows;
        IEnumerable<WmsGoodsReceiptRowDto> goods = GoodsReceiptRows;
        IEnumerable<WmsQcRowDto> qc = QcRows;
        IEnumerable<WmsPutawayRowDto> put = PutawayRows;

        var search = InventorySearch?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            expected = WmsWorksheetGridHelper.FilterRows(
                expected,
                search,
                r => $"{r.ExpectedReceiptId} {r.Status} {r.ExternalPoId}");
            goods = WmsWorksheetGridHelper.FilterRows(
                goods,
                search,
                r => $"{r.GoodsReceiptId} {r.Status} {r.CreatedBy}");
            qc = WmsWorksheetGridHelper.FilterRows(qc, search, r => $"{r.ReceiptLineId} {r.Status}");
            put = WmsWorksheetGridHelper.FilterRows(put, search, r => $"{r.SkuId} {r.Status}");
        }

        Func<WmsExpectedReceiptRowDto, string> expKey = key switch
        {
            "status" => r => r.Status,
            "po" => r => r.ExternalPoId ?? string.Empty,
            _ => r => r.ExpectedReceiptId,
        };
        Func<WmsGoodsReceiptRowDto, string> grKey = key switch
        {
            "status" => r => r.Status,
            "actor" => r => r.CreatedBy,
            _ => r => r.GoodsReceiptId,
        };

        var sortedExpected = WmsWorksheetGridHelper.SortRows(
            expected,
            key,
            ReceivingListSortAscending,
            expKey);
        var sortedGoods = WmsWorksheetGridHelper.SortRows(
            goods,
            key,
            ReceivingListSortAscending,
            grKey);

        ExpectedReceiptRows.Clear();
        foreach (var row in sortedExpected) ExpectedReceiptRows.Add(row);
        GoodsReceiptRows.Clear();
        foreach (var row in sortedGoods) GoodsReceiptRows.Add(row);

        QcRows.Clear();
        foreach (var row in WmsWorksheetGridHelper.SortRows(
                     qc,
                     key,
                     ReceivingListSortAscending,
                     r => r.Status))
            QcRows.Add(row);
        PutawayRows.Clear();
        foreach (var row in WmsWorksheetGridHelper.SortRows(
                     put,
                     key,
                     ReceivingListSortAscending,
                     r => r.SkuId))
            PutawayRows.Add(row);

        StatusMessage = $"Sorted by {key} ({(ReceivingListSortAscending ? "asc" : "desc")}).";
    }

    [RelayCommand]
    private async Task PasteGoodsReceiptLinesFromClipboardAsync()
    {
        var package = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (!package.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            StatusMessage = "Clipboard has no text.";
            return;
        }

        var text = await package.GetTextAsync();
        GoodsReceiptLineDrafts.Clear();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var cells = line.Split('\t');
            if (cells.Length < 2) continue;
            if (!double.TryParse(cells[1].Trim(), out var qty)) continue;
            GoodsReceiptLineDrafts.Add(new WmsGoodsReceiptLineDraftVm
            {
                SkuId = cells[0].Trim(),
                QtyReceived = qty,
                LotId = cells.Length > 2 ? cells[2].Trim() : null,
                SerialId = cells.Length > 3 ? cells[3].Trim() : null,
            });
        }

        StatusMessage = $"Pasted {GoodsReceiptLineDrafts.Count} receipt lines from clipboard.";
    }

    private void ApplyReceivingSearchOnReload<T>(
        ObservableCollection<T> target,
        IEnumerable<T> source,
        Func<T, string> text)
    {
        target.Clear();
        var filtered = string.IsNullOrWhiteSpace(InventorySearch)
            ? source
            : WmsWorksheetGridHelper.FilterRows(source, InventorySearch, text);
        foreach (var row in filtered) target.Add(row);
    }
}