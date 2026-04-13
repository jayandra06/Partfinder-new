using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// Master data grid rows in tenant MongoDB (<c>partfinder_master_data_rows</c>).
/// </summary>
public sealed class MongoMasterDataRecordsService : IMasterDataRecordsService
{
    public const string CollectionName = "partfinder_master_data_rows";

    private readonly ILocalSetupContext _setupContext;

    public MongoMasterDataRecordsService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<MasterDataRowDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<MasterDataRowDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetRowsAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        var docs = await coll.Find(d => d.TemplateId == templateId)
            .SortBy(d => d.RowOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return docs.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetRowsByIdsAsync(
        string templateId,
        IReadOnlyList<string> rowIds,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null || rowIds.Count == 0)
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        var oids = new List<ObjectId>();
        foreach (var id in rowIds)
        {
            if (!string.IsNullOrWhiteSpace(id) && ObjectId.TryParse(id, out var oid))
            {
                oids.Add(oid);
            }
        }

        if (oids.Count == 0)
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        var filter = Builders<MasterDataRowDoc>.Filter.And(
            Builders<MasterDataRowDoc>.Filter.Eq(d => d.TemplateId, templateId),
            Builders<MasterDataRowDoc>.Filter.In(d => d.MongoId, oids));
        var docs = await coll.Find(filter)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return docs.Select(Map).ToList();
    }

    public async Task<string> UpsertRowAsync(
        string templateId,
        string? rowId,
        int rowOrder,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection()
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var bsonValues = new BsonDocument();
        foreach (var kv in values)
        {
            bsonValues[kv.Key] = kv.Value ?? string.Empty;
        }

        var id = string.IsNullOrWhiteSpace(rowId) || !ObjectId.TryParse(rowId, out var oid)
            ? ObjectId.GenerateNewId()
            : oid;
        var doc = new MasterDataRowDoc
        {
            MongoId = id,
            TemplateId = templateId,
            RowOrder = rowOrder,
            Values = bsonValues,
        };
        await coll.ReplaceOneAsync(
                Builders<MasterDataRowDoc>.Filter.Eq(d => d.MongoId, id),
                doc,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
        return id.ToString();
    }

    private static MasterDataRowRecord Map(MasterDataRowDoc d)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in d.Values.Names)
        {
            map[name] = BsonValueToString(d.Values[name]);
        }

        return new MasterDataRowRecord
        {
            Id = d.MongoId.ToString(),
            RowOrder = d.RowOrder,
            Values = map,
        };
    }

    private static string BsonValueToString(BsonValue v)
    {
        if (v.IsBsonNull || v == BsonNull.Value)
        {
            return string.Empty;
        }

        if (v.IsString)
        {
            return v.AsString;
        }

        return v.ToString() ?? string.Empty;
    }

    [BsonIgnoreExtraElements]
    private sealed class MasterDataRowDoc
    {
        [BsonId]
        public ObjectId MongoId { get; set; }

        [BsonElement("templateId")]
        public string TemplateId { get; set; } = "";

        [BsonElement("rowOrder")]
        public int RowOrder { get; set; }

        [BsonElement("values")]
        public BsonDocument Values { get; set; } = new();
    }
}
