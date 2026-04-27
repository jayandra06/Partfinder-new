using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// Persists starred template IDs per user in the tenant MongoDB database
/// (<c>partfinder_user_favourites</c> collection).
/// Each document represents one user's favourite set, keyed by email.
/// This means favourites follow the user across devices — any device that
/// connects to the same tenant database will see the same starred templates.
/// </summary>
public sealed class MongoFavouriteStore : IFavouriteStore
{
    public const string CollectionName = "partfinder_user_favourites";

    private readonly ILocalSetupContext _setupContext;
    private readonly AdminSessionStore _session;
    private readonly ActivityLogger _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private HashSet<string> _favourites = new(StringComparer.Ordinal);

    public MongoFavouriteStore(
        ILocalSetupContext setupContext,
        AdminSessionStore session,
        ActivityLogger logger)
    {
        _setupContext = setupContext;
        _session = session;
        _logger = logger;
    }

    // ── IFavouriteStore ───────────────────────────────────────────────────────

    public event EventHandler? FavouritesChanged;

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var coll = TryGetCollection();
            if (coll is null)
            {
                _favourites = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            var userEmail = GetUserEmail();
            if (string.IsNullOrWhiteSpace(userEmail))
            {
                _favourites = new HashSet<string>(StringComparer.Ordinal);
                return;
            }

            var doc = await coll
                .Find(d => d.UserEmail == userEmail)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            _favourites = doc?.FavouriteTemplateIds is not null
                ? new HashSet<string>(doc.FavouriteTemplateIds, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogSystemEvent(
                "MongoFavouriteStore.LoadAsync",
                $"Failed to load favourites from MongoDB — treating as empty. Error: {ex.Message}");
            _favourites = new HashSet<string>(StringComparer.Ordinal);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc/>
    public bool IsFavourite(string templateId)
        => _favourites.Contains(templateId);

    /// <inheritdoc/>
    public async Task ToggleAsync(string templateId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_favourites.Contains(templateId))
                _favourites.Remove(templateId);
            else
                _favourites.Add(templateId);

            await PersistAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogSystemEvent(
                "MongoFavouriteStore.ToggleAsync",
                $"Failed to persist favourite toggle for template '{templateId}'. Error: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }

        FavouritesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> GetAll()
        => _favourites;

    // ── Private helpers ───────────────────────────────────────────────────────

    private IMongoCollection<UserFavouritesDoc>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var url = new MongoUrl(cs);
        var client = new MongoClient(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<UserFavouritesDoc>(CollectionName);
    }

    private string? GetUserEmail()
    {
        // First try the admin session (logged-in user email)
        _session.Load();
        if (!string.IsNullOrWhiteSpace(_session.Email))
        {
            return _session.Email;
        }

        // Fallback: use adminEmail from setup-state.json
        _setupContext.Refresh();
        if (!string.IsNullOrWhiteSpace(_setupContext.AdminEmail))
        {
            return _setupContext.AdminEmail;
        }

        // Last fallback: use orgCode as a unique key so favourites always persist
        if (!string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            return $"org_{_setupContext.OrgCode}";
        }

        return null;
    }

    /// <summary>
    /// Writes the current in-memory set to MongoDB.
    /// Must be called while <see cref="_lock"/> is held.
    /// </summary>
    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var coll = TryGetCollection();
        if (coll is null)
        {
            return;
        }

        var userEmail = GetUserEmail();
        if (string.IsNullOrWhiteSpace(userEmail))
        {
            return;
        }

        var doc = new UserFavouritesDoc
        {
            UserEmail = userEmail,
            FavouriteTemplateIds = _favourites.ToList(),
        };

        var filter = Builders<UserFavouritesDoc>.Filter.Eq(d => d.UserEmail, userEmail);
        await coll.ReplaceOneAsync(
                filter,
                doc,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }

    // ── MongoDB document model ────────────────────────────────────────────────

    [BsonIgnoreExtraElements]
    private sealed class UserFavouritesDoc
    {
        [BsonId]
        public ObjectId MongoId { get; set; }

        [BsonElement("userEmail")]
        public string UserEmail { get; set; } = "";

        [BsonElement("favouriteTemplateIds")]
        public List<string> FavouriteTemplateIds { get; set; } = [];
    }
}
