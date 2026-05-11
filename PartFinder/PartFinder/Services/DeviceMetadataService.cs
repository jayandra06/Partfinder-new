using System.Net.Http;
using Windows.Devices.Geolocation;
using Windows.Security.ExchangeActiveSyncProvisioning;
using Windows.System.Profile;
using System.Threading.Tasks;
using System;

namespace PartFinder.Services;

public sealed class DeviceMetadataService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public record DeviceMetadata(
        string IpAddress,
        double? Latitude,
        double? Longitude,
        string DeviceName,
        string OsVersion,
        string LocationStatus,
        string City);

    public async Task<DeviceMetadata> GetMetadataAsync()
    {
        string ip = "Not Available";
        double? lat = null;
        double? lon = null;
        string locationStatus = "Not Captured";
        string city = "Not Available";

        // 1. Fetch Public IP
        try
        {
            ip = await Http.GetStringAsync("https://api.ipify.org").ConfigureAwait(false);
            System.Diagnostics.Debug.WriteLine($"[DeviceMetadata] Public IP fetched: {ip}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceMetadata] IP fetch failed: {ex.Message}");
        }

        // 2. Fetch Geolocation
        try
        {
            var accessStatus = await Geolocator.RequestAccessAsync();
            if (accessStatus == GeolocationAccessStatus.Allowed)
            {
                var geolocator = new Geolocator { DesiredAccuracyInMeters = 50 };
                var pos = await geolocator.GetGeopositionAsync().AsTask().ConfigureAwait(false);
                lat = pos.Coordinate.Point.Position.Latitude;
                lon = pos.Coordinate.Point.Position.Longitude;
                locationStatus = "Success";
                System.Diagnostics.Debug.WriteLine($"[DeviceMetadata] Coordinates fetched: {lat}, {lon}");
                
                // Try to get city name via reverse geocoding if possible
                // Note: MapLocationFinder requires a MapServiceToken usually, 
                // but we can try basic capture or rely on backend for city.
                city = "Determining..."; 
            }
            else
            {
                locationStatus = "Location Access Denied";
                System.Diagnostics.Debug.WriteLine("[DeviceMetadata] Location access denied.");
            }
        }
        catch (Exception ex)
        {
            locationStatus = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[DeviceMetadata] Geolocation fetch failed: {ex.Message}");
        }

        // 3. Device Info using Environment as requested
        string deviceName = Environment.MachineName;
        string osVersion = Environment.OSVersion.ToString();
        
        // Enhance OS version display
        var deviceInfo = new EasClientDeviceInformation();
        if (osVersion.Contains("6.2") || osVersion.Contains("10.0"))
        {
            try
            {
                var versionInfo = AnalyticsInfo.VersionInfo;
                long version = long.Parse(versionInfo.DeviceFamilyVersion);
                long build = (version & 0x00000000FFFF0000L) >> 16;
                osVersion = build >= 22000 ? $"Windows 11 (Build {build})" : $"Windows 10 (Build {build})";
            }
            catch { }
        }

        return new DeviceMetadata(ip, lat, lon, deviceName, osVersion, locationStatus, city);
    }
}

