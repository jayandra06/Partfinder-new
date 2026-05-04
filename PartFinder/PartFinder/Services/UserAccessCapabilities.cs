namespace PartFinder.Services;

/// <summary>
/// Resolved permissions for the current profile. Casbin can replace this policy layer later.
/// </summary>
public sealed class UserAccessCapabilities
{
    public bool CanAccessMasterData { get; init; }
    public bool CanAccessDashboard { get; init; }
    public bool CanAccessParts { get; init; }
    public bool CanAccessTemplates { get; init; }
    public bool CanAccessSettings { get; init; }
    public bool CanAccessUserManagement { get; init; }

    // Granular Template Permissions
    public bool CanAddTemplate { get; init; }
    public bool CanViewTemplate { get; init; }
    public bool CanEditTemplate { get; init; }
    public bool CanDeleteTemplate { get; init; }

    // Granular Master Data Permissions
    public bool CanCopyMasterData { get; init; }
    public bool CanViewMasterData { get; init; }
    public bool CanEditMasterData { get; init; }
    public bool CanAddMasterData { get; init; }
    public bool CanDeleteMasterData { get; init; }

    /// <summary>When true, Parts page lists all allowed non–Master-Data templates.</summary>
    public bool PartsAllTemplates { get; init; }

    /// <summary>When PartsAllTemplates is false for an employee, only these template ids.</summary>
    public IReadOnlyList<string> AllowedTemplateIds { get; init; } = Array.Empty<string>();

    public static UserAccessCapabilities FullAdmin { get; } = new()
    {
        CanAccessMasterData = true,
        CanAccessDashboard = true,
        CanAccessParts = true,
        CanAccessTemplates = true,
        CanAccessSettings = true,
        CanAccessUserManagement = true,

        CanAddTemplate = true,
        CanViewTemplate = true,
        CanEditTemplate = true,
        CanDeleteTemplate = true,

        CanCopyMasterData = true,
        CanViewMasterData = true,
        CanEditMasterData = true,
        CanAddMasterData = true,
        CanDeleteMasterData = true,

        PartsAllTemplates = true,
        AllowedTemplateIds = Array.Empty<string>(),
    };
}
