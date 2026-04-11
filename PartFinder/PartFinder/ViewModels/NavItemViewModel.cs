using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed class NavItemViewModel
{
    public required string Label { get; init; }
    public required string IconGlyph { get; init; }
    public required AppPage Page { get; init; }
}
