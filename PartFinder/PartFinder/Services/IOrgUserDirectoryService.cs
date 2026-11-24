using PartFinder.Models;

namespace PartFinder.Services;

public sealed class InviteUserResult
{
    public required string Email { get; init; }
    public required string OrganizationCode { get; init; }
    public required string TemporaryPassword { get; init; }
    public bool EmailSent { get; init; }
    public string? EmailError { get; init; }
}

public interface IOrgUserDirectoryService
{
    Task<IReadOnlyList<OrgAppUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists org_app_users from an explicit tenant Mongo URI (e.g. from setup status API).</summary>
    Task<IReadOnlyList<OrgAppUserSummary>> ListUsersFromTenantUriAsync(
        string tenantMongoUri,
        string orgCode,
        CancellationToken cancellationToken = default);

    Task<OrgAppUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>When the user is the org creator (not in org_app_users), returns createdAt from org_admin_users.</summary>
    Task<DateTime?> GetOrgAdminCreatedAtUtcAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Tenant bootstrap timestamp from <c>_partfinder_setup</c> when admin/user dates are unavailable.</summary>
    Task<DateTime?> GetTenantBootstrapDateUtcAsync(CancellationToken cancellationToken = default);

    Task<InviteUserResult> InviteUserAsync(
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures an invited user exists in tenant Mongo (no email). Used after API invite so Team Management can list locally.
    /// </summary>
    Task EnsureInvitedUserRecordAsync(
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default);

    Task EnsureInvitedUserRecordForTenantUriAsync(
        string tenantMongoUri,
        string orgCode,
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        string temporaryPassword,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateInviteCredentialsAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateUserAsync(
        string id,
        string name,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Updates the display name on the organisation bootstrap admin record (<c>org_admin_users</c>).</summary>
    Task<bool> UpdateOrgAdminDisplayNameAsync(
        string email,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>Clears temporary password and stores permanent hash after first-time password setup.</summary>
    Task<bool> CompleteInvitedUserActivationAsync(
        string email,
        string newPasswordPlain,
        CancellationToken cancellationToken = default);

    /// <summary>Clears invite pending flag when the user signed in without a forced password change.</summary>
    Task<bool> MarkInvitedUserActiveAfterLoginAsync(
        string email,
        CancellationToken cancellationToken = default);
}
