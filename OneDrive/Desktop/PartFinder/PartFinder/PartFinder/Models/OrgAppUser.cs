namespace PartFinder.Models;

public sealed class OrgAppUserSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required bool PartsAllTemplates { get; init; }
    public IReadOnlyList<string> AllowedTemplateIds { get; init; } = Array.Empty<string>();
    public DateTime InvitedAtUtc { get; init; }

    public string PartsScopeDisplay =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Full access"
            : PartsAllTemplates
                ? "Parts (all part templates)"
                : $"Parts ({AllowedTemplateIds.Count} template(s))";
}
