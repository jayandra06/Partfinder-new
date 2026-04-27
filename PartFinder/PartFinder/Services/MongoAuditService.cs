using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Dedicated audit log collection in tenant MongoDB.
/// Collection is created automatically by MongoDB on first insert.
/// Uses a cached MongoClient per connection string for fast, secure access.
/// </summary>
public sealed class MongoAuditService
{
    public const string CollectionName = "partfinder_audit_logs";

    private readonly ILocalSetupContext _setupContext;

    // Cache client per connection string — MongoClient is thread-safe and expensive to create
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MongoClient> _clientCache = new();

    public MongoAuditService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<AuditDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var client = _clientCache.GetOrAdd(cs, static c => new MongoClient(c));
        var url = new MongoUrl(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<AuditDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<AuditDoc>> GetRecentAsync(int limit = 200, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return Array.Empty<AuditDoc>();

        try
        {
            // Ensure descending index on timestamp for fast fetch
            var indexKeys = Builders<AuditDoc>.IndexKeys.Descending(d => d.Timestamp);
            var indexModel = new CreateIndexModel<AuditDoc>(indexKeys,
                new CreateIndexOptions { Background = true, Name = "timestamp_desc" });
            await coll.Indexes.CreateOneAsync(indexModel, cancellationToken: ct).ConfigureAwait(false);

            return await coll.Find(FilterDefinition<AuditDoc>.Empty)
                .SortByDescending(d => d.Timestamp)
                .Limit(limit)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<AuditDoc>();
        }
    }

    /// <summary>
    /// Logs an audit event. Creates the collection automatically on first call.
    /// </summary>
    public async Task LogAsync(AuditDoc doc, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return; // No DB configured — silently skip logging

        try
        {
            if (doc.MongoId == ObjectId.Empty)
                doc.MongoId = ObjectId.GenerateNewId();

            // MongoDB creates the collection automatically on first insert
            await coll.InsertOneAsync(doc, cancellationToken: ct).ConfigureAwait(false);
        }
        catch { /* Never crash on audit logging */ }
    }

    public async Task<long> CountTodayAsync(CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return 0;

        try
        {
            var today = DateTime.UtcNow.Date;
            var filter = Builders<AuditDoc>.Filter.Gte(d => d.Timestamp, today);
            return await coll.CountDocumentsAsync(filter, cancellationToken: ct).ConfigureAwait(false);
        }
        catch
        {
            return 0;
        }
    }
}

[BsonIgnoreExtraElements]
public sealed class AuditDoc
{
    [BsonId]
    public ObjectId MongoId { get; set; }

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("details")]
    public string Details { get; set; } = string.Empty;

    [BsonElement("user")]
    public string User { get; set; } = string.Empty;

    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty;
}
