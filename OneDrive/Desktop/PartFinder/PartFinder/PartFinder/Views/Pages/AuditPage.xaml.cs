using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class AuditPage : Page
{
    public AuditPage()
    {
        InitializeComponent();
    }

    private async void OnFilterClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 400 };

        var typePanel = new StackPanel { Spacing = 6 };
        typePanel.Children.Add(new TextBlock { Text = "EVENT TYPE", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var typeCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        typeCombo.Items.Add("All Events");
        typeCombo.Items.Add("Stock Changes");
        typeCombo.Items.Add("User Actions");
        typeCombo.Items.Add("System Events");
        typeCombo.Items.Add("Alerts");
        typeCombo.SelectedIndex = 0;
        typePanel.Children.Add(typeCombo);
        content.Children.Add(typePanel);

        var userPanel = new StackPanel { Spacing = 6 };
        userPanel.Children.Add(new TextBlock { Text = "USER", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var userCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        userCombo.Items.Add("All Users");
        userCombo.Items.Add("Sahil Ibrahim");
        userCombo.Items.Add("Admin");
        userCombo.Items.Add("System");
        userCombo.SelectedIndex = 0;
        userPanel.Children.Add(userCombo);
        content.Children.Add(userPanel);

        var datePanel = new StackPanel { Spacing = 6 };
        datePanel.Children.Add(new TextBlock { Text = "DATE RANGE", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var dateCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        dateCombo.Items.Add("Today");
        dateCombo.Items.Add("Last 7 days");
        dateCombo.Items.Add("Last 30 days");
        dateCombo.Items.Add("Last 3 months");
        dateCombo.Items.Add("Custom range");
        dateCombo.SelectedIndex = 0;
        datePanel.Children.Add(dateCombo);
        content.Children.Add(datePanel);

        var dialog = new ContentDialog
        {
            Title = "Filter Audit Log",
            Content = content,
            PrimaryButtonText = "Apply Filters",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async void OnExportAuditClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 380 };
        content.Children.Add(new TextBlock { Text = "Export the complete audit log for compliance and reporting.", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], TextWrapping = TextWrapping.Wrap });

        var formatPanel = new StackPanel { Spacing = 6 };
        formatPanel.Children.Add(new TextBlock { Text = "FORMAT", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var formatCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        formatCombo.Items.Add("Excel (.xlsx)");
        formatCombo.Items.Add("CSV (.csv)");
        formatCombo.Items.Add("PDF Report");
        formatCombo.SelectedIndex = 0;
        formatPanel.Children.Add(formatCombo);
        content.Children.Add(formatPanel);

        var rangePanel = new StackPanel { Spacing = 6 };
        rangePanel.Children.Add(new TextBlock { Text = "DATE RANGE", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var rangeCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        rangeCombo.Items.Add("Today");
        rangeCombo.Items.Add("Last 7 days");
        rangeCombo.Items.Add("Last 30 days");
        rangeCombo.Items.Add("All time");
        rangeCombo.SelectedIndex = 2;
        rangePanel.Children.Add(rangeCombo);
        content.Children.Add(rangePanel);

        var dialog = new ContentDialog
        {
            Title = "Export Audit Log",
            Content = content,
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Export Started", Content = new TextBlock { Text = "Audit log is being exported. File will be ready shortly.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnViewDetailClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 12, MinWidth = 420 };

        void AddRow(string label, string value)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] };
            var val = new TextBlock { Text = value, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(val, 1);
            row.Children.Add(lbl);
            row.Children.Add(val);
            content.Children.Add(row);
        }

        AddRow("Event ID", "EVT-2026-04-23-0142");
        AddRow("Type", "Stock Change");
        AddRow("Action", "Stock Added");
        AddRow("Details", "Turbocharger Assembly — +50 units (156 → 206)");
        AddRow("User", "Sahil Ibrahim");
        AddRow("IP Address", "192.168.1.45");
        AddRow("Timestamp", "Apr 23, 2026 — 14:32:07 UTC");
        AddRow("Session ID", "SES-8F2A4C");

        var dialog = new ContentDialog
        {
            Title = "Event Details",
            Content = content,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
