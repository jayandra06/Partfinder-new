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
