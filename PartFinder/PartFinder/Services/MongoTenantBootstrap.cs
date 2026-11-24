using MongoDB.Bson;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Mirrors backend tenant init when the API cannot reach a LAN-only MongoDB (custom URI path).
/// </summary>
public static class MongoTenantBootstrap
{
    public const string OrgAdminCollection = "org_admin_users";

    public static async Task<bool> TryInitializeTenantAsync(string uri, string? orgCode = null, CancellationToken ct = default)
    {
        try
        {
            var cs = MongoConnectionStringUtil.Prepare(uri, orgCode);
            var client = new MongoClient(cs);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, cs, orgCode);
            var admins = db.GetCollection<BsonDocument>(OrgAdminCollection);
            var emailKey = Builders<BsonDocument>.IndexKeys.Ascending("email");
            await admins.Indexes.CreateOneAsync(
                    new CreateIndexModel<BsonDocument>(emailKey, new CreateIndexOptions { Unique = true }),
                    cancellationToken: ct)
                .ConfigureAwait(true);
            var meta = db.GetCollection<BsonDocument>("_partfinder_setup");
            await meta.UpdateOneAsync(
                    new BsonDocument("_id", "bootstrap"),
                    Builders<BsonDocument>.Update.Combine(
                        Builders<BsonDocument>.Update.SetOnInsert("bootstrappedAt", DateTime.UtcNow),
                        Builders<BsonDocument>.Update.SetOnInsert("version", 1)),
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken: ct)
                .ConfigureAwait(true);
            _ = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct)
                .ConfigureAwait(true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Stores the new admin password in the same database the setup API reads.
    /// A URI with no database path uses <c>test</c>, which is the driver default the API uses.
    /// </summary>
    public static async Task<bool> TrySetOrgAdminPasswordAsync(
        string uri,
        string email,
        string newPassword,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            var cs = MongoConnectionStringUtil.Normalize(uri);
            var url = new MongoUrl(cs);
            var dbName = string.IsNullOrWhiteSpace(url.DatabaseName) ? "test" : url.DatabaseName;
            var client = new MongoClient(cs);
            var coll = client.GetDatabase(dbName).GetCollection<BsonDocument>(OrgAdminCollection);
            var emailLower = email.Trim().ToLowerInvariant();
            var hash = BCrypt.Net.BCrypt.HashPassword(newPassword, workFactor: 10);
            var result = await coll.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("email", emailLower),
                    Builders<BsonDocument>.Update
                        .Set("passwordHash", hash)
                        .Set("email", emailLower)
                        .SetOnInsert("name", "Organisation Admin")
                        .SetOnInsert("createdAt", DateTime.UtcNow),
                    new UpdateOptions { IsUpsert = true },
                    cancellationToken: ct)
                .ConfigureAwait(true);
            return result.IsAcknowledged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoTenantBootstrap] TrySetOrgAdminPassword failed: {ex.Message}");
            return false;
        }
    }

    public static async Task<long> CountOrgAdminsAsync(string uri, string? orgCode = null, CancellationToken ct = default)
    {
        try
        {
            var cs = MongoConnectionStringUtil.Prepare(uri, orgCode);
            var client = new MongoClient(cs);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, cs, orgCode);
            var coll = db.GetCollection<BsonDocument>(OrgAdminCollection);
            return await coll.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: ct)
                .ConfigureAwait(true);
        }
        catch
        {
            return 0;
        }
    }
}
