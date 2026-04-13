using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

public sealed class AdminAuthApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    static AdminAuthApiClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("PartFinder-WinUI/1.0");
    }

    public static async Task<(bool ok, string? error, LoginResponseBody? body)> LoginAsync(
        string email,
        string password,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/auth/admin/login",
            new { email, password },
            null,
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Login failed.", null);
        }

        try
        {
            var body = JsonSerializer.Deserialize<LoginResponseBody>(text, ReadJson);
            if (body is not null && !string.IsNullOrWhiteSpace(body.AccessToken))
            {
                return (true, null, body);
            }
        }
        catch
        {
        }

        return (false, ParseApiError(text), null);
    }

    public static async Task<(bool ok, string? error)> ChangePasswordAsync(
        string bearerToken,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/auth/admin/change-password",
            new { currentPassword, newPassword },
            bearerToken,
            ct).ConfigureAwait(true);
        if (!success)
        {
            return (false, err ?? ParseApiError(text));
        }

        return (true, null);
    }

    public static async Task<(bool ok, string? error)> SyncTotpSecretAsync(
        string bearerToken,
        string secretBase32,
        CancellationToken ct = default)
    {
        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/two-factor/sync",
            new { secretBase32 },
            bearerToken,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Sync failed.");
    }

    public static async Task<(bool ok, string? error)> ClearTotpOnServerAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/two-factor/clear",
            new { },
            bearerToken,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Clear failed.");
    }

    public static async Task<(bool ok, string? error)> ResetPasswordWithTotpAsync(
        string email,
        string totpCode,
        string newPassword,
        CancellationToken ct = default)
    {
        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/reset-password-with-totp",
            new { email, totpCode, newPassword },
            null,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Recovery failed.");
    }

    private static async Task<(bool success, string? error, string? body)> RawPostAsync(
        string path,
        object body,
        string? bearerToken,
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
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken.Trim());
        }

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

    public sealed class LoginResponseBody
    {
        [JsonPropertyName("accessToken")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("user")]
        public LoginUserDto? User { get; set; }
    }

    public sealed class LoginUserDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }
}
