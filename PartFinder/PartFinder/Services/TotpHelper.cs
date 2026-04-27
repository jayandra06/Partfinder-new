using System.Security.Cryptography;

namespace PartFinder.Services;

/// <summary>
/// RFC 6238-style TOTP (SHA-1, 30s step, 6 digits) for authenticator apps.
/// </summary>
public static class TotpHelper
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateRandomSecretBase32(int numBytes = 20)
    {
        var bytes = RandomNumberGenerator.GetBytes(numBytes);
        return ToBase32(bytes);
    }

    public static bool Verify(string sixDigitCode, string secretBase32, int allowedStepDrift = 1)
    {
        if (sixDigitCode.Length != 6 || !sixDigitCode.All(char.IsDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = FromBase32(secretBase32);
        }
        catch
        {
            return false;
        }

        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var counter = unix / 30;
        for (var d = -allowedStepDrift; d <= allowedStepDrift; d++)
        {
            if (GenerateCode(key, counter + d) == sixDigitCode)
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateCode(byte[] key, long counter)
    {
        var counterBytes = new byte[8];
        var c = (ulong)counter;
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(c & 0xff);
            c >>= 8;
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[19] & 0x0f;
        var binary =
            ((hash[offset] & 0x7f) << 24) |
            (hash[offset + 1] << 16) |
            (hash[offset + 2] << 8) |
            hash[offset + 3];
        var otp = binary % 1_000_000;
        return otp.ToString("D6");
    }

    private static string ToBase32(byte[] data)
    {
        var outputLength = (data.Length * 8 + 4) / 5;
        var result = new char[outputLength];
        var buffer = 0;
        var bitsLeft = 0;
        var count = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                result[count++] = Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31];
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            result[count++] = Base32Alphabet[(buffer << (5 - bitsLeft)) & 31];
        }

        return new string(result);
    }

    private static byte[] FromBase32(string encoded)
    {
        encoded = encoded.Trim().ToUpperInvariant().Replace(" ", "");
        if (encoded.Length == 0)
        {
            throw new FormatException("Empty secret.");
        }

        var outputLength = encoded.Length * 5 / 8;
        var data = new byte[outputLength];
        var buffer = 0;
        var bitsLeft = 0;
        var count = 0;
        foreach (var c in encoded)
        {
            var val = Base32Alphabet.IndexOf(c);
            if (val < 0)
            {
                throw new FormatException("Invalid base32 character.");
            }

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                data[count++] = (byte)((buffer >> (bitsLeft - 8)) & 0xff);
                bitsLeft -= 8;
            }
        }

        return data;
    }
}
