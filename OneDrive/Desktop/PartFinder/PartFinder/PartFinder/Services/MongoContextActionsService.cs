using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using PartFinder.Models;

namespace PartFinder.Services;

public sealed class MongoContextActionsService : IContextActionsService
{
    public const string CollectionName = "partfinder_context_actions";

    private readonly ILocalSetupContext _setupContext;

    public MongoContextActionsService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<ContextActionDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<ContextActionDoc>(CollectionName);
    }

    public async Task<IReadOnlyList<TemplateContextAction>> GetForSourceTemplateAsync(
        string sourceTemplateId,
        CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return Array.Empty<TemplateContextAction>();
        }

        var docs = await coll
            .Find(d => d.SourceTemplateId == sourceTemplateId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return docs.Select(Map).ToList();
    }

    public async Task SaveAsync(TemplateContextAction action, CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection()
                   ?? throw new InvalidOperationException(
                       "No tenant database is configured. Complete setup and ensure setup-state.json contains dbUri.");

        var existing = await coll.Find(d => d.ActionId == action.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        var doc = Map(action);
        doc.MongoId = existing?.MongoId ?? ObjectId.GenerateNewId();
        var filter = Builders<ContextActionDoc>.Filter.Eq(d => d.ActionId, action.Id);
        _ = await coll.ReplaceOneAsync(
                filter,
                doc,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DeleteAsync(string actionId, CancellationToken cancellationToken = default)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return;
        }

        _ = await coll.DeleteOneAsync(
                d => d.ActionId == actionId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static TemplateContextAction Map(ContextActionDoc d)
    {
        var rules = (d.MatchRules ?? [])
            .Select(
                r => new ContextActionMatchRule
                {
                    TargetFieldKey = r.TargetFieldKey ?? string.Empty,
                    SourceFieldKey = r.SourceFieldKey,
                    LiteralValue = r.LiteralValue,
                })
            .ToList();

        return new TemplateContextAction
        {
            Id = d.ActionId,
            SourceTemplateId = d.SourceTemplateId,
            SourceFieldKey = d.SourceFieldKey,
            MenuLabel = d.MenuLabel,
            TargetTemplateId = d.TargetTemplateId,
            MatchRules = rules,
            DisplayFieldKeys = d.DisplayFieldKeys,
        };
    }

    private static ContextActionDoc Map(TemplateContextAction a)
    {
        return new ContextActionDoc
        {
            ActionId = a.Id,
            SourceTemplateId = a.SourceTemplateId,
            SourceFieldKey = a.SourceFieldKey,
            MenuLabel = a.MenuLabel,
            TargetTemplateId = a.TargetTemplateId,
            MatchRules = a.MatchRules
                .Select(
                    r => new MatchRuleDoc
                    {
                        TargetFieldKey = r.TargetFieldKey,
                        SourceFieldKey = r.SourceFieldKey,
                        LiteralValue = r.LiteralValue,
                    })
                .ToList(),
            DisplayFieldKeys = a.DisplayFieldKeys?.ToList(),
        };
    }

    [BsonIgnoreExtraElements]
    private sealed class ContextActionDoc
    {
        [BsonId]
        public ObjectId MongoId { get; set; }

        [BsonElement("actionId")]
        public string ActionId { get; set; } = "";

        [BsonElement("sourceTemplateId")]
        public string SourceTemplateId { get; set; } = "";

        [BsonElement("sourceFieldKey")]
        public string SourceFieldKey { get; set; } = "";

        [BsonElement("menuLabel")]
        public string MenuLabel { get; set; } = "";

        [BsonElement("targetTemplateId")]
        public string TargetTemplateId { get; set; } = "";

        [BsonElement("matchRules")]
        public List<MatchRuleDoc> MatchRules { get; set; } = [];

        [BsonElement("displayFieldKeys")]
        public List<string>? DisplayFieldKeys { get; set; }
    }

    [BsonIgnoreExtraElements]
    private sealed class MatchRuleDoc
    {
        [BsonElement("targetFieldKey")]
        public string? TargetFieldKey { get; set; }

        [BsonElement("sourceFieldKey")]
        public string? SourceFieldKey { get; set; }

        [BsonElement("literalValue")]
        public string? LiteralValue { get; set; }
    }
}
