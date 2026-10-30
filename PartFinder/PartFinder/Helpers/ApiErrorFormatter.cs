using System.Text.Json;

namespace PartFinder.Helpers;

internal static class ApiErrorFormatter
{
    public static string ToUserMessage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Request failed.";
        }

        var trimmed = raw.Trim();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                {
                    var text = msg.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return NormalizeMessage(text);
                    }
                }

                if (doc.RootElement.ValueKind == JsonValueKind.Array &&
                    doc.RootElement.GetArrayLength() > 0 &&
                    doc.RootElement[0].TryGetProperty("message", out var first))
                {
                    var text = first.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return NormalizeMessage(text);
                    }
                }
            }
            catch
            {
                // Fall through to raw text handling.
            }
        }

        if (trimmed.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return "Session expired. Sign out and sign in again.";
        }

        return trimmed.Length > 120 ? trimmed[..120] + "…" : trimmed;
    }

    private static string NormalizeMessage(string text)
    {
        if (text.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return "Session expired. Sign out and sign in again.";
        }

        return text;
    }
}
