using MongoDB.Driver;

namespace PartFinder.Services;

/// <summary>
/// <c>mongodb+srv://</c> implies TLS. Plain <c>mongodb://</c> does not unless <c>tls=true</c> / <c>ssl=true</c>.
/// MongoDB Atlas (<c>*.mongodb.net</c>) requires TLS, so standard URIs without TLS fail while SRV appears to "just work".
/// </summary>
internal static class MongoConnectionStringUtil
{
    public static string Normalize(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString ?? string.Empty;
        }

        var s = connectionString.Trim();

        // Security: reject obviously malformed or non-MongoDB URIs
        if (!s.StartsWith("mongodb://", StringComparison.OrdinalIgnoreCase) &&
            !s.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid MongoDB connection string: must start with mongodb:// or mongodb+srv://");
        }

        // Security: reject suspiciously long URIs (prevent log injection / buffer issues)
        if (s.Length > 2048)
        {
            throw new ArgumentException("MongoDB connection string exceeds maximum allowed length.");
        }

        if (s.StartsWith("mongodb+srv://", StringComparison.OrdinalIgnoreCase))
        {
            return s;
        }

        if (!s.Contains(".mongodb.net", StringComparison.OrdinalIgnoreCase))
        {
            return s;
        }

        if (QueryHasTls(s))
        {
            return s;
        }

        var sep = s.Contains('?') ? "&" : "?";
        return $"{s}{sep}tls=true";
    }

    /// <summary>
    /// Resolves the database name for tenant operations.
    /// Default provisioning stores each organization in a database named after its org code.
    /// A missing path, or an explicit "admin" path, must never be used: Atlas tenant users
    /// are not authorized to run application commands against the admin database.
    /// </summary>
    public static string ResolveDatabaseName(string? databaseName, string? orgCode = null)
    {
        if (!string.IsNullOrWhiteSpace(databaseName) &&
            !string.Equals(databaseName.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
        {
            return databaseName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(orgCode) &&
            !string.Equals(orgCode.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
        {
            return orgCode.Trim();
        }

        return "partfinder";
    }

    /// <summary>
    /// Inserts <paramref name="defaultDbName"/> when the URI has no database path, or when that path is "admin".
    /// Query parameters are preserved.
    /// </summary>
    public static string EnsureDatabase(string uri, string defaultDbName)
    {
        if (string.IsNullOrWhiteSpace(uri) || string.IsNullOrWhiteSpace(defaultDbName))
        {
            return uri ?? string.Empty;
        }

        var trimmed = uri.Trim();
        var qIndex = trimmed.IndexOf('?');
        var query = qIndex >= 0 ? trimmed[qIndex..] : string.Empty;
        var withoutQuery = qIndex >= 0 ? trimmed[..qIndex] : trimmed;
        var dbSegment = Uri.EscapeDataString(defaultDbName.Trim());

        var schemeIdx = withoutQuery.IndexOf("//", StringComparison.Ordinal);
        if (schemeIdx < 0)
        {
            return $"{trimmed}/{dbSegment}";
        }

        var pathStart = withoutQuery.IndexOf('/', schemeIdx + 2);
        if (pathStart < 0)
        {
            return $"{withoutQuery}/{dbSegment}{query}";
        }

        var path = withoutQuery[(pathStart + 1)..].Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(path) ||
            string.Equals(path, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return $"{withoutQuery[..pathStart]}/{dbSegment}{query}";
        }

        return trimmed;
    }

    /// <summary>
    /// Normalizes TLS settings and guarantees the tenant database is present in the URI.
    /// </summary>
    public static string Prepare(string? connectionString, string? orgCode = null)
    {
        var normalized = Normalize(connectionString);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        string? urlDatabaseName = null;
        try
        {
            urlDatabaseName = new MongoUrl(normalized).DatabaseName;
        }
        catch (MongoConfigurationException)
        {
            return normalized;
        }

        var dbName = ResolveDatabaseName(urlDatabaseName, orgCode);
        return EnsureDatabase(normalized, dbName);
    }

    /// <summary>
    /// Returns the tenant database instance using normalized connection settings.
    /// Never returns the "admin" database for tenant operations.
    /// </summary>
    public static IMongoDatabase GetTenantDatabase(
        MongoClient client,
        string connectionString,
        string? orgCode = null)
    {
        var url = new MongoUrl(connectionString);
        var dbName = ResolveDatabaseName(url.DatabaseName, orgCode);
        if (string.Equals(dbName, "admin", StringComparison.OrdinalIgnoreCase))
        {
            dbName = !string.IsNullOrWhiteSpace(orgCode) ? orgCode.Trim() : "partfinder";
        }
        return client.GetDatabase(dbName);
    }

    private static bool QueryHasTls(string uri)
    {
        var q = uri.IndexOf('?', StringComparison.Ordinal);
        if (q < 0)
        {
            return false;
        }

        var tail = uri.AsSpan(q + 1);
        return tail.Contains("tls=true", StringComparison.OrdinalIgnoreCase)
            || tail.Contains("tls=1", StringComparison.OrdinalIgnoreCase)
            || tail.Contains("ssl=true", StringComparison.OrdinalIgnoreCase)
            || tail.Contains("ssl=1", StringComparison.OrdinalIgnoreCase);
    }
}
