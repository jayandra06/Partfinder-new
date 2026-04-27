namespace PartFinder.Services;

/// <summary>
/// Reads tenant Mongo URI and org code from local setup-state.json (written after setup wizard).
/// </summary>
public interface ILocalSetupContext
{
    void Refresh();

    bool TryGetTenantMongoUri(out string? uri);

    string? OrgCode { get; }

    /// <summary>Org admin email from setup-state.json (merged; last non-empty wins).</summary>
    string? AdminEmail { get; }
}
