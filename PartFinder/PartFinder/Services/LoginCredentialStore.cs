using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Security.Credentials;

namespace PartFinder.Services;

/// <summary>
/// Saved organisation logins keyed by 6-digit org code + email (multiple orgs per PC).
/// Passwords use DPAPI with optional Windows Credential Locker.
/// </summary>
public sealed class LoginCredentialStore
{
    private const string VaultResource = "PartFinder.Login";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _indexPath;
    private readonly string _logPath;

    public LoginCredentialStore()
    {
        var dir = ResolveStorageDirectory();
        _indexPath = Path.Combine(dir, "login-credential-index.json");
        _logPath = Path.Combine(dir, "Logs", "login-save.log");
    }

    public static string ResolveStorageDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(localAppData))
        {
            localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local");
        }

        var dir = Path.Combine(localAppData, "PartFinder");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <returns>false if org/email/password was invalid or the index could not be written.</returns>
    public bool Save(string email, string password, string? orgCode)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOrg = NormalizeOrgCode(orgCode);
        if (normalizedEmail is null || string.IsNullOrEmpty(password) || normalizedOrg is null)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[LoginCredentialStore] Save skipped: emailOk={normalizedEmail is not null}, orgOk={normalizedOrg is not null}, pwdLen={password.Length}");
            return false;
        }

        var vaultUser = VaultUserName(normalizedOrg, normalizedEmail);
        TrySaveToVault(vaultUser, password);

        var index = LoadIndex();
        index.LastEmail = normalizedEmail;
        index.LastOrgCode = normalizedOrg;
        var row = GetOrCreateAccount(index, normalizedOrg, normalizedEmail);
        row.ProtectedPasswordBase64 = ProtectToBase64(password);
        row.LastUsedUtc = DateTimeOffset.UtcNow;
        row.OrgCode = normalizedOrg;
        row.Email = normalizedEmail;

        try
        {
            PersistIndex(index);
            AppendLoginLog($"Saved login for org {normalizedOrg}, email {normalizedEmail}.");
            return true;
        }
        catch (Exception ex)
        {
            AppendLoginLog($"Save failed for org {normalizedOrg}, email {normalizedEmail}: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[LoginCredentialStore] PersistIndex failed: {ex.Message}");
            return false;
        }
    }

    public bool TryGetPassword(string orgCode, string email, out string password)
    {
        password = string.Empty;
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOrg = NormalizeOrgCode(orgCode);
        if (normalizedEmail is null || normalizedOrg is null)
        {
            return false;
        }

        var index = LoadIndex();
        var row = FindAccount(index, normalizedOrg, normalizedEmail);
        if (row is not null && TryUnprotectFromBase64(row.ProtectedPasswordBase64, out password))
        {
            return password.Length > 0;
        }

        return TryReadFromVault(VaultUserName(normalizedOrg, normalizedEmail), out password);
    }

    public bool TryGetPassword(string email, out string password)
    {
        password = string.Empty;
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return false;
        }

        var index = LoadIndex();
        var matches = index.Accounts
            .Where(a => string.Equals(a.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1 &&
            TryUnprotectFromBase64(matches[0].ProtectedPasswordBase64, out password) &&
            password.Length > 0)
        {
            return true;
        }

        return TryReadFromVault(normalizedEmail, out password);
    }

    public string? GetOrgCodeForEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return null;
        }

        var index = LoadIndex();
        var matches = index.Accounts
            .Where(a => string.Equals(a.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matches.Count == 1)
        {
            return matches[0].OrgCode;
        }

        var lastOrg = NormalizeOrgCode(index.LastOrgCode);
        if (lastOrg is not null &&
            matches.Any(a => string.Equals(a.OrgCode, lastOrg, StringComparison.Ordinal)))
        {
            return lastOrg;
        }

        return null;
    }

    public IReadOnlyList<SavedLoginEntry> GetAll()
    {
        var index = LoadIndex();
        return index.Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a.Email) && NormalizeOrgCode(a.OrgCode) is not null)
            .OrderByDescending(a => a.LastUsedUtc ?? DateTimeOffset.MinValue)
            .ThenBy(a => a.OrgCode, StringComparer.Ordinal)
            .ThenBy(a => a.Email, StringComparer.OrdinalIgnoreCase)
            .Select(a => new SavedLoginEntry(
                a.OrgCode!,
                a.Email,
                a.LastUsedUtc))
            .ToList();
    }

    public IReadOnlyList<SavedLoginEntry> GetForOrg(string orgCode)
    {
        var normalizedOrg = NormalizeOrgCode(orgCode);
        if (normalizedOrg is null)
        {
            return [];
        }

        return GetAll().Where(e => string.Equals(e.OrgCode, normalizedOrg, StringComparison.Ordinal)).ToList();
    }

    public IReadOnlyList<SavedLoginEntry> GetForEmail(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null)
        {
            return [];
        }

        return GetAll()
            .Where(e => string.Equals(e.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public (string? OrgCode, string? Email, string? Password) GetLastUsed()
    {
        var index = LoadIndex();
        var org = NormalizeOrgCode(index.LastOrgCode);
        var email = NormalizeEmail(index.LastEmail);
        if (org is null || email is null)
        {
            if (email is not null)
            {
                var sole = GetForEmail(email);
                if (sole.Count == 1)
                {
                    org = sole[0].OrgCode;
                }
            }

            if (org is null || email is null)
            {
                return (null, null, null);
            }
        }

        TryGetPassword(org, email, out var password);
        return (org, email, string.IsNullOrEmpty(password) ? null : password);
    }

    public void TouchLastUsed(string orgCode, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOrg = NormalizeOrgCode(orgCode);
        if (normalizedEmail is null || normalizedOrg is null)
        {
            return;
        }

        var index = LoadIndex();
        var row = FindAccount(index, normalizedOrg, normalizedEmail);
        if (row is null)
        {
            return;
        }

        row.LastUsedUtc = DateTimeOffset.UtcNow;
        index.LastOrgCode = normalizedOrg;
        index.LastEmail = normalizedEmail;
        PersistIndex(index);
    }

    public void Remove(string orgCode, string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOrg = NormalizeOrgCode(orgCode);
        if (normalizedEmail is null || normalizedOrg is null)
        {
            return;
        }

        TryRemoveFromVault(VaultUserName(normalizedOrg, normalizedEmail));
        TryRemoveFromVault(normalizedEmail);

        var index = LoadIndex();
        index.Accounts.RemoveAll(a =>
            string.Equals(a.OrgCode, normalizedOrg, StringComparison.Ordinal) &&
            string.Equals(a.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (string.Equals(NormalizeOrgCode(index.LastOrgCode), normalizedOrg, StringComparison.Ordinal) &&
            string.Equals(NormalizeEmail(index.LastEmail), normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            index.LastOrgCode = null;
            index.LastEmail = null;
        }

        PersistIndex(index);
    }

    private static string VaultUserName(string orgCode, string normalizedEmail) => $"{orgCode}|{normalizedEmail}";

    private static void TrySaveToVault(string vaultUser, string password)
    {
        try
        {
            var vault = new PasswordVault();
            TryRemoveFromVault(vaultUser);
            vault.Add(new PasswordCredential(VaultResource, vaultUser, password));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginCredentialStore] Vault save skipped: {ex.Message}");
        }
    }

    private static bool TryReadFromVault(string vaultUser, out string password)
    {
        password = string.Empty;
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(VaultResource, vaultUser);
            cred.RetrievePassword();
            password = cred.Password ?? string.Empty;
            return password.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryRemoveFromVault(string vaultUser)
    {
        try
        {
            var vault = new PasswordVault();
            var existing = vault.Retrieve(VaultResource, vaultUser);
            vault.Remove(existing);
        }
        catch
        {
            // not found
        }
    }

    private static AccountIndexEntry GetOrCreateAccount(CredentialIndex index, string orgCode, string email)
    {
        var row = FindAccount(index, orgCode, email);
        if (row is not null)
        {
            return row;
        }

        row = new AccountIndexEntry { OrgCode = orgCode, Email = email };
        index.Accounts.Add(row);
        return row;
    }

    private static AccountIndexEntry? FindAccount(CredentialIndex index, string orgCode, string email) =>
        index.Accounts.FirstOrDefault(a =>
            string.Equals(a.OrgCode, orgCode, StringComparison.Ordinal) &&
            string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));

    private static string ProtectToBase64(string plain)
    {
        var bytes = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plain),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    private static bool TryUnprotectFromBase64(string? base64, out string plain)
    {
        plain = string.Empty;
        if (string.IsNullOrWhiteSpace(base64))
        {
            return false;
        }

        try
        {
            var bytes = ProtectedData.Unprotect(
                Convert.FromBase64String(base64),
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            plain = Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrEmpty(plain);
        }
        catch
        {
            return false;
        }
    }

    private CredentialIndex LoadIndex()
    {
        try
        {
            if (!File.Exists(_indexPath))
            {
                return new CredentialIndex();
            }

            var json = File.ReadAllText(_indexPath);
            var index = JsonSerializer.Deserialize<CredentialIndex>(json, Json) ?? new CredentialIndex();
            MigrateLegacyAccounts(index);
            return index;
        }
        catch
        {
            return new CredentialIndex();
        }
    }

    private static void MigrateLegacyAccounts(CredentialIndex index)
    {
        foreach (var account in index.Accounts)
        {
            account.Email = NormalizeEmail(account.Email) ?? account.Email.Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(account.OrgCode))
            {
                account.OrgCode = NormalizeOrgCode(account.OrgCode);
            }
        }

        index.Accounts.RemoveAll(a => string.IsNullOrWhiteSpace(a.Email));

        if (string.IsNullOrWhiteSpace(index.LastOrgCode) && !string.IsNullOrWhiteSpace(index.LastEmail))
        {
            var lastEmail = NormalizeEmail(index.LastEmail);
            var matches = index.Accounts
                .Where(a => string.Equals(a.Email, lastEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 1 && !string.IsNullOrWhiteSpace(matches[0].OrgCode))
            {
                index.LastOrgCode = matches[0].OrgCode;
            }
        }

        index.LastEmail = NormalizeEmail(index.LastEmail);
        index.LastOrgCode = NormalizeOrgCode(index.LastOrgCode);
    }

    private void PersistIndex(CredentialIndex index)
    {
        index.Accounts = index.Accounts
            .Where(a => NormalizeOrgCode(a.OrgCode) is not null && NormalizeEmail(a.Email) is not null)
            .GroupBy(a => $"{a.OrgCode}|{a.Email}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.LastUsedUtc ?? DateTimeOffset.MinValue).First())
            .ToList();

        var dir = Path.GetDirectoryName(_indexPath)!;
        Directory.CreateDirectory(dir);
        var tmp = _indexPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(index, Json));
        if (File.Exists(_indexPath))
        {
            File.Replace(tmp, _indexPath, null);
        }
        else
        {
            File.Move(tmp, _indexPath, overwrite: true);
        }
    }

    private void AppendLoginLog(string message)
    {
        try
        {
            var dir = Path.GetDirectoryName(_logPath)!;
            Directory.CreateDirectory(dir);
            File.AppendAllText(_logPath, $"[{DateTimeOffset.UtcNow:O}] {message}{Environment.NewLine}");
        }
        catch
        {
            // best effort
        }
    }

    private static string? NormalizeEmail(string? email)
    {
        var trimmed = email?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string? NormalizeOrgCode(string? orgCode)
    {
        var trimmed = orgCode?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length != 6 || !trimmed.All(char.IsDigit))
        {
            return null;
        }

        return trimmed;
    }

    private sealed class CredentialIndex
    {
        public string? LastEmail { get; set; }

        public string? LastOrgCode { get; set; }

        public List<AccountIndexEntry> Accounts { get; set; } = [];
    }

    private sealed class AccountIndexEntry
    {
        public string Email { get; set; } = string.Empty;

        public string? OrgCode { get; set; }

        public string? ProtectedPasswordBase64 { get; set; }

        public DateTimeOffset? LastUsedUtc { get; set; }
    }
}

public sealed record SavedLoginEntry(string OrgCode, string Email, DateTimeOffset? LastUsedUtc)
{
    public string DisplayLabel => $"{OrgCode} · {Email}";
}