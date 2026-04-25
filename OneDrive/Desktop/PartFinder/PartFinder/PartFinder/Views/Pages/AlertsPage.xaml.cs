using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class AlertsPage : Page
{
    public AlertsPage()
    {
        InitializeComponent();
    }

    private async void OnMarkAllReadClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Mark All as Read",
            Content = new TextBlock { Text = "All 54 alerts will be marked as read. This action cannot be undone.", TextWrapping = TextWrapping.Wrap, FontSize = 13 },
            PrimaryButtonText = "Mark All Read",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ShowToastAsync("All alerts marked as read.");
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
        content.Children.Add(new ToggleSwitch { Header = "Daily digest", IsOn = true });

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
            await ShowToastAsync("Alert settings saved.");
    }

    private async void OnResolveAlertClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 380 };
        content.Children.Add(new TextBlock { Text = "Add a resolution note (optional):", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        content.Children.Add(new TextBox { PlaceholderText = "Describe how this was resolved...", FontSize = 13, Padding = new Thickness(12, 10, 12, 10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 });

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
            await ShowToastAsync("Alert resolved successfully.");
    }

    private async void OnDismissAlertClick(object sender, RoutedEventArgs e)
    {
        await ShowToastAsync("Alert dismissed.");
    }

    private async void OnReorderClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 400 };

        var qtyPanel = new StackPanel { Spacing = 6 };
        qtyPanel.Children.Add(new TextBlock { Text = "REORDER QUANTITY", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        qtyPanel.Children.Add(new TextBox { Text = "100", FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
        content.Children.Add(qtyPanel);

        var supplierPanel = new StackPanel { Spacing = 6 };
        supplierPanel.Children.Add(new TextBlock { Text = "SUPPLIER", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        combo.Items.Add("Global Parts Inc");
        combo.Items.Add("Premium Motors");
        combo.Items.Add("Tech Components Ltd");
        combo.SelectedIndex = 0;
        supplierPanel.Children.Add(combo);
        content.Children.Add(supplierPanel);

        var dialog = new ContentDialog
        {
            Title = "Create Reorder",
            Content = content,
            PrimaryButtonText = "Place Order",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
            await ShowToastAsync("Reorder placed successfully.");
    }

    private async Task ShowToastAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Done",
            Content = new TextBlock { Text = message, FontSize = 13 },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
