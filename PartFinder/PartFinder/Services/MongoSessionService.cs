using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>
/// Manages user login sessions in MongoDB "user_sessions" collection.
/// Non-blocking — if MongoDB is unavailable, operations silently fail
/// without affecting the existing login flow.
/// </summary>
public sealed class MongoSessionService
{
    public const string CollectionName = "user_sessions";

    private readonly ILocalSetupContext _setupContext;

    // Reuse cached MongoClient (thread-safe, expensive to create)
    private static readonly ConcurrentDictionary<string, MongoClient> _clientCache = new();

    // Current device session ID — generated once per app launch
    private static readonly string _currentSessionId = Guid.NewGuid().ToString("N");

    public static string CurrentSessionId => _currentSessionId;

    public MongoSessionService(ILocalSetupContext setupContext)
    {
        _setupContext = setupContext;
    }

    private IMongoCollection<LoginSessionRecord>? TryGetCollection()
    {
        if (!_setupContext.TryGetTenantMongoUri(out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;

        var cs = MongoConnectionStringUtil.Normalize(raw);
        var client = _clientCache.GetOrAdd(cs, static c => new MongoClient(c));
        var url = new MongoUrl(cs);
        var db = client.GetDatabase(url.DatabaseName ?? "admin");
        return db.GetCollection<LoginSessionRecord>(CollectionName);
    }

    /// <summary>
    /// Creates a new session record when user logs in.
    /// Fire-and-forget safe — never throws.
    /// </summary>
    public async Task CreateSessionAsync(string userEmail, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return;

        try
        {
            var deviceName = Environment.MachineName;
            var ipAddress = GetLocalIpAddress();
            var osVersion = Environment.OSVersion.ToString();

            // Get location in parallel with timeout (don't block if it fails)
            var locationTask = GetLocationFromIpAsync(ipAddress, ct);
            var location = "Unknown";
            try
            {
                if (await Task.WhenAny(locationTask, Task.Delay(3000, ct)).ConfigureAwait(false) == locationTask)
                {
                    location = await locationTask.ConfigureAwait(false);
                }
            }
            catch { /* Location fetch failed, use default */ }

            var record = new LoginSessionRecord
            {
                MongoId = ObjectId.GenerateNewId(),
                UserId = userEmail.Trim().ToLowerInvariant(),
                SessionId = _currentSessionId,
                DeviceName = deviceName,
                IpAddress = ipAddress,
                Location = location,
                LoginTime = DateTime.UtcNow,
                LastActivityTime = DateTime.UtcNow,
                IsActive = true,
                UserAgent = "PartFinder-WinUI/1.0",
                OsVersion = osVersion,
            };

            await coll.InsertOneAsync(record, cancellationToken: ct).ConfigureAwait(false);

            // Ensure indexes for fast queries (background, non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var indexKeys = Builders<LoginSessionRecord>.IndexKeys
                        .Ascending(r => r.UserId)
                        .Descending(r => r.LoginTime);
                    var indexModel = new CreateIndexModel<LoginSessionRecord>(indexKeys,
                        new CreateIndexOptions { Background = true, Name = "userId_loginTime" });
                    await coll.Indexes.CreateOneAsync(indexModel, cancellationToken: default).ConfigureAwait(false);
                }
                catch { /* Index creation failed, continue */ }
            }, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Session creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all active sessions for a user.
    /// </summary>
    public async Task<IReadOnlyList<LoginSessionRecord>> GetActiveSessionsAsync(
        string userEmail, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return Array.Empty<LoginSessionRecord>();

        try
        {
            var filter = Builders<LoginSessionRecord>.Filter.And(
                Builders<LoginSessionRecord>.Filter.Eq(r => r.UserId, userEmail.Trim().ToLowerInvariant()),
                Builders<LoginSessionRecord>.Filter.Eq(r => r.IsActive, true));

            return await coll.Find(filter)
                .SortByDescending(r => r.LoginTime)
                .Limit(50)
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return Array.Empty<LoginSessionRecord>();
        }
    }

    /// <summary>
    /// Logs out a specific session by marking it inactive.
    /// </summary>
    public async Task<bool> LogoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return false;

        try
        {
            var filter = Builders<LoginSessionRecord>.Filter.Eq(r => r.SessionId, sessionId);
            var update = Builders<LoginSessionRecord>.Update.Set(r => r.IsActive, false);
            var result = await coll.UpdateOneAsync(filter, update, cancellationToken: ct).ConfigureAwait(false);
            return result.ModifiedCount > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Marks the current device session as inactive (called on logout).
    /// </summary>
    public async Task DeactivateCurrentSessionAsync(CancellationToken ct = default)
    {
        await LogoutSessionAsync(_currentSessionId, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Logs out all sessions except the current one.
    /// </summary>
    public async Task<int> LogoutAllOtherSessionsAsync(string userEmail, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return 0;

        try
        {
            var filter = Builders<LoginSessionRecord>.Filter.And(
                Builders<LoginSessionRecord>.Filter.Eq(r => r.UserId, userEmail.Trim().ToLowerInvariant()),
                Builders<LoginSessionRecord>.Filter.Eq(r => r.IsActive, true),
                Builders<LoginSessionRecord>.Filter.Ne(r => r.SessionId, _currentSessionId));

            var update = Builders<LoginSessionRecord>.Update.Set(r => r.IsActive, false);
            var result = await coll.UpdateManyAsync(filter, update, cancellationToken: ct).ConfigureAwait(false);
            return (int)result.ModifiedCount;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Updates the last activity time for the current session.
    /// </summary>
    public async Task TouchCurrentSessionAsync(CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return;

        try
        {
            var filter = Builders<LoginSessionRecord>.Filter.Eq(r => r.SessionId, _currentSessionId);
            var update = Builders<LoginSessionRecord>.Update.Set(r => r.LastActivityTime, DateTime.UtcNow);
            await coll.UpdateOneAsync(filter, update, cancellationToken: ct).ConfigureAwait(false);
        }
        catch { /* Silent */ }
    }

    /// <summary>
    /// Claims an existing session by updating its SessionId to the current app instance's ID.
    /// Used when the app restarts but the session is still active in MongoDB.
    /// </summary>
    public async Task ClaimSessionAsync(string oldSessionId, string newSessionId, CancellationToken ct = default)
    {
        var coll = TryGetCollection();
        if (coll is null) return;

        try
        {
            var filter = Builders<LoginSessionRecord>.Filter.Eq(r => r.SessionId, oldSessionId);
            var update = Builders<LoginSessionRecord>.Update
                .Set(r => r.SessionId, newSessionId)
                .Set(r => r.LastActivityTime, DateTime.UtcNow);
            await coll.UpdateOneAsync(filter, update, cancellationToken: ct).ConfigureAwait(false);
        }
        catch { /* Silent */ }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint)
                return endPoint.Address.ToString();
        }
        catch { /* ignore */ }
        return "Unknown";
    }

    private static async Task<string> GetLocationFromIpAsync(string ipAddress, CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var url = $"http://ip-api.com/json/?query={ipAddress}&fields=city,regionName,country";
            var response = await http.GetStringAsync(url, ct).ConfigureAwait(false);
            
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var city = root.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String 
                ? c.GetString() : null;
            var region = root.TryGetProperty("regionName", out var r) && r.ValueKind == JsonValueKind.String 
                ? r.GetString() : null;
            var country = root.TryGetProperty("country", out var co) && co.ValueKind == JsonValueKind.String 
                ? co.GetString() : null;

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(city)) parts.Add(city!);
            if (!string.IsNullOrWhiteSpace(region)) parts.Add(region!);
            if (!string.IsNullOrWhiteSpace(country)) parts.Add(country!);

            return parts.Count > 0 ? string.Join(", ", parts) : ipAddress;
        }
        catch
        {
            return ipAddress;
        }
    }
}
