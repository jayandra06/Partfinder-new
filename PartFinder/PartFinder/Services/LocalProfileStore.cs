using System.Text.Json;

namespace PartFinder.Services;

public sealed class LocalProfileStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly ILocalSetupContext? _setupContext;
    private readonly string _baseDir;

    public LocalProfileStore(ILocalSetupContext? setupContext = null)
    {
        _setupContext = setupContext;
        _baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder");
    }

    public string? DisplayName { get; private set; }
    public string? Department { get; private set; }

    /// <summary>
    /// Absolute path to the saved avatar image, or null if none is set.
    /// </summary>
    public string? AvatarPath { get; private set; }

    public event Action? ProfileChanged;

    private (string filePath, string avatarDir) GetPaths()
    {
        var org = _setupContext?.OrgCode?.Trim();
        var email = _setupContext?.AdminEmail?.Trim();

        string folder;
        if (!string.IsNullOrWhiteSpace(org))
        {
            var userSegment = !string.IsNullOrWhiteSpace(email) ? SanitizeSegment(email) : "default";
            folder = Path.Combine(_baseDir, "profiles", SanitizeSegment(org), userSegment);
        }
        else
        {
            folder = Path.Combine(_baseDir, "profiles", "global");
        }

        return (
            Path.Combine(folder, "profile-state.json"),
            Path.Combine(folder, "avatars")
        );
    }

    private static string SanitizeSegment(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = input.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }

    public void Load()
    {
        try
        {
            var (filePath, _) = GetPaths();
            if (!File.Exists(filePath))
            {
                DisplayName = null;
                Department = null;
                AvatarPath = null;
                return;
            }

            var dto = JsonSerializer.Deserialize<ProfileState>(File.ReadAllText(filePath), Json);

            DisplayName = dto?.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = null;

            Department = dto?.Department?.Trim();
            if (string.IsNullOrWhiteSpace(Department)) Department = null;

            // Validate stored avatar path still exists
            var storedAvatar = dto?.AvatarPath?.Trim();
            AvatarPath = (!string.IsNullOrWhiteSpace(storedAvatar) && File.Exists(storedAvatar))
                ? storedAvatar
                : null;
        }
        catch
        {
            DisplayName = null;
            Department = null;
            AvatarPath = null;
        }
    }

    public void SaveProfile(string? displayName, string? department)
    {
        DisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = null;

        Department = department?.Trim();
        if (string.IsNullOrWhiteSpace(Department)) Department = null;

        Persist();
        ProfileChanged?.Invoke();
    }

    /// <summary>
    /// Copies the source image file into the app's avatar folder and saves the path.
    /// Returns the new avatar path on success, or null on failure.
    /// </summary>
    public string? SaveAvatar(string sourceFilePath)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return null;

            var (_, avatarDir) = GetPaths();
            Directory.CreateDirectory(avatarDir);

            // Use a stable per-user filename so old files are replaced automatically
            var ext = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp"))
                return null;

            var destPath = Path.Combine(avatarDir, $"avatar{ext}");

            // Remove any previous avatar files with different extensions
            foreach (var old in Directory.GetFiles(avatarDir, "avatar.*"))
            {
                if (!string.Equals(old, destPath, StringComparison.OrdinalIgnoreCase))
                    File.Delete(old);
            }

            File.Copy(sourceFilePath, destPath, overwrite: true);
            AvatarPath = destPath;
            Persist();
            ProfileChanged?.Invoke();
            return destPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Legacy helper kept for backward compatibility.</summary>
    public void SaveDisplayName(string? value)
    {
        SaveProfile(value, Department);
    }

    /// <summary>Clears all profile data (used on logout to prevent stale data for next user).</summary>
    public void Clear()
    {
        DisplayName = null;
        Department = null;
        AvatarPath = null;
        try
        {
            var (filePath, _) = GetPaths();
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch { }
    }

    private void Persist()
    {
        var (filePath, _) = GetPaths();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(
            filePath,
            JsonSerializer.Serialize(
                new ProfileState
                {
                    DisplayName = DisplayName,
                    Department = Department,
                    AvatarPath = AvatarPath,
                },
                Json));
    }

    private sealed class ProfileState
    {
        public string? DisplayName { get; set; }
        public string? Department { get; set; }
        public string? AvatarPath { get; set; }
    }
}
