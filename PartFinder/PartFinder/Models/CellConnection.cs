using CommunityToolkit.Mvvm.ComponentModel;

namespace PartFinder.Models;

/// <summary>
/// Represents a connector line between two cells on the Template Canvas.
/// Each connection links a source cell port to a target cell port with an editable label.
/// </summary>
public sealed class CellConnection : ObservableObject
{
    private string _label = "Group";

    /// <summary>Unique identifier for this connection.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Index of the source cell in the ColumnLabels collection.</summary>
    public int SourceCellIndex { get; set; }

    /// <summary>Which side of the source cell the line attaches to.</summary>
    public ConnectionPortSide SourceSide { get; set; }

    /// <summary>Index of the target cell in the ColumnLabels collection.</summary>
    public int TargetCellIndex { get; set; }

    /// <summary>Which side of the target cell the line attaches to.</summary>
    public ConnectionPortSide TargetSide { get; set; }

    /// <summary>Editable label displayed on the connector line.</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    /// <summary>StableKey of the source cell (used for persistence across reorders).</summary>
    public string? SourceCellKey { get; set; }

    /// <summary>StableKey of the target cell (used for persistence across reorders).</summary>
    public string? TargetCellKey { get; set; }
}
