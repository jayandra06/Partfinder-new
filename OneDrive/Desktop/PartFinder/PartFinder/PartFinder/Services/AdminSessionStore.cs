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

            var dto = JsonSerializer.Deserialize<SessionDto>(File.ReadAllText(_filePath), Json);
            AccessToken = dto?.AccessToken?.Trim();
            Email = dto?.Email?.Trim();
        }
        catch
        {
            AccessToken = null;
            Email = null;
        }
    }

    public void Save(string accessToken, string email)
    {
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

    private sealed class SessionDto
    {
        public string? AccessToken { get; set; }
        public string? Email { get; set; }
    }
}
