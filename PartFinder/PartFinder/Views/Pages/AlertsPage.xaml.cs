using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class AlertsPage : Page
{
    public AlertsViewModel ViewModel { get; }

    private readonly MongoAlertsService _alertsService;
    private readonly MongoAuditService _auditService;
    private readonly AdminSessionStore _session;

    public AlertsPage()
    {
        ViewModel = App.Services.GetRequiredService<AlertsViewModel>();
        _alertsService = App.Services.GetRequiredService<MongoAlertsService>();
        _auditService = App.Services.GetRequiredService<MongoAuditService>();
        _session = App.Services.GetRequiredService<AdminSessionStore>();
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAlertsAsync();
    }

    private async void OnMarkAllReadClick(object sender, RoutedEventArgs e)
    {
        ViewModel.MarkAllAsReadCommand.Execute(null);
        await ShowInfoAsync("Done", "All alerts marked as read.");
    }

    private async void OnConfigureAlertsClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 400 };
        content.Children.Add(new TextBlock { Text = "Alert Thresholds", FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var lowStockPanel = new StackPanel { Spacing = 6 };
        lowStockPanel.Children.Add(new TextBlock { Text = "LOW STOCK THRESHOLD (units)", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        lowStockPanel.Children.Add(new TextBox { Text = "50", FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
        content.Children.Add(lowStockPanel);

        var criticalPanel = new StackPanel { Spacing = 6 };
        criticalPanel.Children.Add(new TextBlock { Text = "CRITICAL STOCK THRESHOLD (units)", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        criticalPanel.Children.Add(new TextBox { Text = "10", FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
        content.Children.Add(criticalPanel);

        content.Children.Add(new ToggleSwitch { Header = "Email notifications", IsOn = true });
        content.Children.Add(new ToggleSwitch { Header = "Critical alerts only", IsOn = false });

        var dialog = new ContentDialog
        {
            Title = "Configure Alerts",
            Content = content,
            PrimaryButtonText = "Save Settings",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ShowInfoAsync("Saved", "Alert settings saved.");
    }

    private async void OnResolveAlertClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AlertItem alert)
        {
            var content = new StackPanel { Spacing = 12, MinWidth = 380 };
            content.Children.Add(new TextBlock { Text = "Add a resolution note (optional):", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
            var notesBox = new TextBox { PlaceholderText = "Describe how this was resolved...", FontSize = 13, Padding = new Thickness(12, 10, 12, 10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };
            content.Children.Add(notesBox);

            var dialog = new ContentDialog
            {
                Title = "Resolve Alert",
                Content = content,
                PrimaryButtonText = "Mark Resolved",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _alertsService.ResolveAsync(alert.Id).ConfigureAwait(true);

                _session.Load();
                await _auditService.LogAsync(new AuditDoc
                {
                    EventType = "User Action",
                    Action    = "Alert Resolved",
                    Details   = $"Alert resolved: {alert.Title}. Note: {notesBox.Text.Trim()}",
                    User      = _session.Email ?? "Admin",
                    Timestamp = DateTime.UtcNow,
                }).ConfigureAwait(true);

                await ViewModel.LoadAlertsAsync().ConfigureAwait(true);
                await ShowInfoAsync("Resolved", "Alert marked as resolved.");
            }
        }
    }

    private async void OnDismissAlertClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AlertItem alert)
        {
            await _alertsService.DismissAsync(alert.Id).ConfigureAwait(true);

            _session.Load();
            await _auditService.LogAsync(new AuditDoc
            {
                EventType = "User Action",
                Action    = "Alert Dismissed",
                Details   = $"Alert dismissed: {alert.Title}",
                User      = _session.Email ?? "Admin",
                Timestamp = DateTime.UtcNow,
            }).ConfigureAwait(true);

            await ViewModel.LoadAlertsAsync().ConfigureAwait(true);
        }
    }

    private async void OnReorderClick(object sender, RoutedEventArgs e)
    {
        await ShowInfoAsync("Reorder", "Reorder request has been placed.");
    }

    private async Task ShowInfoAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, FontSize = 13 },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
