using System.Collections.ObjectModel;
using System.Text.Json;

namespace PartFinder.Services;

/// <summary>
/// Persists the global floating chatbot position across restarts and holds
/// shared chat session state across page navigations.
/// </summary>
public sealed class ChatbotStateService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private bool _loaded;

    public double? ButtonXRatio { get; private set; }
    public double? ButtonYRatio { get; private set; }
    public bool IsOpen { get; set; }
    public string? ConversationId { get; set; }

    public ObservableCollection<ChatMessageItem> Messages { get; } = [];

    public void ResetConversation()
    {
        ConversationId = null;
        Messages.Clear();
    }

    public void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        try
        {
            var path = GetStateFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<ChatbotPositionDto>(json);
            if (dto is null)
            {
                return;
            }

            if (dto.XRatio is >= 0 and <= 1 && dto.YRatio is >= 0 and <= 1)
            {
                ButtonXRatio = dto.XRatio;
                ButtonYRatio = dto.YRatio;
            }
        }
        catch
        {
            // Ignore corrupt state file and fall back to default bottom-right corner.
        }
    }

    public void SavePosition(double xRatio, double yRatio)
    {
        EnsureLoaded();
        ButtonXRatio = Math.Clamp(xRatio, 0d, 1d);
        ButtonYRatio = Math.Clamp(yRatio, 0d, 1d);

        try
        {
            var path = GetStateFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(
                new ChatbotPositionDto { XRatio = ButtonXRatio.Value, YRatio = ButtonYRatio.Value },
                JsonOpts);
            File.WriteAllText(path, json);
        }
        catch
        {
            // Non-fatal if disk write fails.
        }
    }

    private static string GetStateFilePath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "chatbot-state.json");

    private sealed class ChatbotPositionDto
    {
        public double XRatio { get; set; }
        public double YRatio { get; set; }
    }
}
