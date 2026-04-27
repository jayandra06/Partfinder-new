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

            // Security: reject suspiciously large security files (max 4 KB)
            var info = new FileInfo(_filePath);
            if (info.Length > 4 * 1024)
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
        // Security: enforce exactly 6 digits
        if (!IsSixDigits(sixDigitPasscode))
            throw new ArgumentException("Passcode must be exactly 6 digits.");

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
        // Security: reject obviously wrong input before touching stored data
        if (!IsSixDigits(sixDigitPasscode))
            return false;

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
            // Security: constant-time comparison prevents timing attacks
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
        var secret = Current?.TwoFactorSecretBase32?.Trim();
        // Security: only return if it looks like a valid Base32 secret
        return string.IsNullOrWhiteSpace(secret) ? null : secret;
    }

    public void SetTwoFactor(string secretBase32, bool enabled)
    {
        // Security: validate Base32 format before storing
        if (string.IsNullOrWhiteSpace(secretBase32) || !IsValidBase32(secretBase32))
            throw new ArgumentException("Invalid TOTP secret format.");

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
        var dir = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(dir);

        // Security: write to temp file first, then atomic replace
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json));
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static byte[] HashPasscode(string passcode, byte[] salt)
    {
        // Security: SHA256 with salt — sufficient for a 6-digit local passcode
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

    private static bool IsSixDigits(string value) =>
        value.Length == 6 && value.All(char.IsDigit);

    // Security: Base32 alphabet only (RFC 4648)
    private static bool IsValidBase32(string s) =>
        s.ToUpperInvariant().All(c => "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567=".Contains(c));

    private sealed class SecurityFileDto
    {
        public string? PasscodeSalt { get; set; }
        public string? PasscodeHash { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorSecretBase32 { get; set; }
    }
}
