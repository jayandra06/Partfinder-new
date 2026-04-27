using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Dedicated alerts collection in tenant MongoDB.
/// Collection is created automatically by MongoDB on first insert.
/// </summary>
public sealed class MongoAlertsService
{
    public const string CollectionName = "partfinder_alerts";

    private readonly ILocalSetupContext _setupContext;

    public MongoAlertsService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<AlertDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<AlertDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<AlertDoc>> GetActiveAsync(CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return Array.Empty<AlertDoc>();

        try
        {
            var filter = Builders<AlertDoc>.Filter.Eq(d => d.IsResolved, false);
            return await coll.Find(filter)
                .SortByDescending(d => d.CreatedAt)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<AlertDoc>();
        }
    }

    public async Task<string> UpsertAsync(AlertDoc doc, CancellationToken ct = default)
    {
        var coll = TryGetCollection()
            ?? throw new InvalidOperationException("No tenant database configured.");

        if (doc.MongoId == ObjectId.Empty)
            doc.MongoId = ObjectId.GenerateNewId();

        await coll.ReplaceOneAsync(
            Builders<AlertDoc>.Filter.Eq(d => d.MongoId, doc.MongoId),
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct).ConfigureAwait(false);

        return doc.MongoId.ToString();
    }

    public async Task ResolveAsync(string id, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null || !ObjectId.TryParse(id, out var oid)) return;

        try
        {
            var update = Builders<AlertDoc>.Update
                .Set(d => d.IsResolved, true)
                .Set(d => d.ResolvedAt, DateTime.UtcNow);
            await coll.UpdateOneAsync(
                Builders<AlertDoc>.Filter.Eq(d => d.MongoId, oid),
                update, cancellationToken: ct).ConfigureAwait(false);
        }
        catch { }
    }

    public async Task DismissAsync(string id, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null || !ObjectId.TryParse(id, out var oid)) return;

        try
        {
            await coll.DeleteOneAsync(
                Builders<AlertDoc>.Filter.Eq(d => d.MongoId, oid), ct)
                .ConfigureAwait(false);
        }
        catch { }
    }

    public async Task<(int Critical, int Warning, int Info)> GetCountsAsync(CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return (0, 0, 0);

        try
        {
            var activeFilter = Builders<AlertDoc>.Filter.Eq(d => d.IsResolved, false);
            var all = await coll.Find(activeFilter).ToListAsync(ct).ConfigureAwait(false);
            var critical = all.Count(a => a.Severity == "Critical");
            var warning = all.Count(a => a.Severity == "Warning");
            var info = all.Count(a => a.Severity == "Info");
            return (critical, warning, info);
        }
        catch
        {
            return (0, 0, 0);
        }
    }
}

[BsonIgnoreExtraElements]
public sealed class AlertDoc
{
    [BsonId]
    public ObjectId MongoId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("severity")]
    public string Severity { get; set; } = "Info"; // Critical, Warning, Info

    [BsonElement("category")]
    public string Category { get; set; } = "Stock"; // Stock, System, User

    [BsonElement("partName")]
    public string PartName { get; set; } = string.Empty;

    [BsonElement("isResolved")]
    public bool IsResolved { get; set; }

    [BsonElement("isRead")]
    public bool IsRead { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("resolvedAt")]
    public DateTime? ResolvedAt { get; set; }
}
