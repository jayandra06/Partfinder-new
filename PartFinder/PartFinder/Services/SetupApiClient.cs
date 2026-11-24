using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using PartFinder.Models;

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

public sealed class SetupInviteUserResponse
{
    public bool Ok { get; set; }
    public string? Email { get; set; }
    public string? OrganizationCode { get; set; }
    public string? TemporaryPassword { get; set; }
    public bool EmailSent { get; set; }
    public string? EmailError { get; set; }
}

public sealed class SetupListOrgUsersResponse
{
    public bool Ok { get; set; }
    public string? OrgDatabaseUri { get; set; }
    public List<SetupOrgUserListItem>? Users { get; set; }
}

public sealed class SetupOrgUserListItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool PartsAllTemplates { get; set; }
    public List<string>? AllowedTemplateIds { get; set; }
    public DateTime InvitedAtUtc { get; set; }
    public TemplatePermissionsDto? TemplatePermissions { get; set; }
    public MasterDataPermissionsDto? MasterDataPermissions { get; set; }
    public string? Status { get; set; }
    public bool IsOrgAdminDirectoryEntry { get; set; }
}

/// <summary>User row embedded in setup status (ISO date strings).</summary>
public sealed class SetupListOrgUserItem
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool PartsAllTemplates { get; set; }
    public List<string>? AllowedTemplateIds { get; set; }
    public string? InvitedAtUtc { get; set; }
    public TemplatePermissionsDto? TemplatePermissions { get; set; }
    public MasterDataPermissionsDto? MasterDataPermissions { get; set; }
    public string? Status { get; set; }
}

public sealed class SetupInviteLoginResponse
{
    public bool Ok { get; set; }
    public string? Message { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool MustChangePassword { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}

public sealed class SetupRefreshSessionResponse
{
    public bool Ok { get; set; }
    public string? Email { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Message { get; set; }
}

public static class SetupApiClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(45) };

    private static readonly JsonSerializerOptions WriteJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // Security: MaxDepth prevents JSON bomb attacks
    private static readonly JsonSerializerOptions ReadJson = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16,
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
        string email,
        string oldPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/org-admin",
            new { orgCode, email, oldPassword, newPassword },
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

    public static async Task<(bool ok, string? error, SetupInviteUserResponse? body)> InviteUserAsync(
        string orgCode,
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        TemplatePermissionsDto? templatePermissions = null,
        MasterDataPermissionsDto? masterDataPermissions = null,
        CancellationToken ct = default)
    {
        // Security: validate inputs before sending to server
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return (false, "Invalid name.", null);
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !email.Contains('@'))
            return (false, "Invalid email address.", null);
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
            return (false, "Invalid role. Must be Admin or Employee.", null);

        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/invite-user",
            new
            {
                orgCode,
                name,
                email,
                role,
                partsAllTemplates,
                allowedTemplateIds,
                templatePermissions,
                masterDataPermissions,
            },
            ct).ConfigureAwait(true);

        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupInviteUserResponse>(text, ReadJson);
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

    public static async Task<(bool ok, string? error, IReadOnlyList<OrgAppUserSummary>? users)> ListOrgUsersAsync(
        string orgCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            return (false, "Organisation code is required.", null);
        }

        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/list-org-users",
            new { orgCode = orgCode.Trim() },
            ct).ConfigureAwait(true);

        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Could not load users from the server.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupListOrgUsersResponse>(text, ReadJson);
            if (o?.Ok != true || o.Users is null)
            {
                return (false, err ?? ParseApiError(text), null);
            }

            if (!string.IsNullOrWhiteSpace(o.OrgDatabaseUri))
            {
                SetupStatePersistence.TryWriteDbUri(o.OrgDatabaseUri.Trim(), orgCode.Trim());
            }

            var mapped = o.Users
                .Where(u => !string.IsNullOrWhiteSpace(u.Id) && !string.IsNullOrWhiteSpace(u.Email))
                .Select(u => new OrgAppUserSummary
                {
                    Id = u.Id!,
                    Name = string.IsNullOrWhiteSpace(u.Name) ? u.Email! : u.Name!,
                    Email = u.Email!,
                    Role = string.IsNullOrWhiteSpace(u.Role) ? "Employee" : u.Role!,
                    PartsAllTemplates = u.PartsAllTemplates,
                    AllowedTemplateIds = u.AllowedTemplateIds ?? [],
                    InvitedAtUtc = u.InvitedAtUtc == default ? DateTime.UtcNow : u.InvitedAtUtc,
                    TemplatePermissions = u.TemplatePermissions,
                    MasterDataPermissions = u.MasterDataPermissions,
                    Status = string.IsNullOrWhiteSpace(u.Status) ? "Pending" : u.Status!,
                    IsOrgAdminDirectoryEntry = u.IsOrgAdminDirectoryEntry,
                })
                .ToList();

            return (true, null, mapped);
        }
        catch
        {
            return (false, "Server returned user data that could not be read.", null);
        }
    }

    public static IReadOnlyList<OrgAppUserSummary> MapOrgAppUserItems(
        IEnumerable<SetupListOrgUserItem>? items)
    {
        if (items is null)
        {
            return Array.Empty<OrgAppUserSummary>();
        }

        return items
            .Select(MapListOrgUserItem)
            .Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .ToList();
    }

    private static OrgAppUserSummary MapListOrgUserItem(SetupListOrgUserItem item)
    {
        DateTime invitedAt = default;
        if (!string.IsNullOrWhiteSpace(item.InvitedAtUtc)
            && DateTime.TryParse(
                item.InvitedAtUtc,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            invitedAt = parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();
        }

        var status = string.IsNullOrWhiteSpace(item.Status) ? "Pending" : item.Status.Trim();
        return new OrgAppUserSummary
        {
            Id = item.Id ?? string.Empty,
            Name = item.Name?.Trim() ?? string.Empty,
            Email = item.Email?.Trim() ?? string.Empty,
            Role = item.Role?.Trim() ?? "Employee",
            PartsAllTemplates = item.PartsAllTemplates,
            AllowedTemplateIds = item.AllowedTemplateIds ?? [],
            InvitedAtUtc = invitedAt,
            TemplatePermissions = item.TemplatePermissions,
            MasterDataPermissions = item.MasterDataPermissions,
            Status = status,
        };
    }

    private static bool ShouldRetryInviteLoginWithoutDeviceId(string? err, string? text)
    {
        var blob = $"{err}\n{text}";
        return blob.Contains("deviceId", StringComparison.OrdinalIgnoreCase)
               && blob.Contains("should not exist", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeNetworkError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return false;
        }

        var e = error.Trim();
        return e.Contains("actively refused", StringComparison.OrdinalIgnoreCase)
               || e.Contains("No such host", StringComparison.OrdinalIgnoreCase)
               || e.Contains("timed out", StringComparison.OrdinalIgnoreCase)
               || e.Contains("timeout", StringComparison.OrdinalIgnoreCase)
               || e.Contains("network", StringComparison.OrdinalIgnoreCase)
               || e.Contains("connection", StringComparison.OrdinalIgnoreCase)
               || e.Contains("Could not reach", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<(bool ok, string? error, SetupRefreshSessionResponse? body)> RefreshSessionAsync(
        string orgCode,
        string email,
        string deviceId,
        string refreshToken,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/refresh-session",
            new { orgCode, email, deviceId, refreshToken },
            ct).ConfigureAwait(true);

        if (!success)
        {
            return (false, err ?? ParseApiError(text), null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupRefreshSessionResponse>(text, ReadJson);
            if (o?.Ok == true && !string.IsNullOrWhiteSpace(o.AccessToken))
            {
                return (true, null, o);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    public static async Task<(bool ok, string? error)> RevokeSessionAsync(
        string orgCode,
        string email,
        string deviceId,
        string? refreshToken = null,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/revoke-session",
            new { orgCode, email, deviceId, refreshToken },
            ct).ConfigureAwait(true);

        return success ? (true, null) : (false, err ?? ParseApiError(text));
    }

    public static async Task<(bool ok, string? error, SetupInviteLoginResponse? body)> ValidateInviteLoginAsync(
        string orgCode,
        string email,
        string temporaryPassword,
        CancellationToken ct = default)
    {
        var deviceId = DeviceIdentityService.GetDeviceId();
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/invite-login",
            new
            {
                orgCode,
                email,
                temporaryPassword,
                deviceId,
            },
            ct).ConfigureAwait(true);

        // Older license APIs reject unknown fields (forbidNonWhitelisted) before deviceId was added.
        if (!success && ShouldRetryInviteLoginWithoutDeviceId(err, text))
        {
            (success, err, text) = await RawPostAsync(
                "/api/public/setup/invite-login",
                new
                {
                    orgCode,
                    email,
                    temporaryPassword,
                },
                ct).ConfigureAwait(true);
        }

        if (!success)
        {
            return (false, err ?? "Request failed.", null);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupInviteLoginResponse>(text, ReadJson);
            if (o is not null)
            {
                return (o.Ok, o.Ok ? null : (o.Message ?? "Invalid invited credentials."), o);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    public sealed class SetupForgotPasswordResponse
    {
        public bool Ok { get; set; }
        public string? Message { get; set; }
    }

    public static async Task<(bool ok, string? error, string? message)> RequestForgotPasswordAsync(
        string orgCode,
        string email,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/forgot-password",
            new { orgCode, email },
            ct).ConfigureAwait(true);

        if (!success || string.IsNullOrWhiteSpace(text))
        {
            return (false, err ?? "Request failed.", null);
        }

        try
        {
            var o = JsonSerializer.Deserialize<SetupForgotPasswordResponse>(text, ReadJson);
            if (o?.Ok == true)
            {
                return (true, null, o.Message);
            }
        }
        catch
        {
        }

        return (false, err ?? ParseApiError(text), null);
    }

    public static async Task<(bool ok, string? error)> ChangePasswordAsync(
        string orgCode,
        string email,
        string currentPassword,
        string newPassword,
        CancellationToken ct = default)
    {
        var (success, err, text) = await RawPostAsync(
            "/api/public/setup/change-password",
            new
            {
                orgCode,
                email,
                currentPassword,
                newPassword,
            },
            ct).ConfigureAwait(true);

        if (!success)
        {
            return (false, err ?? ParseApiError(text));
        }

        return (true, null);
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

        return (false, ParseHttpError(text, resp.StatusCode), text);
    }

    public static string FormatStatusFailureMessage(string? statusErr)
    {
        var api = LicenseApiClient.GetBaseUrl();
        var detail = string.IsNullOrWhiteSpace(statusErr) ? "Request failed." : statusErr.Trim();

        if (LooksLikeNetworkError(detail))
        {
            return
                $"Cannot reach the PartFinder API at {api}. {detail} Check your internet connection and LicenseApi:BaseUrl in appsettings.json.";
        }

        if (detail.Contains("server error", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("FUNCTION_INVOCATION", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("(50", StringComparison.Ordinal))
        {
            return
                $"The PartFinder API at {api} returned an error: {detail} The hosted backend may need redeploying, or run PartFinder-Backend locally and set LicenseApi:BaseUrl to http://127.0.0.1:3000 in appsettings.json.";
        }

        return
            $"Cannot complete setup against {api}. {detail} Check LicenseApi:BaseUrl in appsettings.json (use http://127.0.0.1:3000 when running the API locally).";
    }

    private static string ParseHttpError(string? body, System.Net.HttpStatusCode statusCode)
    {
        if ((int)statusCode >= 500)
        {
            var snippet = body?.Trim();
            if (!string.IsNullOrEmpty(snippet) && snippet.Length <= 280)
            {
                return $"Server error ({(int)statusCode}): {snippet}";
            }

            return $"Server error ({(int)statusCode}). The API at {LicenseApiClient.GetBaseUrl()} may be down or misconfigured.";
        }

        if (statusCode == System.Net.HttpStatusCode.NotFound)
        {
            var snippet = body?.Trim();
            if (string.IsNullOrEmpty(snippet))
            {
                return
                    "The API at backend.partfinder.store returned 404 (not deployed). In Vercel, set Root Directory to PartFinder-Backend, add MONGODB_URI and JWT_SECRET, deploy the latest main branch, then try again.";
            }

            if (!string.IsNullOrEmpty(snippet) &&
                (snippet.Contains("Cannot POST", StringComparison.OrdinalIgnoreCase) ||
                 snippet.Contains("Cannot GET", StringComparison.OrdinalIgnoreCase)))
            {
                return
                    "The license API does not support forgot-password yet. Deploy the latest PartFinder-Backend to backend.partfinder.store, then try again.";
            }
        }

        return ParseApiError(body);
    }

    private static string ParseApiError(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "Request failed.";
        }

        var trimmed = json.Trim();
        if (trimmed.StartsWith("Cannot POST", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("Cannot GET", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
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
