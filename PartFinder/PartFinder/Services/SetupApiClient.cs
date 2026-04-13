using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

public sealed class SetupOkUriResponse
{
    public bool Ok { get; set; }
    public string? OrgDatabaseUri { get; set; }
    public bool? ClientOnlyInit { get; set; }
}

public sealed class SetupOrgAdminResponse
{
    public bool Ok { get; set; }
    public bool Created { get; set; }
    public bool Skipped { get; set; }
}

public sealed class SetupTestOkResponse
{
    public bool Ok { get; set; }
}

public static class SetupApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static SetupApiClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("PartFinder-WinUI/1.0");
    }

    public static async Task<(SetupStatusResult? status, string? error)> StatusAsync(
        string orgCode,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/status",
            new { orgCode },
            ct).ConfigureAwait(true);
        if (!success)
        {
            return (null, err);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, err ?? "Empty response from server.");
        }

        try
        {
            var status = JsonSerializer.Deserialize<SetupStatusResult>(text, ReadJson);
            return (status, null);
        }
        catch
        {
            return (null, "Server returned data that could not be read.");
        }
    }

    public static async Task<(bool ok, string? error, string? orgDatabaseUri)> ProvisionDefaultAsync(
        string orgCode,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/database/provision-default",
            new { orgCode },
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupOkUriResponse>(text, ReadJson);
            if (o?.Ok == true && !string.IsNullOrWhiteSpace(o.OrgDatabaseUri))
            {
                return (true, null, o.OrgDatabaseUri);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    public static async Task<(bool ok, string? error, string? orgDatabaseUri)> SaveCustomDatabaseAsync(
        string orgCode,
        string uri,
        bool clientInitializationConfirmed,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["orgCode"] = orgCode,
            ["uri"] = uri,
        };
        if (clientInitializationConfirmed)
        {
            body["clientInitializationConfirmed"] = true;
        }

        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/database/save-custom",
            body,
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupOkUriResponse>(text, ReadJson);
            if (o?.Ok == true && !string.IsNullOrWhiteSpace(o.OrgDatabaseUri))
            {
                return (true, null, o.OrgDatabaseUri);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    public static async Task<(bool ok, string? error)> TestDatabaseAsync(
        string orgCode,
        string? uri,
        CancellationToken ct = default)
    {
        var body = string.IsNullOrWhiteSpace(uri)
            ? (object)new { orgCode }
            : new { orgCode, uri };
        var (success, err, text) = await RawPostAsync("/api/public/setup/database/test", body, ct)
            .ConfigureAwait(true);
        if (!success)
        {
            return (false, err ?? ParseApiError(text));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Invalid response.");
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupTestOkResponse>(text, ReadJson);
            return o?.Ok == true ? (true, null) : (false, err ?? "Unexpected response.");
        }
        catch
        {
            return (false, err ?? "Invalid response.");
        }
    }

    public static async Task<(bool ok, string? error, SetupOrgAdminResponse? body)> CreateOrgAdminAsync(
        string orgCode,
        string name,
        string email,
        string password,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/org-admin",
            new { orgCode, name, email, password },
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupOrgAdminResponse>(text, ReadJson);
            if (o?.Ok == true)
            {
                return (true, null, o);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    private static async Task<T?> PostBodyAsync<T>(string path, object body, CancellationToken ct)
        where T : class
    {
        var (success, _, text) = await RawPostAsync(path, body, ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(text, ReadJson);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<(bool success, string? error, string? body)> RawPostAsync(
        string path,
        object body,
        CancellationToken ct)
    {
        var baseUrl = LicenseApiClient.GetBaseUrl();
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        var json = JsonSerializer.Serialize(body, WriteJson);
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        HttpResponseMessage resp;
        try
        {
            resp = await Http.SendAsync(req, ct).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }

        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(true);
        if (resp.IsSuccessStatusCode)
        {
            return (true, null, text);
        }

        return (false, ParseApiError(text), text);
    }

    private static string ParseApiError(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "Request failed.";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("message", out var msg))
            {
                return "Request failed.";
            }

            if (msg.ValueKind == JsonValueKind.String)
            {
                return msg.GetString() ?? "Request failed.";
            }

            if (msg.ValueKind == JsonValueKind.Array && msg.GetArrayLength() > 0)
            {
                var first = msg[0];
                return first.ValueKind == JsonValueKind.String
                    ? first.GetString() ?? "Request failed."
                    : first.ToString();
            }
        }
        catch
        {
        }

        return "Request failed.";
    }
}
