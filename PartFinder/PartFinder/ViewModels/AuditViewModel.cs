using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class AuditViewModel : ViewModelBase
{
    private readonly MongoAuditService _auditService;

    public AuditViewModel(MongoAuditService auditService)
    {
        _auditService = auditService;
    }

    public ObservableCollection<AuditLogEntry> AuditLogs { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _totalEvents;
    [ObservableProperty] private int _stockChangeCount;
    [ObservableProperty] private int _userActionCount;
    [ObservableProperty] private int _systemEventCount;

    public async Task LoadAuditLogsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var docs = await _auditService.GetRecentAsync(200, cancellationToken).ConfigureAwait(true);

            AuditLogs.Clear();
            StockChangeCount = 0;
            UserActionCount = 0;
            SystemEventCount = 0;

            foreach (var doc in docs)
            {
                if (doc.EventType == "Stock Change") StockChangeCount++;
                else if (doc.EventType == "User Action") UserActionCount++;
                else SystemEventCount++;

                AuditLogs.Add(new AuditLogEntry
                {
                    EventId = doc.MongoId.ToString(),
                    EventType = string.IsNullOrWhiteSpace(doc.EventType) ? "System Event" : doc.EventType,
                    Action = string.IsNullOrWhiteSpace(doc.Action) ? "Event" : doc.Action,
                    Details = string.IsNullOrWhiteSpace(doc.Details) ? "—" : doc.Details,
                    User = string.IsNullOrWhiteSpace(doc.User) ? "System" : doc.User,
                    IpAddress = doc.IpAddress,
                    Timestamp = doc.Timestamp,
                    SessionId = doc.SessionId,
                    FormattedDate = doc.Timestamp.ToLocalTime().ToString("dd MMM yyyy"),
                    FormattedTime = doc.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                });
            }

            TotalEvents = AuditLogs.Count;
            StatusMessage = TotalEvents == 0
                ? "No audit events yet. Events will appear here as users interact with the system."
                : $"{TotalEvents} events loaded";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAuditLogsAsync();
}

public sealed class AuditLogEntry
{
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public required string Action { get; init; }
    public required string Details { get; init; }
    public required string User { get; init; }
    public required string IpAddress { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string SessionId { get; init; }
    public string FormattedDate { get; init; } = string.Empty;
    public string FormattedTime { get; init; } = string.Empty;

    public string EventTypeColor => EventType switch
    {
        "Stock Change"  => "#102ABD8F",
        "User Action"   => "#10FEBC2E",
        "System Event"  => "#108B5CF6",
        "Template"      => "#101F7AE0",
        "Alert"         => "#10FF5F57",
        _               => "#101F7AE0"
    };

    public string EventTypeForeground => EventType switch
    {
        "Stock Change"  => "#2ABD8F",
        "User Action"   => "#FEBC2E",
        "System Event"  => "#8B5CF6",
        "Template"      => "#1F7AE0",
        "Alert"         => "#FF5F57",
        _               => "#1F7AE0"
    };

    public string EventTypeIcon => EventType switch
    {
        "Stock Change"  => "\uE8B5",
        "User Action"   => "\uE8FA",
        "System Event"  => "\uE9F9",
        "Template"      => "\uE9F9",
        "Alert"         => "\uE7BA",
        _               => "\uE946"
    };

    public string FormattedTimestamp => Timestamp.ToLocalTime().ToString("MMM dd, yyyy — HH:mm:ss");

    public string UserInitial => string.IsNullOrWhiteSpace(User) ? "?" : User[0].ToString().ToUpperInvariant();

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - Timestamp;
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}
