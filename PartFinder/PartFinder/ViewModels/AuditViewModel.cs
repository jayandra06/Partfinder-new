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

    private readonly List<AuditLogEntry> _allLogs = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedEventType = "All Events";
    [ObservableProperty] private string _selectedUser = "All Users";
    [ObservableProperty] private string _selectedDateRange = "All time";

    public ObservableCollection<string> AvailableUsers { get; } = new() { "All Users" };

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedEventTypeChanged(string value) => ApplyFilters();
    partial void OnSelectedUserChanged(string value) => ApplyFilters();
    partial void OnSelectedDateRangeChanged(string value) => ApplyFilters();

    private void ApplyFilters()
    {
        var filtered = _allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var lowerSearch = SearchText.ToLowerInvariant();
            filtered = filtered.Where(x => 
                x.Action.ToLowerInvariant().Contains(lowerSearch) ||
                x.Details.ToLowerInvariant().Contains(lowerSearch) ||
                x.User.ToLowerInvariant().Contains(lowerSearch));
        }

        if (SelectedEventType != "All Events" && !string.IsNullOrWhiteSpace(SelectedEventType))
        {
            var matchType = SelectedEventType switch
            {
                "Stock Changes" => "Stock Change",
                "User Actions" => "User Action",
                "System Events" => "System Event",
                "Templates" => "Template",
                "Alerts" => "Alert",
                _ => SelectedEventType
            };
            filtered = filtered.Where(x => x.EventType == matchType);
        }

        if (SelectedUser != "All Users" && !string.IsNullOrWhiteSpace(SelectedUser))
        {
            filtered = filtered.Where(x => x.User == SelectedUser);
        }

        if (SelectedDateRange != "All time" && !string.IsNullOrWhiteSpace(SelectedDateRange))
        {
            var cutoff = DateTime.UtcNow;
            if (SelectedDateRange == "Today") cutoff = cutoff.Date;
            else if (SelectedDateRange == "Last 7 days") cutoff = cutoff.AddDays(-7);
            else if (SelectedDateRange == "Last 30 days") cutoff = cutoff.AddDays(-30);
            
            if (SelectedDateRange != "All time")
            {
                filtered = filtered.Where(x => x.Timestamp >= cutoff);
            }
        }

        AuditLogs.Clear();
        foreach (var item in filtered)
        {
            AuditLogs.Add(item);
        }
    }

    public async Task LoadAuditLogsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var docs = await _auditService.GetRecentAsync(200, cancellationToken).ConfigureAwait(true);

            _allLogs.Clear();
            StockChangeCount = 0;
            UserActionCount = 0;
            SystemEventCount = 0;
            var users = new HashSet<string>();

            foreach (var doc in docs)
            {
                if (doc.EventType == "Stock Change") StockChangeCount++;
                else if (doc.EventType == "User Action") UserActionCount++;
                else SystemEventCount++;

                var entry = new AuditLogEntry
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
                };
                _allLogs.Add(entry);
                users.Add(entry.User);
            }

            AvailableUsers.Clear();
            AvailableUsers.Add("All Users");
            foreach (var u in users.OrderBy(x => x))
            {
                AvailableUsers.Add(u);
            }

            TotalEvents = _allLogs.Count;
            StatusMessage = TotalEvents == 0
                ? "No audit events yet. Events will appear here as users interact with the system."
                : $"{TotalEvents} events loaded";
                
            ApplyFilters();
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
