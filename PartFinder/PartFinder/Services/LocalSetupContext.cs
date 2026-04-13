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

    public string? OrgCode => _cachedOrgCode;

    public void Refresh()
    {
        _cachedUri = null;
        _cachedOrgCode = null;
        try
        {
            var path = SetupPaths.FindExistingSetupStatePath();
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.True)
            {
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
                    _cachedOrgCode = oc.GetString()?.Trim();
                }
            }
        }
        catch
        {
            _cachedUri = null;
            _cachedOrgCode = null;
        }
    }

    public bool TryGetTenantMongoUri([NotNullWhen(true)] out string? uri)
    {
        Refresh();
        uri = _cachedUri;
        return !string.IsNullOrWhiteSpace(uri);
    }
}
