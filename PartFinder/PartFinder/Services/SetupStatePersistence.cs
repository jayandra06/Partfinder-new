using System.Text.Json;
using System.Text.Json.Nodes;

namespace PartFinder.Services;

/// <summary>Keeps local setup-state.json aligned with the organisation record on the backend.</summary>
internal static class SetupStatePersistence
{
    private static readonly JsonSerializerOptions WriteJson = new()
    {
        WriteIndented = true,
    };

    public static async Task SyncOrgDatabaseFromBackendAsync(
        string orgCode,
        CancellationToken cancellationToken = default)
    {
        var trimmed = orgCode.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        var (status, _) = await SetupApiClient.StatusAsync(trimmed, cancellationToken).ConfigureAwait(false);
        if (status?.Valid != true || string.IsNullOrWhiteSpace(status.OrgDatabaseUri))
        {
            return;
        }

        TryWriteDbUri(status.OrgDatabaseUri.Trim(), trimmed);
    }

    public static void TryWriteDbUri(string uriFromBackend, string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(uriFromBackend))
        {
            return;
        }

        string prepared;
        try
        {
            prepared = MongoConnectionStringUtil.Prepare(uriFromBackend, orgCode);
        }
        catch (ArgumentException)
        {
            prepared = uriFromBackend;
        }

        try
        {
            SetupPaths.MigrateToCanonicalSetupStateIfNeeded();
            var path = SetupPaths.SetupStateFilePath;
            JsonObject root;
            if (File.Exists(path))
            {
                root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                root = new JsonObject();
            }

            root["dbUri"] = prepared;
            if (!string.IsNullOrWhiteSpace(orgCode))
            {
                root["orgCode"] = orgCode.Trim();
            }

            File.WriteAllText(path, root.ToJsonString(WriteJson));
            SetupPaths.PurgeLegacySetupStateFiles();
        }
        catch
        {
            // Best effort — listing may still use the backend API.
        }
    }
}
