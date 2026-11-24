 using MongoDB.Bson.Serialization.Attributes;

namespace PartFinder.Models;

public sealed class TemplatePermissionsDto
{
    [BsonElement("add")]
    public bool Add { get; set; }

    [BsonElement("view")]
    public bool View { get; set; }

    [BsonElement("edit")]
    public bool Edit { get; set; }

    [BsonElement("delete")]
    public bool Delete { get; set; }
}

public sealed class MasterDataPermissionsDto
{
    [BsonElement("copy")]
    public bool Copy { get; set; }

    [BsonElement("view")]
    public bool View { get; set; }

    [BsonElement("edit")]
    public bool Edit { get; set; }

    [BsonElement("add")]
    public bool Add { get; set; }

    [BsonElement("delete")]
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

    public TemplatePermissionsDto? TemplatePermissions { get; init; }
    public MasterDataPermissionsDto? MasterDataPermissions { get; init; }

    /// <summary>"Active" if password changed, "Pending" if still on temporary password.</summary>
    public string Status { get; init; } = "Pending";

    /// <summary>Organisation admin from <c>org_admin_users</c> (not editable via invite directory).</summary>
    public bool IsOrgAdminDirectoryEntry { get; init; }

    public bool CanEditInUserManagement => !IsOrgAdminDirectoryEntry;

    public bool IsActive =>
        string.Equals(Status, "Active", StringComparison.OrdinalIgnoreCase);

    public string PartsScopeDisplay =>
        string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase)
            ? "Full access"
            : PartsAllTemplates
                ? "Parts (all part templates)"
                : $"Parts ({AllowedTemplateIds.Count} template(s))";

    /// <summary>First letter of the user's name for the avatar circle.</summary>
    public string Initials =>
        string.IsNullOrWhiteSpace(Name)
            ? "?"
            : Name.Trim()[..1].ToUpperInvariant();

    /// <summary>Human-readable invited date.</summary>
    public string InvitedAtDisplay =>
        InvitedAtUtc == default
            ? "—"
            : InvitedAtUtc.ToLocalTime().ToString("dd MMM yyyy");
}
