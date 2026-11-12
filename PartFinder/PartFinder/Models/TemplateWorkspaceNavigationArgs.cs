namespace PartFinder.Models;

public sealed class TemplateWorkspaceNavigationArgs
{
    public required string TemplateId { get; init; }

    public bool ViewOnly { get; init; }
}
