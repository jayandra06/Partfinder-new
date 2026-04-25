namespace PartFinder.Models;

public sealed class GridColumnDefinition
{
    public required string Key { get; init; }
    public required string Header { get; init; }
    public double Width { get; init; } = 160;
}
