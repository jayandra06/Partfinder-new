using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// RBAC policy for the desktop shell. Replace with Casbin-backed enforcement when server-side auth lands.
/// </summary>
public sealed class CurrentUserAccessService : ICurrentUserAccessService
{
    private readonly ILocalSetupContext _setupContext;
    private readonly AdminSessionStore _adminSession;
    private readonly IOrgUserDirectoryService _users;

    private UserAccessCapabilities _capabilities = UserAccessCapabilities.FullAdmin;

    public CurrentUserAccessService(
        ILocalSetupContext setupContext,
        AdminSessionStore adminSession,
        IOrgUserDirectoryService users)
    {
        _setupContext = setupContext;
        _adminSession = adminSession;
        _users = users;
    }

    public UserAccessCapabilities Capabilities => _capabilities;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _setupContext.Refresh();
        _adminSession.Load();

        var email = ResolveCurrentEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            _capabilities = UserAccessCapabilities.FullAdmin;
            return;
        }

        var record = await _users.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            // No record in org_app_users means this is the org creator (in org_admins collection).
            // Org creator gets full access including User Management.
            _capabilities = UserAccessCapabilities.FullAdmin;
            return;
        }

        if (string.Equals(record.Role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            // Invited admin — full access EXCEPT User Management (only org creator can manage users).
            _capabilities = new UserAccessCapabilities
            {
                CanAccessMasterData = true,
                CanAccessDashboard = true,
                CanAccessParts = true,
                CanAccessTemplates = true,
                CanAccessSettings = true,
                CanAccessUserManagement = false,

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
            return;
        }

        if (string.Equals(record.Role, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            _capabilities = new UserAccessCapabilities
            {
                // Page-level access (navbar visibility) — Employee sees all standard pages.
                // Granular permissions below decide what they can DO inside each page.
                CanAccessMasterData = true,
                CanAccessDashboard = true,
                CanAccessParts = true,
                CanAccessTemplates = true,
                CanAccessSettings = true,
                CanAccessUserManagement = false,

                // Granular template permissions (actual operations)
                CanAddTemplate = record.TemplatePermissions?.Add ?? false,
                CanViewTemplate = record.TemplatePermissions?.View ?? false,
                CanEditTemplate = record.TemplatePermissions?.Edit ?? false,
                CanDeleteTemplate = record.TemplatePermissions?.Delete ?? false,

                // Granular master data permissions (actual operations)
                CanCopyMasterData = record.MasterDataPermissions?.Copy ?? false,
                CanViewMasterData = record.MasterDataPermissions?.View ?? false,
                CanEditMasterData = record.MasterDataPermissions?.Edit ?? false,
                CanAddMasterData = record.MasterDataPermissions?.Add ?? false,
                CanDeleteMasterData = record.MasterDataPermissions?.Delete ?? false,

                PartsAllTemplates = record.PartsAllTemplates,
                AllowedTemplateIds = record.AllowedTemplateIds,
            };
            return;
        }

        // Unknown role — default to FullAdmin for backward compatibility.
        _capabilities = UserAccessCapabilities.FullAdmin;
    }

    public IReadOnlyList<PartTemplateDefinition> FilterTemplatesForParts(IReadOnlyList<PartTemplateDefinition> templates)
    {
        var cap = _capabilities;
        IEnumerable<PartTemplateDefinition> q = templates;

        // Never use Master Data template on Parts for employees.
        if (!cap.CanAccessMasterData)
        {
            q = q.Where(t => !string.Equals(
                t.Id,
                MongoTemplateSchemaService.MasterDataTemplateId,
                StringComparison.OrdinalIgnoreCase));
        }

        if (!cap.PartsAllTemplates && cap.AllowedTemplateIds.Count > 0)
        {
            var allow = new HashSet<string>(cap.AllowedTemplateIds, StringComparer.OrdinalIgnoreCase);
            q = q.Where(t => allow.Contains(t.Id));
        }

        return q.ToList();
    }

    private string? ResolveCurrentEmail()
    {
        var a = _adminSession.Email?.Trim();
        if (!string.IsNullOrEmpty(a))
        {
            return a;
        }

        _setupContext.Refresh();
        return _setupContext.AdminEmail?.Trim();
    }

}
