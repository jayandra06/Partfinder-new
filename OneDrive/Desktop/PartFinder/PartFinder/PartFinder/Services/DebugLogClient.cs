using System.Net.Http.Json;
using System.Text.Json;

namespace PartFinder.Services;

/// <summary>
/// Sends diagnostic lines to PartFinder-Backend POST /api/admin/debug/logs/ingest.
/// Set DebugLog:IngestKey in appsettings.json and DEBUG_LOG_INGEST_KEY in API .env to the same value.
/// </summary>
public static class DebugLogClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(8),
    };

    public static void Post(string level, string message, string? context = null)
    {
        _ = PostAsync(level, message, context);
    }

    private static async Task PostAsync(string level, string message, string? context)
    {
        var key = ReadIngestKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var baseUrl = LicenseApiClient.GetBaseUrl().TrimEnd('/');
        var url = baseUrl + "/api/admin/debug/logs/ingest";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.TryAddWithoutValidation("X-Debug-Log-Ingest-Key", key.Trim());
            req.Content = JsonContent.Create(
                new
                {
                    source = "partfinder-desktop",
                    level,
                    message,
                    context,
                },
                options: new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await Http.SendAsync(req).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static string? ReadIngestKey()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("DebugLog", out var dbg))
            {
                return null;
            }

            if (!dbg.TryGetProperty("IngestKey", out var keyEl))
            {
                return null;
            }

            return keyEl.GetString();
        }
        catch
        {
            return null;
        }
    }
}
