using System.Text.Json;
using Windows.Storage;
using PartFinder.Models;
using System.Diagnostics;

namespace PartFinder.Services;

public sealed class SessionPersistenceService
{
    public async Task SaveLoginSessionAsync(LoginSession session)
    {
        try
        {
            string json = JsonSerializer.Serialize(session);
            ApplicationData.Current.LocalSettings.Values["LastLoginSession"] = json;
            Debug.WriteLine("Session saved successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save session: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    public LoginSession? LoadLoginSession()
    {
        try
        {
            object data = ApplicationData.Current.LocalSettings.Values["LastLoginSession"];
            if (data != null)
            {
                Debug.WriteLine("Session loaded successfully");
                return JsonSerializer.Deserialize<LoginSession>(data.ToString());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to load session: {ex.Message}");
        }
        return null;
    }
}
