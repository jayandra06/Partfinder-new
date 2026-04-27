using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// Loads parts grid rows from tenant MongoDB using the same persisted rows collection.
/// </summary>
public sealed class MongoPartsDataService : IPartsDataService
{
    private readonly ILocalSetupContext _setupContext;

    public MongoPartsDataService(ILocalSetupContext setupContext)
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
        return db.GetCollection<MasterDataRowDoc>(MongoMasterDataRecordsService.CollectionName);
    }

    public async Task<(IReadOnlyList<PartRecord> Records, bool HasMore)> GetPageAsync(
        string templateId,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return (Array.Empty<PartRecord>(), false);
        }

        var docs = await coll.Find(d => d.TemplateId == templateId)
            .SortBy(d => d.RowOrder)
            .Skip(offset)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = docs.Count > pageSize;
        var records = docs
            .Take(pageSize)
            .Select(Map)
            .ToList();
        return (records, hasMore);
    }

    private static PartRecord Map(MasterDataRowDoc d)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var name in d.Values.Names)
        {
            values[name] = ToDisplayValue(d.Values[name]);
        }

        return new PartRecord
        {
            Id = d.MongoId.ToString(),
            Values = values,
        };
    }

    private static object? ToDisplayValue(BsonValue v)
    {
        if (v.IsBsonNull || v == BsonNull.Value)
        {
            return string.Empty;
        }

        if (v.IsString)
        {
            return v.AsString;
        }

        if (v.IsInt32)
        {
            return v.AsInt32;
        }

        if (v.IsInt64)
        {
            return v.AsInt64;
        }

        if (v.IsDouble)
        {
            return v.AsDouble;
        }

        if (v.IsBoolean)
        {
            return v.AsBoolean;
        }

        if (v.IsDecimal128)
        {
            return v.AsDecimal128.ToString();
        }

        return v.ToString();
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
