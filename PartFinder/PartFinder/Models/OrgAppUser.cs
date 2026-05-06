namespace PartFinder.Models;

public sealed class TemplatePermissionsDto
{
    public bool Add { get; set; }
    public bool View { get; set; }
    public bool Edit { get; set; }
    public bool Delete { get; set; }
}

public sealed class MasterDataPermissionsDto
{
    public bool Copy { get; set; }
    public bool View { get; set; }
    public bool Edit { get; set; }
    public bool Add { get; set; }
    public bool Delete { get; set; }
}

public sealed class OrgAppUserSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required bool PartsAllTemplates { get; init; }
    public IReadOnlyList<string> AllowedTemplateIds { get; init; } = Array.Empty<string>();
    public DateTime InvitedAtUtc { get; init; }
    public string Status { get; init; } = "Pending";

    public TemplatePermissionsDto? TemplatePermissions { get; init; }
    public MasterDataPermissionsDto? MasterDataPermissions { get; init; }

    public TemplatePermissionsDto? TemplatePermissions { get; init; }
    public MasterDataPermissionsDto? MasterDataPermissions { get; init; }

    /// <summary>"Active" if password changed, "Pending" if still on temporary password.</summary>
    public string Status { get; init; } = "Pending";

    public string PartsScopeDisplay =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Full access"
            : PartsAllTemplates
                ? "Parts (all part templates)"
                : $"Parts ({AllowedTemplateIds.Count} template(s))";
}
