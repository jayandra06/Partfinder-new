using MongoDB.Bson;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Mirrors backend tenant init when the API cannot reach a LAN-only MongoDB (custom URI path).
/// </summary>
public static class MongoTenantBootstrap
{
    public const string OrgAdminCollection = "org_admin_users";

    public static async Task<bool> TryInitializeTenantAsync(string uri, CancellationToken ct = default)
    {
        try
        {
            var url = new MongoUrl(uri);
            if (string.IsNullOrEmpty(url.DatabaseName))
            {
                return false;
            }

            var client = new MongoClient(uri);
            var db = client.GetDatabase(url.DatabaseName);
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

    public static async Task<long> CountOrgAdminsAsync(string uri, CancellationToken ct = default)
    {
        try
        {
            var url = new MongoUrl(uri);
            if (string.IsNullOrEmpty(url.DatabaseName))
            {
                return 0;
            }

            var client = new MongoClient(uri);
            var db = client.GetDatabase(url.DatabaseName);
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
