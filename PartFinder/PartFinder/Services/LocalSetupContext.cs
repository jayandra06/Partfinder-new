using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace PartFinder.Services;

public sealed class LocalSetupContext : ILocalSetupContext
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private string? _cachedUri;
    private string? _cachedOrgCode;
    private string? _cachedAdminEmail;
    private bool _cachedInvitedUserLogin;

    public string? OrgCode => _cachedOrgCode;

    public string? AdminEmail => _cachedAdminEmail;

    public bool InvitedUserLogin => _cachedInvitedUserLogin;

    public void Refresh()
    {
        _cachedUri = null;
        _cachedOrgCode = null;
        _cachedAdminEmail = null;
        _cachedInvitedUserLogin = false;
        try
        {
            SetupPaths.MigrateToCanonicalSetupStateIfNeeded();
            var path = SetupPaths.GetAuthoritativeSetupStatePath();
            if (!File.Exists(path))
            {
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (root.TryGetProperty("dbUri", out var db) && db.ValueKind == JsonValueKind.String)
            {
                var u = db.GetString()?.Trim();
                if (!string.IsNullOrEmpty(u))
                {
                    _cachedUri = u;
                }
            }

            if (root.TryGetProperty("orgCode", out var oc) && oc.ValueKind == JsonValueKind.String)
            {
                var code = oc.GetString()?.Trim();
                if (!string.IsNullOrEmpty(code))
                {
                    _cachedOrgCode = code;
                }
            }

            if (root.TryGetProperty("adminEmail", out var ae) && ae.ValueKind == JsonValueKind.String)
            {
                var em = ae.GetString()?.Trim();
                if (!string.IsNullOrEmpty(em))
                {
                    _cachedAdminEmail = em;
                }
            }

            if (root.TryGetProperty("invitedUserLogin", out var iul) &&
                iul.ValueKind == JsonValueKind.True)
            {
                _cachedInvitedUserLogin = true;
            }
        }
        catch
        {
            _cachedUri = null;
            _cachedOrgCode = null;
            _cachedAdminEmail = null;
            _cachedInvitedUserLogin = false;
        }
    }

    public bool TryGetTenantMongoUri([NotNullWhen(true)] out string? uri)
    {
        Refresh();
        if (string.IsNullOrWhiteSpace(_cachedUri))
        {
            uri = null;
            return false;
        }

        try
        {
            uri = MongoConnectionStringUtil.Prepare(_cachedUri, _cachedOrgCode);
        }
        catch (ArgumentException)
        {
            uri = _cachedUri;
        }

        return !string.IsNullOrWhiteSpace(uri);
    }
}
