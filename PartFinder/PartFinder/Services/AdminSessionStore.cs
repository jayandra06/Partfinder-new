using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

public sealed class AdminSessionStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public AdminSessionStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder");
        _filePath = Path.Combine(dir, "admin-session.json");
    }

    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }

    public bool HasSession => !string.IsNullOrWhiteSpace(AccessToken);

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                AccessToken = null;
                Email = null;
                return;
            }

            // Security: reject suspiciously large session files (max 8 KB)
            var info = new FileInfo(_filePath);
            if (info.Length > 8 * 1024)
            {
                AccessToken = null;
                Email = null;
                return;
            }

            var dto = JsonSerializer.Deserialize<SessionDto>(File.ReadAllText(_filePath), Json);
            var token = dto?.AccessToken?.Trim();
            var email = dto?.Email?.Trim();

            // Security: basic sanity on loaded values
            AccessToken = IsValidJwtShape(token) ? token : null;
            Email = IsValidEmail(email) ? email : null;
        }
        catch
        {
            AccessToken = null;
            Email = null;
        }
    }

    public void Save(string accessToken, string email)
    {
        // Security: validate before persisting
        if (!IsValidJwtShape(accessToken))
            throw new ArgumentException("Cannot save malformed access token.");
        if (!IsValidEmail(email))
            throw new ArgumentException("Cannot save invalid email.");

        AccessToken = accessToken.Trim();
        Email = email.Trim();
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(
            _filePath,
            JsonSerializer.Serialize(new SessionDto { AccessToken = AccessToken, Email = Email }, Json));
    }

    public void Clear()
    {
        AccessToken = null;
        Email = null;
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch
        {
        }
    }

    // Security: JWT must have exactly 3 dot-separated non-empty parts
    private static bool IsValidJwtShape(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 4096)
            return false;
        var parts = token.Split('.');
        return parts.Length == 3 && parts.All(p => p.Length > 0);
    }

    // Security: basic email sanity (not a full RFC check, just prevents garbage)
    private static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;
        var atIndex = email.IndexOf('@');
        return atIndex > 0 && atIndex < email.Length - 1;
    }

    private sealed class SessionDto
    {
        public string? AccessToken { get; set; }
        public string? Email { get; set; }
    }
}
