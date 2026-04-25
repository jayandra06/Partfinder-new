namespace PartFinder.Models;

public sealed class MasterDataRowRecord
{
    public required string Id { get; init; }

    public int RowOrder { get; init; }

    public required IReadOnlyDictionary<string, string> Values { get; init; }
}
