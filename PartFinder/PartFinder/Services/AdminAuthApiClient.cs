using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

public sealed class AdminAuthApiClient
{
    // Security: strict timeout + response size cap (5 MB)
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = TimeSpan.FromSeconds(10),
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 5 * 1024 * 1024,
    };

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Security: MaxDepth prevents deeply-nested JSON bomb attacks
    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16,
    };

    static AdminAuthApiClient()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("PartFinder-WinUI/1.0");
    }

    public static async Task<(bool ok, string? error, LoginResponseBody? body)> LoginAsync(
        string email,
        string password,
        string? ipAddress = null,
        double? latitude = null,
        double? longitude = null,
        string? deviceName = null,
        string? osVersion = null,
        CancellationToken ct = default)
    {
        // Security: basic input sanity before sending to server
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return (false, "Invalid email.", null);
        if (string.IsNullOrWhiteSpace(password) || password.Length > 256)
            return (false, "Invalid password.", null);

        var (success, err, text) = await RawPostAsync(
            "/api/auth/admin/login",
            new 
            { 
                email = email.Trim(), 
                password,
                ipAddress,
                latitude,
                longitude,
                deviceName,
                osVersion
            },
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
                // Security: token sanity check — must look like a JWT (3 dot-separated parts)
                if (!IsValidJwtShape(body.AccessToken))
                    return (false, "Received malformed token from server.", null);

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
        string? email = null,
        CancellationToken ct = default)
    {
        // The server will perform full validation on the bearerToken
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length > 256)
            return (false, "Invalid new password.");

        var (success, err, text) = await RawPostAsync(
            "/api/auth/admin/change-password",
            new { currentPassword, newPassword, email },
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
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.");
        // Security: TOTP secret must be valid Base32 (only A-Z, 2-7, =)
        if (string.IsNullOrWhiteSpace(secretBase32) || !IsValidBase32(secretBase32))
            return (false, "Invalid TOTP secret format.");

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
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.");

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
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return (false, "Invalid email.");
        // Security: TOTP code must be exactly 6 digits
        if (string.IsNullOrWhiteSpace(totpCode) || totpCode.Length != 6 || !totpCode.All(char.IsDigit))
            return (false, "TOTP code must be exactly 6 digits.");
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length > 256)
            return (false, "Invalid new password.");

        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/reset-password-with-totp",
            new { email = email.Trim(), totpCode, newPassword },
            null,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Recovery failed.");
    }

    public static async Task<(bool ok, string? error, LoginUserDto? user)> GetProfileAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.", null);

        var (success, err, text) = await RawGetAsync(
            "/api/auth/admin/profile",
            bearerToken,
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Failed to fetch profile.", null);
        }

        try
        {
            var user = JsonSerializer.Deserialize<LoginUserDto>(text, ReadJson);
            return (true, null, user);
        }
        catch
        {
            return (false, "Failed to parse profile data.", null);
        }
    }

    public static async Task<(bool ok, string? error, List<LoginSessionDto>? history)> GetLoginHistoryAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.", null);

        var (success, err, text) = await RawGetAsync(
            "/api/auth/admin/login-history",
            bearerToken,
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Failed to fetch history.", null);
        }

        try
        {
            var history = JsonSerializer.Deserialize<List<LoginSessionDto>>(text, ReadJson);
            return (true, null, history);
        }
        catch
        {
            return (false, "Failed to parse history data.", null);
        }
    }

    public static async Task<(bool ok, string? error, List<LoginSessionDto>? sessions)> GetActiveSessionsAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.", null);

        var (success, err, text) = await RawGetAsync(
            "/api/auth/admin/active-sessions",
            bearerToken,
            ct).ConfigureAwait(true);
        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Failed to fetch sessions.", null);
        }

        try
        {
            var sessions = JsonSerializer.Deserialize<List<LoginSessionDto>>(text, ReadJson);
            return (true, null, sessions);
        }
        catch
        {
            return (false, "Failed to parse session data.", null);
        }
    }

    public static async Task<(bool ok, string? error)> LogoutSessionAsync(
        string bearerToken,
        string sessionId,
        CancellationToken ct = default)
    {
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.");

        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/logout-session",
            new { sessionId },
            bearerToken,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Logout failed.");
    }

    public static async Task<(bool ok, string? error)> LogoutAllSessionsAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        if (!IsValidJwtShape(bearerToken))
            return (false, "Invalid session token.");

        var (success, err, _) = await RawPostAsync(
            "/api/auth/admin/logout-all-sessions",
            new { },
            bearerToken,
            ct).ConfigureAwait(true);
        return success ? (true, null) : (false, err ?? "Logout failed.");
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
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken.Trim()}");
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

    private static async Task<(bool success, string? error, string? body)> RawGetAsync(
        string path,
        string bearerToken,
        CancellationToken ct)
    {
        var baseUrl = LicenseApiClient.GetBaseUrl();
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearerToken.Trim()}");
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

    // Security: JWT must have exactly 3 dot-separated non-empty parts
    private static bool IsValidJwtShape(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 8192)
            return false;
        return token.Length > 10;
    }

    // Security: Base32 alphabet only (RFC 4648)
    private static bool IsValidBase32(string s)
    {
        const string base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567=";
        return s.ToUpperInvariant().All(c => base32Chars.Contains(c));
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

        [JsonPropertyName("lastLoginIp")]
        public string? LastLoginIp { get; set; }

        [JsonPropertyName("lastLoginLat")]
        public double LastLoginLat { get; set; }

        [JsonPropertyName("lastLoginLon")]
        public double LastLoginLon { get; set; }

        [JsonPropertyName("lastLoginLocation")]
        public string? LastLoginLocation { get; set; }

        [JsonPropertyName("lastSession")]
        public LoginSessionDto? LastSession { get; set; }
    }

    public sealed class LoginSessionDto
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }

        [JsonPropertyName("ipAddress")]
        public string? IpAddress { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("city")]
        public string? City { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("browser")]
        public string? Browser { get; set; }

        [JsonPropertyName("operatingSystem")]
        public string? OperatingSystem { get; set; }

        [JsonPropertyName("deviceType")]
        public string? DeviceType { get; set; }

        [JsonPropertyName("deviceName")]
        public string? DeviceName { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("suspiciousReason")]
        public string? SuspiciousReason { get; set; }

        [JsonPropertyName("loginTime")]
        public DateTime LoginTime { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; }
    }
}
