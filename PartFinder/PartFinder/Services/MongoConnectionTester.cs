using MongoDB.Bson;
using MongoDB.Driver;

namespace PartFinder.Services;

public static class MongoConnectionTester
{
    public static async Task<bool> TryPingAsync(
        string connectionString,
        string? orgCode = null,
        CancellationToken ct = default)
    {
        try
        {
            var cs = MongoConnectionStringUtil.Prepare(connectionString, orgCode);
            var client = new MongoClient(cs);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, cs, orgCode);
            _ = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct)
                .ConfigureAwait(true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
