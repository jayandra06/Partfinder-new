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

    public string? OrgCode => _cachedOrgCode;

    public string? AdminEmail => _cachedAdminEmail;

    public void Refresh()
    {
        _cachedUri = null;
        _cachedOrgCode = null;
        _cachedAdminEmail = null;
        try
        {
            // Multiple setup-state.json copies can exist (LocalAppData vs Roaming, older builds).
            // The first file found on disk is not always the one that contains dbUri. Merge all
            // completed setups so org code and Mongo URI resolve consistently with the shell UI.
            foreach (var path in SetupPaths.SetupStateCandidatePaths)
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                var isCompleted = root.TryGetProperty("completed", out var completed) &&
                                  completed.ValueKind == JsonValueKind.True;

                // Last non-empty wins so a complete Roaming copy can override an incomplete Local copy.
                if (isCompleted &&
                    root.TryGetProperty("dbUri", out var db) &&
                    db.ValueKind == JsonValueKind.String)
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
            }
        }
        catch
        {
            _cachedUri = null;
            _cachedOrgCode = null;
            _cachedAdminEmail = null;
        }
    }

    public bool TryGetTenantMongoUri([NotNullWhen(true)] out string? uri)
    {
        Refresh();
        uri = _cachedUri;
        return !string.IsNullOrWhiteSpace(uri);
    }
}
