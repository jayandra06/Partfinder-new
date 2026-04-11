namespace PartFinder.Models;

public sealed class PartRecord
{
    public required string Id { get; init; }
    public required Dictionary<string, object?> Values { get; init; }
}
