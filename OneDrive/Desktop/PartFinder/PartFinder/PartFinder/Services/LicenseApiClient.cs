using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

public sealed record LicenseVerifyResponse(
    bool Valid,
    string? Reason,
    string? Message,
    string? OrganizationName,
    string? OrgCode,
    string? MaintenanceUntil = null);

public static class LicenseApiClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(20),
    };

    private static readonly JsonSerializerOptions SerializeJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    static LicenseApiClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("PartFinder-WinUI/1.0");
    }

    public static string GetBaseUrl()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(path))
            {
                return "http://localhost:3000";
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("LicenseApi", out var licenseApi) &&
                licenseApi.TryGetProperty("BaseUrl", out var baseUrlEl))
            {
                var s = baseUrlEl.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return s.TrimEnd('/');
                }
            }
        }
        catch
        {
        }

        return "http://localhost:3000";
    }

    public static async Task<LicenseVerifyResponse?> VerifyAsync(string orgCode, CancellationToken ct = default)
    {
        var baseUrl = GetBaseUrl();
        var url = $"{baseUrl}/api/public/license/verify";

        var payload = JsonSerializer.Serialize(new { orgCode }, SerializeJson);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
        {
            CharSet = "utf-8",
        };

        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, ct).ConfigureAwait(true);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return response.IsSuccessStatusCode
                ? new LicenseVerifyResponse(false, "EMPTY_RESPONSE", "Empty response from license server.", null, null, null)
                : new LicenseVerifyResponse(
                    false,
                    "HTTP_ERROR",
                    $"License server returned {(int)response.StatusCode} with an empty body.",
                    null,
                    null,
                    null);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                var err = TryReadApiErrorMessage(root);
                var maintUntilErr = ReadString(root, "maintenanceUntil");
                return new LicenseVerifyResponse(
                    false,
                    "HTTP_ERROR",
                    err ?? $"License server error ({(int)response.StatusCode}).",
                    null,
                    null,
                    maintUntilErr);
            }

            var valid = root.TryGetProperty("valid", out var v) && v.ValueKind == JsonValueKind.True;
            var reason = ReadString(root, "reason");
            var message = ReadString(root, "message");
            var orgName = ReadString(root, "organizationName");
            var code = ReadString(root, "orgCode");
            var maintenanceUntil = ReadString(root, "maintenanceUntil");

            return new LicenseVerifyResponse(valid, reason, message, orgName, code, maintenanceUntil);
        }
        catch (JsonException)
        {
            return new LicenseVerifyResponse(false, "INVALID_RESPONSE", "Invalid response from license server.", null, null, null);
        }
    }

    private static string? TryReadApiErrorMessage(JsonElement root)
    {
        if (!root.TryGetProperty("message", out var msg))
        {
            return null;
        }

        if (msg.ValueKind == JsonValueKind.String)
        {
            return msg.GetString();
        }

        if (msg.ValueKind == JsonValueKind.Array && msg.GetArrayLength() > 0)
        {
            var first = msg[0];
            return first.ValueKind == JsonValueKind.String ? first.GetString() : first.ToString();
        }

        return null;
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var el))
        {
            return null;
        }

        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }
}