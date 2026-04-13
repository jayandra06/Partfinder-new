using MongoDB.Bson;
using MongoDB.Driver;

namespace PartFinder.Services;

public static class MongoConnectionTester
{
    public static async Task<bool> TryPingAsync(string connectionString, CancellationToken ct = default)
    {
        try
        {
            var client = new MongoClient(connectionString);
            _ = await client.GetDatabase("admin")
                .RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct)
                .ConfigureAwait(true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
