using System.Collections.Concurrent;
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

    private const int InsertBatchSize = 50;

    private static readonly ConcurrentDictionary<string, MongoClient> ClientCache = new();

    private readonly ILocalSetupContext _setupContext;
    private readonly ITemplateSchemaService _templateSchema;

    public MongoMasterDataRecordsService(
        ILocalSetupContext setupContext,
        ITemplateSchemaService templateSchema)
    {
        _setupContext = setupContext;
        _templateSchema = templateSchema;
    }

    private bool TryGetClient(out MongoClient client)
    {
        client = null!;
        try
        {
            if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var cs = MongoConnectionStringUtil.Prepare(raw, _setupContext.OrgCode);
            client = ClientCache.GetOrAdd(cs, static connectionString => new MongoClient(connectionString));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] TryGetClient failed: {ex.Message}");
            return false;
        }
    }

    private string? GetOrgCodeOrNull()
    {
        _setupContext.Refresh();
        var code = _setupContext.OrgCode?.Trim();
        return string.IsNullOrWhiteSpace(code) ? null : code;
    }

    private IMongoCollection<MasterDataRowDoc>? TryGetCollection(MongoClient client)
    {
        try
        {
            if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var cs = MongoConnectionStringUtil.Prepare(raw, _setupContext.OrgCode);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, cs, _setupContext.OrgCode);
            if (string.Equals(db.DatabaseNamespace.DatabaseName, "admin", StringComparison.OrdinalIgnoreCase))
            {
                var targetDb = !string.IsNullOrWhiteSpace(_setupContext.OrgCode) ? _setupContext.OrgCode.Trim() : "partfinder";
                db = client.GetDatabase(targetDb);
            }
            return db.GetCollection<MasterDataRowDoc>(CollectionName);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] TryGetCollection failed: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetRowsAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryGetClient(out var client))
            {
                return Array.Empty<MasterDataRowRecord>();
            }

            var template = await _templateSchema
                .GetTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(false);
            var orgCode = GetOrgCodeOrNull();
            if (template is not null
                && MongoSharedPartsCatalog.IsCatalogTemplateName(template.Name)
                && orgCode is not null)
            {
                return await MongoSharedPartsCatalog.LoadRowsAsync(
                        client,
                        orgCode,
                        template.Fields,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var coll = TryGetCollection(client);
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] GetRowsAsync failed: {ex.Message}");
            return Array.Empty<MasterDataRowRecord>();
        }
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetStoredRowsAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryGetClient(out var client))
            {
                return Array.Empty<MasterDataRowRecord>();
            }

            var coll = TryGetCollection(client);
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] GetStoredRowsAsync failed: {ex.Message}");
            return Array.Empty<MasterDataRowRecord>();
        }
    }

    public async Task<int> GetRowCountAsync(string templateId, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!TryGetClient(out var client))
            {
                return 0;
            }

            var template = await _templateSchema
                .GetTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(false);
            var orgCode = GetOrgCodeOrNull();
            if (template is not null
                && MongoSharedPartsCatalog.IsCatalogTemplateName(template.Name)
                && orgCode is not null)
            {
                return await MongoSharedPartsCatalog
                    .CountRowsAsync(client, orgCode, cancellationToken)
                    .ConfigureAwait(false);
            }

            var coll = TryGetCollection(client);
            if (coll is null)
            {
                return 0;
            }

            return (int)await coll
                .CountDocumentsAsync(d => d.TemplateId == templateId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] GetRowCountAsync failed: {ex.Message}");
            return 0;
        }
    }

    public async Task<(IReadOnlyList<MasterDataRowRecord> Rows, int TotalCount)> GetRowsPageAsync(
        string templateId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return (Array.Empty<MasterDataRowRecord>(), 0);
        }

        try
        {
            if (!TryGetClient(out var client))
            {
                return (Array.Empty<MasterDataRowRecord>(), 0);
            }

            var template = await _templateSchema
                .GetTemplateAsync(templateId, cancellationToken)
                .ConfigureAwait(false);
            var orgCode = GetOrgCodeOrNull();

            if (template is not null
                && MongoSharedPartsCatalog.IsCatalogTemplateName(template.Name)
                && orgCode is not null)
            {
                var total = await MongoSharedPartsCatalog
                    .CountRowsAsync(client, orgCode, cancellationToken)
                    .ConfigureAwait(false);
                var (rows, _) = await MongoSharedPartsCatalog
                    .LoadPageAsync(client, orgCode, template.Fields, skip, take, cancellationToken)
                    .ConfigureAwait(false);
                return (rows, total);
            }

            var coll = TryGetCollection(client);
            if (coll is null)
            {
                return (Array.Empty<MasterDataRowRecord>(), 0);
            }

            var filter = Builders<MasterDataRowDoc>.Filter.Eq(d => d.TemplateId, templateId);
            var totalCount = (int)await coll
                .CountDocumentsAsync(filter, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var docs = await coll.Find(filter)
                .SortBy(d => d.RowOrder)
                .Skip(skip)
                .Limit(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return (docs.Select(Map).ToList(), totalCount);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MongoMasterDataRecordsService] GetRowsPageAsync failed: {ex.Message}");
            return (Array.Empty<MasterDataRowRecord>(), 0);
        }
    }

    public async Task<IReadOnlyList<MasterDataRowRecord>> GetRowsByIdsAsync(
        string templateId,
        IReadOnlyList<string> rowIds,
        CancellationToken cancellationToken = default)
    {
        if (rowIds.Count == 0 || !TryGetClient(out var client))
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

        var template = await _templateSchema
            .GetTemplateAsync(templateId, cancellationToken)
            .ConfigureAwait(false);
        var orgCode = GetOrgCodeOrNull();
        if (template is not null
            && MongoSharedPartsCatalog.IsCatalogTemplateName(template.Name)
            && orgCode is not null)
        {
            return await MongoSharedPartsCatalog.LoadRowsByIdsAsync(
                    client,
                    orgCode,
                    template.Fields,
                    oids,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var coll = TryGetCollection(client);
        if (coll is null)
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
        if (!TryGetClient(out var client))
        {
            throw new InvalidOperationException(
                "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");
        }

        var coll = TryGetCollection(client)
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

    public async Task<bool> DeleteRowAsync(
        string templateId,
        string rowId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rowId) || !ObjectId.TryParse(rowId, out var oid))
        {
            return false;
        }

        if (!TryGetClient(out var client))
        {
            return false;
        }

        var coll = TryGetCollection(client);
        if (coll is null)
        {
            return false;
        }

        var filter = Builders<MasterDataRowDoc>.Filter.And(
            Builders<MasterDataRowDoc>.Filter.Eq(d => d.TemplateId, templateId),
            Builders<MasterDataRowDoc>.Filter.Eq(d => d.MongoId, oid));
        var result = await coll.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount > 0;
    }

    public async Task<IReadOnlyList<string>> SaveRowsAsync(
        string templateId,
        IReadOnlyList<(string? Id, int RowOrder, IReadOnlyDictionary<string, string> Values)> rows,
        IReadOnlyList<string> deleteRowIds,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetClient(out var client))
        {
            throw new InvalidOperationException(
                "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");
        }

        var coll = TryGetCollection(client)
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var models = new List<WriteModel<MasterDataRowDoc>>(rows.Count + deleteRowIds.Count);
        var ids = new string[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var (rowId, rowOrder, values) = rows[i];
            var id = !string.IsNullOrWhiteSpace(rowId) && ObjectId.TryParse(rowId, out var existing)
                ? existing
                : ObjectId.GenerateNewId();
            ids[i] = id.ToString();
            var bsonValues = new BsonDocument();
            foreach (var kv in values)
            {
                bsonValues[kv.Key] = kv.Value ?? string.Empty;
            }

            models.Add(new ReplaceOneModel<MasterDataRowDoc>(
                Builders<MasterDataRowDoc>.Filter.Eq(d => d.MongoId, id),
                new MasterDataRowDoc
                {
                    MongoId = id,
                    TemplateId = templateId,
                    RowOrder = rowOrder,
                    Values = bsonValues,
                })
            {
                IsUpsert = true,
            });
        }

        foreach (var removedId in deleteRowIds)
        {
            if (!string.IsNullOrWhiteSpace(removedId) && ObjectId.TryParse(removedId, out var oid))
            {
                models.Add(new DeleteOneModel<MasterDataRowDoc>(
                    Builders<MasterDataRowDoc>.Filter.Eq(d => d.MongoId, oid)));
            }
        }

        if (models.Count > 0)
        {
            await coll.BulkWriteAsync(models, new BulkWriteOptions { IsOrdered = false }, cancellationToken)
                .ConfigureAwait(false);
        }

        return ids;
    }

    public async Task<int> AppendRowsAsync(
        string templateId,
        IEnumerable<IReadOnlyDictionary<string, string>> rows,
        IProgress<int>? insertedCount = null,
        CancellationToken cancellationToken = default)
    {
        var rowList = rows as IReadOnlyList<IReadOnlyDictionary<string, string>> ?? rows.ToList();
        if (rowList.Count == 0)
        {
            return 0;
        }

        if (!TryGetClient(out var client))
        {
            throw new InvalidOperationException(
                "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");
        }

        var coll = TryGetCollection(client)
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var startOrder = (int)await coll
            .CountDocumentsAsync(
                Builders<MasterDataRowDoc>.Filter.Eq(d => d.TemplateId, templateId),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var inserted = 0;
        for (var offset = 0; offset < rowList.Count; offset += InsertBatchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(InsertBatchSize, rowList.Count - offset);
            var docs = new List<MasterDataRowDoc>(count);
            for (var i = 0; i < count; i++)
            {
                var bsonValues = new BsonDocument();
                foreach (var kv in rowList[offset + i])
                {
                    bsonValues[kv.Key] = kv.Value ?? string.Empty;
                }

                docs.Add(
                    new MasterDataRowDoc
                    {
                        MongoId = ObjectId.GenerateNewId(),
                        TemplateId = templateId,
                        RowOrder = startOrder + inserted + i,
                        Values = bsonValues,
                    });
            }

            await coll.InsertManyAsync(docs, cancellationToken: cancellationToken).ConfigureAwait(false);
            inserted += docs.Count;
            insertedCount?.Report(inserted);
        }

        return inserted;
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
