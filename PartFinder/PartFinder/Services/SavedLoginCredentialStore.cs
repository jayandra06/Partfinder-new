using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Credentials;

namespace PartFinder.Services;

/// <summary>
/// Stores refresh tokens in Windows Credential Locker; org/email in a small local profile file (no secrets).
/// </summary>
public sealed class SavedLoginCredentialStore
{
    private const string VaultResource = "PartFinder";
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _profilePath;
    private readonly string _promptStatePath;

    public SavedLoginCredentialStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder");
        _profilePath = Path.Combine(dir, "saved-login-profile.json");
        _promptStatePath = Path.Combine(dir, "saved-login-prompt-state.json");
    }

    /// <summary>
    /// Whether to show the "Save Login Information?" dialog for this email (once per device).
    /// </summary>
    public bool ShouldOfferSaveLoginPrompt(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
        {
            return false;
        }

        if (WasSaveLoginPromptAnswered(normalized))
        {
            return false;
        }

        var profile = LoadProfile();
        if (profile is not null
            && string.Equals(NormalizeEmail(profile.Email), normalized, StringComparison.Ordinal)
            && TryReadRefreshToken(profile) is not null)
        {
            return false;
        }

        return true;
    }

    private bool WasSaveLoginPromptAnswered(string normalizedEmail) =>
        LoadPromptState().AnsweredEmails.Contains(normalizedEmail, StringComparer.Ordinal);

    public void MarkSaveLoginPromptAnswered(string email)
    {
        var normalized = NormalizeEmail(email);
        if (normalized is null)
        {
            return;
        }

        var state = LoadPromptState();
        if (state.AnsweredEmails.Contains(normalized, StringComparer.Ordinal))
        {
            return;
        }

        state.AnsweredEmails.Add(normalized);
        PersistPromptState(state);
    }

    public bool HasSavedProfile()
    {
        var profile = LoadProfile();
        return profile is not null
               && !string.IsNullOrWhiteSpace(profile.OrgCode)
               && !string.IsNullOrWhiteSpace(profile.Email)
               && TryReadRefreshToken(profile) is not null;
    }

    public SavedLoginProfile? LoadProfile()
    {
        try
        {
            if (!File.Exists(_profilePath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<SavedLoginProfile>(
                File.ReadAllText(_profilePath),
                Json);
        }
        catch
        {
            return null;
        }
    }

    public string? TryReadRefreshToken(SavedLoginProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.OrgCode) || string.IsNullOrWhiteSpace(profile.Email))
        {
            return null;
        }

        try
        {
            var vault = new PasswordVault();
            var userName = BuildCredentialUserName(profile.OrgCode, profile.Email);
            var cred = vault.Retrieve(VaultResource, userName);
            var token = cred?.Password?.Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    public void Save(string orgCode, string email, string refreshToken)
    {
        var code = orgCode.Trim();
        var mail = email.Trim().ToLowerInvariant();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            throw new ArgumentException("Invalid organisation code.");
        }

        if (string.IsNullOrWhiteSpace(mail) || string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Email and refresh token are required.");
        }

        ClearInternal(skipProfile: true);

        var vault = new PasswordVault();
        vault.Add(new PasswordCredential(
            VaultResource,
            BuildCredentialUserName(code, mail),
            refreshToken));

        Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
        File.WriteAllText(
            _profilePath,
            JsonSerializer.Serialize(
                new SavedLoginProfile { OrgCode = code, Email = mail },
                Json));
    }

    public bool ReplaceRefreshToken(string orgCode, string email, string newRefreshToken)
    {
        var profile = LoadProfile();
        if (profile is null)
        {
            return false;
        }

        if (!string.Equals(profile.OrgCode, orgCode.Trim(), StringComparison.Ordinal)
            || !string.Equals(profile.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            Save(orgCode, email, newRefreshToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Clear()
    {
        ClearInternal(skipProfile: false);
    }

    private void ClearInternal(bool skipProfile)
    {
        try
        {
            var vault = new PasswordVault();
            var toRemove = vault.RetrieveAll()
                .Where(c =>
                    string.Equals(c.Resource, VaultResource, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var item in toRemove)
            {
                try
                {
                    vault.Remove(item);
                }
                catch
                {
                    // ignore single removal failures
                }
            }
        }
        catch
        {
            // Credential locker unavailable
        }

        if (!skipProfile)
        {
            try
            {
                if (File.Exists(_profilePath))
                {
                    File.Delete(_profilePath);
                }
            }
            catch
            {
            }
        }
    }

    private static string BuildCredentialUserName(string orgCode, string email) =>
        $"{orgCode.Trim()}|{email.Trim().ToLowerInvariant()}";

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private SaveLoginPromptState LoadPromptState()
    {
        try
        {
            if (!File.Exists(_promptStatePath))
            {
                return new SaveLoginPromptState();
            }

            return JsonSerializer.Deserialize<SaveLoginPromptState>(
                       File.ReadAllText(_promptStatePath),
                       Json)
                   ?? new SaveLoginPromptState();
        }
        catch
        {
            return new SaveLoginPromptState();
        }
    }

    private void PersistPromptState(SaveLoginPromptState state)
    {
        var dir = Path.GetDirectoryName(_promptStatePath)!;
        Directory.CreateDirectory(dir);
        var tmp = _promptStatePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state, Json));
        File.Move(tmp, _promptStatePath, overwrite: true);
    }

    private sealed class SaveLoginPromptState
    {
        public List<string> AnsweredEmails { get; set; } = [];
    }
}

public sealed class SavedLoginProfile
{
    public string? OrgCode { get; set; }
    public string? Email { get; set; }
}