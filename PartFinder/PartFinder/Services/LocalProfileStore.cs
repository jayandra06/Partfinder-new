using System.Text.Json;

namespace PartFinder.Services;

public sealed class LocalProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public LocalProfileStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder");
        _filePath = Path.Combine(dir, "profile-state.json");
    }

    public string? DisplayName { get; private set; }

    public event Action? ProfileChanged;

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                DisplayName = null;
                return;
            }

            var dto = JsonSerializer.Deserialize<ProfileState>(File.ReadAllText(_filePath), Json);
            DisplayName = dto?.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = null;
            }
        }
        catch
        {
            DisplayName = null;
        }
    }

    public void SaveDisplayName(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = null;
        }

        DisplayName = normalized;
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(
            _filePath,
            JsonSerializer.Serialize(new ProfileState { DisplayName = DisplayName }, Json));
        ProfileChanged?.Invoke();
    }

    private sealed class ProfileState
    {
        public string? DisplayName { get; set; }
    }
}
