using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PartFinder.Core;
using PartFinder.Models;
using FieldSnapshot = PartFinder.Core.TemplateFieldMerge.FieldSnapshot;

namespace PartFinder.Services;

/// <summary>
/// Persists part templates in the tenant MongoDB database (<c>partfinder_templates</c> collection).
/// </summary>
public sealed class MongoTemplateSchemaService : ITemplateSchemaService
{
    public const string CollectionName = "partfinder_templates";
    public const string MasterDataTemplateId = ExplorerTemplateNames.TemplateId;
    /// <summary>Display name for the primary reference-data template (Explorer).</summary>
    public const string MasterDataTemplateName = ExplorerTemplateNames.DisplayName;
    public const string LegacyMasterDataTemplateName = ExplorerTemplateNames.LegacyDisplayName;

    public static bool IsExplorerTemplateName(string? name) =>
        ExplorerTemplateNames.IsExplorerTemplateName(name);

    private readonly ILocalSetupContext _setupContext;

    public MongoTemplateSchemaService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<TemplateDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<TemplateDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<PartTemplateDefinition>> GetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return Array.Empty<PartTemplateDefinition>();
        }

        var docs = await coll.Find(FilterDefinition<TemplateDoc>.Empty)
            .SortBy(d => d.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var byId = docs.ToDictionary(d => d.TemplateId, StringComparer.Ordinal);
        return docs.Select(d => MapToDefinition(d, byId)).ToList();
    }

    public async Task<PartTemplateDefinition?> GetTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return null;
        }

        var doc = await coll.Find(d => d.TemplateId == templateId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        var lookup = new Dictionary<string, TemplateDoc>(StringComparer.Ordinal) { [doc.TemplateId] = doc };
        if (!string.IsNullOrWhiteSpace(doc.BaseTemplateId))
        {
            var baseDoc = await coll.Find(d => d.TemplateId == doc.BaseTemplateId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (baseDoc is not null)
            {
                lookup[baseDoc.TemplateId] = baseDoc;
            }
        }

        return MapToDefinition(doc, lookup);
    }

    public async Task SaveTemplateAsync(
        PartTemplateDefinition template,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection()
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var existing = await coll.Find(d => d.TemplateId == template.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var doc = MapToDoc(template);
        doc.MongoId = existing?.MongoId ?? ObjectId.GenerateNewId();
        var filter = Builders<TemplateDoc>.Filter.Eq(d => d.TemplateId, template.Id);
        _ = await coll.ReplaceOneAsync(
                filter,
                doc,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteTemplateAsync(
        string templateId,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection()
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var filter = Builders<TemplateDoc>.Filter.Eq(d => d.TemplateId, templateId);
        await coll.DeleteOneAsync(filter, cancellationToken).ConfigureAwait(false);
    }

    private static PartTemplateDefinition MapToDefinition(
        TemplateDoc d,
        IReadOnlyDictionary<string, TemplateDoc>? docsByTemplateId = null)
    {
        IReadOnlyList<FieldSnapshot>? baseFields = null;
        if (!string.IsNullOrWhiteSpace(d.BaseTemplateId)
            && docsByTemplateId is not null
            && docsByTemplateId.TryGetValue(d.BaseTemplateId, out var baseDoc))
        {
            baseFields = baseDoc.Fields.Select(ToFieldSnapshot).ToList();
        }

        var childFields = d.Fields.Select(ToFieldSnapshot).ToList();
        var merged = TemplateFieldMerge.MergeInheritedFields(baseFields, childFields);
        var fields = merged
            .Select(
                f => new TemplateFieldDefinition
                {
                    Key = f.Key,
                    Label = f.Label,
                    Type = ParseFieldType(f.Type),
                    IsRequired = f.IsRequired,
                    DisplayOrder = f.DisplayOrder,
                    ValidationPattern = null,
                    Options = null,
                    LinkedTemplateId = f.LinkedTemplateId,
                    LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
                })
            .ToList();

        return new PartTemplateDefinition
        {
            Id = d.TemplateId,
            Name = d.Name,
            Version = d.Version,
            IsPublished = d.IsPublished,
            BaseTemplateId = string.IsNullOrWhiteSpace(d.BaseTemplateId) ? null : d.BaseTemplateId,
            Fields = fields,
        };
    }

    private static FieldSnapshot ToFieldSnapshot(FieldDoc f) =>
        new(
            f.Key,
            f.Label,
            f.Type,
            f.IsRequired,
            f.DisplayOrder,
            f.LinkedTemplateId,
            f.LinkedDisplayFieldKey);

    private static TemplateDoc MapToDoc(PartTemplateDefinition t)
    {
        return new TemplateDoc
        {
            TemplateId = t.Id,
            Name = t.Name,
            Version = t.Version,
            IsPublished = t.IsPublished,
            BaseTemplateId = t.BaseTemplateId,
            Fields = t.Fields
                .OrderBy(f => f.DisplayOrder)
                .Select(
                    f => new FieldDoc
                    {
                        Key = f.Key,
                        Label = f.Label,
                        Type = FieldTypeToString(f.Type),
                        IsRequired = f.IsRequired,
                        DisplayOrder = f.DisplayOrder,
                        ValidationPattern = f.ValidationPattern,
                        Options = f.Options?.ToList(),
                        LinkedTemplateId = f.LinkedTemplateId,
                        LinkedDisplayFieldKey = f.LinkedDisplayFieldKey,
                    })
                .ToList(),
        };
    }

    private static string FieldTypeToString(TemplateFieldType t) => t.ToString();

    private static TemplateFieldType ParseFieldType(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return TemplateFieldType.Text;
        }

        return Enum.TryParse<TemplateFieldType>(s, true, out var v) ? v : TemplateFieldType.Text;
    }

    [BsonIgnoreExtraElements]
    private sealed class TemplateDoc
    {
        [BsonId]
        public ObjectId MongoId { get; set; }

        [BsonElement("templateId")]
        public string TemplateId { get; set; } = "";

        [BsonElement("name")]
        public string Name { get; set; } = "";

        [BsonElement("version")]
        public int Version { get; set; } = 1;

        [BsonElement("isPublished")]
        public bool IsPublished { get; set; } = true;

        [BsonElement("baseTemplateId")]
        public string? BaseTemplateId { get; set; }

        [BsonElement("fields")]
        public List<FieldDoc> Fields { get; set; } = [];
    }

    [BsonIgnoreExtraElements]
    private sealed class FieldDoc
    {
        [BsonElement("key")]
        public string Key { get; set; } = "";

        [BsonElement("label")]
        public string Label { get; set; } = "";

        [BsonElement("type")]
        public string Type { get; set; } = "Text";

        [BsonElement("isRequired")]
        public bool IsRequired { get; set; }

        [BsonElement("displayOrder")]
        public int DisplayOrder { get; set; }

        [BsonElement("validationPattern")]
        public string? ValidationPattern { get; set; }

        [BsonElement("options")]
        public List<string>? Options { get; set; }

        [BsonElement("linkedTemplateId")]
        public string? LinkedTemplateId { get; set; }

        [BsonElement("linkedDisplayFieldKey")]
        public string? LinkedDisplayFieldKey { get; set; }
    }
}
