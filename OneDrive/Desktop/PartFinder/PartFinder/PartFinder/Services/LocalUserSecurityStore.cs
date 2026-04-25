using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PartFinder.Services;

/// <summary>
/// Local passcode hash and 2FA secret. Used for app lock and as verification for password / passcode reset flows.
/// </summary>
public sealed class LocalUserSecurityStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;

    public LocalUserSecurityStore()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder");
        _filePath = Path.Combine(dir, "user-security.json");
    }

    public bool PasscodeIsSet
    {
        get
        {
            Load();
            return Current?.PasscodeHash is { Length: > 0 } && Current?.PasscodeSalt is { Length: > 0 };
        }
    }

    public bool TwoFactorEnabled
    {
        get
        {
            Load();
            return Current?.TwoFactorEnabled == true &&
                   !string.IsNullOrWhiteSpace(Current?.TwoFactorSecretBase32);
        }
    }

    private SecurityFileDto? Current { get; set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Current = new SecurityFileDto();
                return;
            }

            var json = File.ReadAllText(_filePath);
            Current = JsonSerializer.Deserialize<SecurityFileDto>(json, Json) ?? new SecurityFileDto();
        }
        catch
        {
            Current = new SecurityFileDto();
        }
    }

    public void SavePasscode(string sixDigitPasscode)
    {
        Load();
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPasscode(sixDigitPasscode, salt);
        Current ??= new SecurityFileDto();
        Current.PasscodeSalt = Convert.ToBase64String(salt);
        Current.PasscodeHash = Convert.ToBase64String(hash);
        Persist();
    }

    public bool VerifyPasscode(string sixDigitPasscode)
    {
        Load();
        if (Current?.PasscodeSalt is null || Current.PasscodeHash is null)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(Current.PasscodeSalt);
            var expected = Convert.FromBase64String(Current.PasscodeHash);
            var actual = HashPasscode(sixDigitPasscode, salt);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch
        {
            return false;
        }
    }

    public void ClearPasscode()
    {
        Load();
        if (Current is null)
        {
            return;
        }

        Current.PasscodeHash = null;
        Current.PasscodeSalt = null;
        Persist();
    }

    public string? GetTwoFactorSecret()
    {
        Load();
        return string.IsNullOrWhiteSpace(Current?.TwoFactorSecretBase32) ? null : Current.TwoFactorSecretBase32.Trim();
    }

    public void SetTwoFactor(string secretBase32, bool enabled)
    {
        Load();
        Current ??= new SecurityFileDto();
        Current.TwoFactorSecretBase32 = secretBase32;
        Current.TwoFactorEnabled = enabled;
        Persist();
    }

    public void DisableTwoFactor()
    {
        Load();
        if (Current is null)
        {
            return;
        }

        Current.TwoFactorEnabled = false;
        Current.TwoFactorSecretBase32 = null;
        Persist();
    }

    private void Persist()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(Current, Json));
    }

    private static byte[] HashPasscode(string passcode, byte[] salt)
    {
        var data = Encoding.UTF8.GetBytes(passcode);
        return SHA256.HashData(Concat(salt, data));
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private sealed class SecurityFileDto
    {
        public string? PasscodeSalt { get; set; }
        public string? PasscodeHash { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorSecretBase32 { get; set; }
    }
}
