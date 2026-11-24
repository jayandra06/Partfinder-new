using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace PartFinder.Services;

public readonly record struct MongoPingResult(bool Ok, string? ErrorDetail);

/// <summary>
/// Lightweight connectivity check used at login and setup so we fail fast with a clear message.
/// </summary>
public static class MongoConnectionTester
{
    /// <summary>Returns true when <c>ping</c> succeeds against the tenant database.</summary>
    public static async Task<bool> TryPingAsync(
        string connectionString,
        string? orgCode = null,
        CancellationToken ct = default)
    {
        var r = await PingAsync(connectionString, orgCode, ct).ConfigureAwait(true);
        return r.Ok;
    }

    /// <summary>
    /// Attempts to ping the tenant database. On failure, <see cref="MongoPingResult.ErrorDetail"/>
    /// contains a user-facing explanation (Atlas IP allowlist, credentials, etc.).
    /// </summary>
    public static async Task<MongoPingResult> PingAsync(
        string connectionString,
        string? orgCode = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return new MongoPingResult(false, "No MongoDB connection string was provided.");
        }

        string prepared;
        try
        {
            prepared = MongoConnectionStringUtil.Prepare(connectionString, orgCode);
        }
        catch (Exception ex)
        {
            return new MongoPingResult(false, $"Invalid MongoDB URI: {ex.Message}");
        }

        try
        {
            var client = new MongoClient(prepared);
            var db = MongoConnectionStringUtil.GetTenantDatabase(client, prepared, orgCode);
            _ = await db.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct)
                .ConfigureAwait(true);
            return new MongoPingResult(true, null);
        }
        catch (Exception ex)
        {
            return new MongoPingResult(false, FormatPingFailure(ex, prepared));
        }
    }

    private static string FormatPingFailure(Exception ex, string preparedConnectionString)
    {
        var isAtlas = preparedConnectionString.Contains(".mongodb.net", StringComparison.OrdinalIgnoreCase);

        // Walk the chain for common driver cases
        for (var cur = ex; cur != null; cur = cur.InnerException)
        {
            if (cur is MongoAuthenticationException)
            {
                return "MongoDB rejected the username or password. Check the credentials in your connection string.";
            }
        }

        var message = ex.Message;
        if (ContainsTimeout(message))
        {
            var sb = new StringBuilder();
            sb.Append("Cannot reach MongoDB (connection timed out). ");
            if (isAtlas)
            {
                sb.Append(
                    "For MongoDB Atlas: open Atlas → Network Access and allow your current IP (or 0.0.0.0/0 for development only), confirm the cluster is running (not paused), and ensure this network allows outbound TLS to port 27017. ");
            }
            else
            {
                sb.Append("Check VPN/firewall rules, the host name, and that MongoDB is listening on the expected port. ");
            }

            sb.Append("Technical detail: ");
            sb.Append(message);
            return sb.ToString();
        }

        if (message.Contains("authentication", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("bad auth", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return $"MongoDB authentication failed. Check the username and password. ({message})";
        }

        return $"MongoDB connection failed: {message}";
    }

    private static bool ContainsTimeout(string message)
    {
        return message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
               || message.Contains("timed out", StringComparison.OrdinalIgnoreCase)
               || message.Contains("selecting a server", StringComparison.OrdinalIgnoreCase);
    }
}
