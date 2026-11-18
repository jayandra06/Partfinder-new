namespace PartFinder.ViewModels;

public sealed class WmsWorkbookNavItem
{
    public bool IsGroupHeader { get; init; }
    public string? GroupTitle { get; init; }
    public string? SectionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public bool IsOperational { get; init; }
    public string? RoadmapPhase { get; init; }
}