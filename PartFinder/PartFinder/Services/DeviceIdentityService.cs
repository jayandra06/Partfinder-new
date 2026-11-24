using System.Security.Cryptography;
using System.Text;

namespace PartFinder.Services;

/// <summary>Stable per-install device id for refresh sessions (non-secret).</summary>
public static class DeviceIdentityService
{
    private static string? _cached;

    public static string GetDeviceId()
    {
        if (!string.IsNullOrWhiteSpace(_cached))
        {
            return _cached;
        }

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "device-id.txt");

        try
        {
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (existing.Length >= 16)
                {
                    _cached = existing;
                    return _cached;
                }
            }

            var bytes = RandomNumberGenerator.GetBytes(24);
            var id = Convert.ToBase64String(bytes);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, id);
            _cached = id;
            return _cached;
        }
        catch
        {
            _cached = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.MachineName)));
            return _cached;
        }
    }
}