using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class SuppliersPage : Page
{
    public SuppliersPage()
    {
        InitializeComponent();
    }

    private async void OnAddSupplierClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };

        void AddField(string label, string placeholder)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
            panel.Children.Add(new TextBox { PlaceholderText = placeholder, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
            content.Children.Add(panel);
        }

        AddField("COMPANY NAME", "e.g. Global Parts Inc");
        AddField("CONTACT EMAIL", "e.g. contact@supplier.com");
        AddField("PHONE NUMBER", "e.g. +1 234 567 8900");
        AddField("CATEGORY", "e.g. Engine Parts");
        AddField("LEAD TIME (DAYS)", "e.g. 8-12");

        var ratingPanel = new StackPanel { Spacing = 6 };
        ratingPanel.Children.Add(new TextBlock { Text = "INITIAL RATING", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var ratingCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        ratingCombo.Items.Add("5.0 ★★★★★");
        ratingCombo.Items.Add("4.0 ★★★★☆");
        ratingCombo.Items.Add("3.0 ★★★☆☆");
        ratingCombo.SelectedIndex = 1;
        ratingPanel.Children.Add(ratingCombo);
        content.Children.Add(ratingPanel);

        var dialog = new ContentDialog
        {
            Title = "Add Supplier",
            Content = content,
            PrimaryButtonText = "Add Supplier",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Supplier Added", Content = new TextBlock { Text = "New supplier has been added successfully.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnExportListClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Supplier List",
            Content = new TextBlock { Text = "Export all 24 suppliers to Excel? The file will include contact info, ratings, and performance data.", TextWrapping = TextWrapping.Wrap, FontSize = 13 },
            PrimaryButtonText = "Export Excel",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Export Started", Content = new TextBlock { Text = "Supplier list is being exported. File will be ready shortly.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnViewSupplierClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 440 };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var iconBorder = new Border { Width = 56, Height = 56, CornerRadius = new CornerRadius(12), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 31, 122, 224)) };
        iconBorder.Child = new FontIcon { Glyph = "\uE8FA", FontSize = 24, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["AccentPrimaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var headerText = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        headerText.Children.Add(new TextBlock { Text = "Global Parts Inc", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        headerText.Children.Add(new TextBlock { Text = "contact@globalparts.com", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        headerPanel.Children.Add(iconBorder);
        headerPanel.Children.Add(headerText);
        content.Children.Add(headerPanel);

        content.Children.Add(new Border { Height = 1, Background = (Microsoft.UI.Xaml.Media.Brush)Resources["BorderDefaultBrush"] });

        void AddRow(string label, string value)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"], VerticalAlignment = VerticalAlignment.Center };
            var val = new TextBlock { Text = value, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 1);
            row.Children.Add(lbl);
            row.Children.Add(val);
            content.Children.Add(row);
        }

        AddRow("Category", "Engine Parts");
        AddRow("Rating", "4.8 / 5.0 ★★★★★");
        AddRow("Lead Time", "8-12 days");
        AddRow("On-Time Delivery", "97.2%");
        AddRow("YTD Spend", "$2.4M");
        AddRow("Total Orders", "142");
        AddRow("Status", "Active & Verified");

        var dialog = new ContentDialog
        {
            Title = "Supplier Details",
            Content = content,
            PrimaryButtonText = "Edit Supplier",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
