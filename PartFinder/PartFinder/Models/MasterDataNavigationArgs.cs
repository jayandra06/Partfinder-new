namespace PartFinder.Models;

public sealed class MasterDataNavigationArgs
{
    public MasterDataSurfaceMode Mode { get; init; } = MasterDataSurfaceMode.ReadOnlyCatalog;

    public string? TemplateId { get; init; }

    public bool StartInEditMode { get; init; }
}
