using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PartFinder.Services;

/// <summary>Shared client-side search/sort for WMS workbook grids.</summary>
public static class WmsWorksheetGridHelper
{
    public static List<T> FilterRows<T>(IEnumerable<T> rows, string? query, Func<T, string> textSelector)
    {
        if (string.IsNullOrWhiteSpace(query)) return rows.ToList();
        var q = query.Trim();
        return rows.Where(r => textSelector(r).Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public static List<T> SortRows<T>(IEnumerable<T> rows, string? column, bool ascending, Func<T, string> key)
    {
        var list = rows.ToList();
        if (string.IsNullOrWhiteSpace(column)) return list;
        return ascending
            ? list.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
            : list.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public abstract class WmsEditableRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string? _validationError;
    public string? ValidationError
    {
        get => _validationError;
        set
        {
            _validationError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class WmsExpectedLineDraftVm : WmsEditableRowVm
{
    private string _skuId = string.Empty;
    public string SkuId
    {
        get => _skuId;
        set { _skuId = value; OnPropertyChanged(); }
    }

    private double _qtyExpected;
    public double QtyExpected
    {
        get => _qtyExpected;
        set { _qtyExpected = value; OnPropertyChanged(); }
    }

    private string _primaryUom = "EA";
    public string PrimaryUom
    {
        get => _primaryUom;
        set { _primaryUom = value; OnPropertyChanged(); }
    }
}

public sealed class WmsGoodsReceiptLineDraftVm : WmsEditableRowVm
{
    private string _skuId = string.Empty;
    public string SkuId
    {
        get => _skuId;
        set { _skuId = value; OnPropertyChanged(); }
    }

    private double _qtyReceived;
    public double QtyReceived
    {
        get => _qtyReceived;
        set { _qtyReceived = value; OnPropertyChanged(); }
    }

    public string? LotId { get; set; }
    public string? SerialId { get; set; }
    public bool Damaged { get; set; }
    public bool Shortage { get; set; }
    public bool OverDelivery { get; set; }
}