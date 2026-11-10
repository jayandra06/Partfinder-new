using MongoDB.Bson;
using MongoDB.Driver;
using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// Legacy MSF parts live in the shared <c>partfinder.parts</c> collection, scoped by <c>org_code</c>.
/// Templates named <see cref="CatalogTemplateName"/> read from that collection instead of
/// <c>partfinder_master_data_rows</c>.
/// </summary>
internal static class MongoSharedPartsCatalog
{
    public const string SharedDatabaseName = "partfinder";
    public const string PartsCollectionName = "parts";
    public const string CatalogTemplateName = "partfinder.parts";
    public const string OrgCodeField = "org_code";

    public static bool IsCatalogTemplateName(string? templateName) =>
        string.Equals(templateName?.Trim(), CatalogTemplateName, StringComparison.OrdinalIgnoreCase);

    public static async Task<IReadOnlyList<MasterDataRowRecord>> LoadRowsAsync(
        MongoClient client,
        string orgCode,
        IReadOnlyList<TemplateFieldDefinition> fields,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        var coll = client.GetDatabase(SharedDatabaseName).GetCollection<BsonDocument>(PartsCollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq(OrgCodeField, orgCode.Trim());
        var docs = await coll.Find(filter)
            .SortBy(d => d["_id"])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return MapDocuments(docs, fields, rowOrderOffset: 0);
    }

    public static async Task<(IReadOnlyList<MasterDataRowRecord> Rows, bool HasMore)> LoadPageAsync(
        MongoClient client,
        string orgCode,
        IReadOnlyList<TemplateFieldDefinition> fields,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (pageSize <= 0)
        {
            return (Array.Empty<MasterDataRowRecord>(), false);
        }

        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return (Array.Empty<MasterDataRowRecord>(), false);
        }

        var coll = client.GetDatabase(SharedDatabaseName).GetCollection<BsonDocument>(PartsCollectionName);
        var filter = Builders<BsonDocument>.Filter.Eq(OrgCodeField, orgCode.Trim());
        var docs = await coll.Find(filter)
            .SortBy(d => d["_id"])
            .Skip(offset)
            .Limit(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = docs.Count > pageSize;
        var page = docs.Take(pageSize).ToList();
        var rows = MapDocuments(page, fields, offset);
        return (rows, hasMore);
    }

    public static async Task<IReadOnlyList<MasterDataRowRecord>> LoadRowsByIdsAsync(
        MongoClient client,
        string orgCode,
        IReadOnlyList<TemplateFieldDefinition> fields,
        IReadOnlyList<ObjectId> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return Array.Empty<MasterDataRowRecord>();
        }

        var coll = client.GetDatabase(SharedDatabaseName).GetCollection<BsonDocument>(PartsCollectionName);
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq(OrgCodeField, orgCode.Trim()),
            Builders<BsonDocument>.Filter.In("_id", ids));
        var docs = await coll.Find(filter)
            .SortBy(d => d["_id"])
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return MapDocuments(docs, fields, rowOrderOffset: 0);
    }

    internal static string ResolvePartFieldValue(BsonDocument doc, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        if (string.Equals(label, "_id", StringComparison.OrdinalIgnoreCase))
        {
            return doc.TryGetValue("_id", out var id) ? BsonValueToString(id) : string.Empty;
        }

        if (doc.TryGetValue(label, out var direct))
        {
            return BsonValueToString(direct);
        }

        foreach (var name in doc.Names)
        {
            if (string.Equals(name, label, StringComparison.OrdinalIgnoreCase))
            {
                return BsonValueToString(doc[name]);
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<MasterDataRowRecord> MapDocuments(
        IReadOnlyList<BsonDocument> docs,
        IReadOnlyList<TemplateFieldDefinition> fields,
        int rowOrderOffset)
    {
        var list = new List<MasterDataRowRecord>(docs.Count);
        for (var i = 0; i < docs.Count; i++)
        {
            var doc = docs[i];
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                values[field.Key] = ResolvePartFieldValue(doc, field.Label);
            }

            var id = doc.TryGetValue("_id", out var oid) ? oid.ToString()! : ObjectId.GenerateNewId().ToString();
            list.Add(
                new MasterDataRowRecord
                {
                    Id = id,
                    RowOrder = rowOrderOffset + i,
                    Values = values,
                });
        }

        return list;
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
}
