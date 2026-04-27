using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Dedicated inventory collection in tenant MongoDB.
/// Collection is created automatically by MongoDB on first insert.
/// </summary>
public class MongoInventoryService
{
    public const string CollectionName = "partfinder_inventory";

    private readonly ILocalSetupContext _setupContext;

    public MongoInventoryService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    // Returns null if DB not configured — callers treat null as "empty, no error"
    private IMongoCollection<InventoryDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<InventoryDoc>(CollectionName);
    }

    public virtual async Task<IReadOnlyList<InventoryDoc>> GetAllAsync(CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return Array.Empty<InventoryDoc>();

        try
        {
            return await coll.Find(FilterDefinition<InventoryDoc>.Empty)
                .SortBy(d => d.PartName)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch
        {
            // Collection may not exist yet — return empty, no crash
            return Array.Empty<InventoryDoc>();
        }
    }

    public virtual async Task<string> UpsertAsync(InventoryDoc doc, CancellationToken ct = default)
    {
        var coll = TryGetCollection()
            ?? throw new InvalidOperationException("No tenant database configured.");

        if (doc.MongoId == ObjectId.Empty)
            doc.MongoId = ObjectId.GenerateNewId();

        // MongoDB creates the collection automatically on first upsert
        await coll.ReplaceOneAsync(
            Builders<InventoryDoc>.Filter.Eq(d => d.MongoId, doc.MongoId),
            doc,
            new ReplaceOptions { IsUpsert = true },
            ct).ConfigureAwait(false);

        return doc.MongoId.ToString();
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null || !ObjectId.TryParse(id, out var oid)) return;

        try
        {
            await coll.DeleteOneAsync(
                Builders<InventoryDoc>.Filter.Eq(d => d.MongoId, oid), ct)
                .ConfigureAwait(false);
        }
        catch { /* ignore if collection doesn't exist */ }
    }
}

[BsonIgnoreExtraElements]
public sealed class InventoryDoc
{
    [BsonId]
    public ObjectId MongoId { get; set; }

    [BsonElement("partName")]
    public string PartName { get; set; } = string.Empty;

    [BsonElement("partId")]
    public string PartId { get; set; } = string.Empty;

    [BsonElement("category")]
    public string Category { get; set; } = string.Empty;

    [BsonElement("quantity")]
    public int Quantity { get; set; }

    [BsonElement("minStock")]
    public int MinStock { get; set; } = 10;

    [BsonElement("unitPrice")]
    public double UnitPrice { get; set; }

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("supplier")]
    public string Supplier { get; set; } = string.Empty;

    [BsonElement("lastUpdated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    [BsonElement("notes")]
    public string Notes { get; set; } = string.Empty;
}
