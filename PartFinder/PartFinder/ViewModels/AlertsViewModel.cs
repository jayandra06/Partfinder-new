using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class AlertsViewModel : ViewModelBase
{
    private readonly MongoAlertsService _alertsService;
    private readonly ActivityLogger _activityLogger;

    public AlertsViewModel(MongoAlertsService alertsService, ActivityLogger activityLogger)
    {
        _alertsService = alertsService;
        _activityLogger = activityLogger;
    }

    public ObservableCollection<AlertItem> Alerts { get; } = [];

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private int _totalAlerts;
    [ObservableProperty] private int _criticalAlerts;
    [ObservableProperty] private int _warningAlerts;

    public async Task LoadAlertsAsync(CancellationToken cancellationToken = default)
    {
        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var docs = await _alertsService.GetActiveAsync(cancellationToken).ConfigureAwait(true);

            Alerts.Clear();
            CriticalAlerts = 0;
            WarningAlerts = 0;

            foreach (var doc in docs)
            {
                if (doc.Severity == "Critical") CriticalAlerts++;
                else if (doc.Severity == "Warning") WarningAlerts++;

                Alerts.Add(new AlertItem
                {
                    Id = doc.MongoId.ToString(),
                    Title = string.IsNullOrWhiteSpace(doc.Title) ? "Alert" : doc.Title,
                    Message = string.IsNullOrWhiteSpace(doc.Message) ? "—" : doc.Message,
                    Severity = string.IsNullOrWhiteSpace(doc.Severity) ? "Info" : doc.Severity,
                    Category = string.IsNullOrWhiteSpace(doc.Category) ? "System" : doc.Category,
                    PartName = doc.PartName,
                    Timestamp = doc.CreatedAt,
                    IsRead = doc.IsRead,
                });
            }

            TotalAlerts = Alerts.Count;
            StatusMessage = TotalAlerts == 0
                ? "No active alerts. System is running normally."
                : $"{TotalAlerts} active alerts";
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
    private async Task DismissAlertAsync(AlertItem? alert)
    {
        if (alert is null) return;
        await _alertsService.DismissAsync(alert.Id).ConfigureAwait(true);
        _activityLogger.LogUserAction("Alert Dismissed", $"Alert \"{alert.Title}\" dismissed");
        Alerts.Remove(alert);
        TotalAlerts = Alerts.Count;
    }

    [RelayCommand]
    private async Task ResolveAlertAsync(AlertItem? alert)
    {
        if (alert is null) return;
        await _alertsService.ResolveAsync(alert.Id).ConfigureAwait(true);
        _activityLogger.LogUserAction("Alert Resolved", $"Alert \"{alert.Title}\" resolved");
        Alerts.Remove(alert);
        TotalAlerts = Alerts.Count;
    }

    [RelayCommand]
    private void MarkAllAsRead()
    {
        foreach (var alert in Alerts)
            alert.IsRead = true;
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAlertsAsync();
}

public sealed partial class AlertItem : ObservableObject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Severity { get; init; }
    public required string Category { get; init; }
    public required string PartName { get; init; }
    public required DateTime Timestamp { get; init; }

    [ObservableProperty] private bool _isRead;

    public string SeverityColor => Severity switch
    {
        "Critical" => "#FF5F57",
        "Warning"  => "#FFB781",
        _          => "#1F7AE0"
    };

    public string SeverityIcon => Severity switch
    {
        "Critical" => "\uEA39",
        "Warning"  => "\uE7BA",
        _          => "\uE946"
    };

    public string TimeAgo
    {
        get
        {
            var span = DateTime.UtcNow - Timestamp.ToUniversalTime();
            if (span.TotalMinutes < 1) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            return $"{(int)span.TotalDays}d ago";
        }
    }
}
