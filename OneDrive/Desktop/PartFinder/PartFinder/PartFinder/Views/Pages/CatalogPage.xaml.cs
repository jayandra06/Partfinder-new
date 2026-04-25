using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class CatalogPage : Page
{
    public CatalogPage()
    {
        InitializeComponent();
    }

    private async void OnViewPartClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 440 };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var iconBorder = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(12), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 31, 122, 224)) };
        iconBorder.Child = new FontIcon { Glyph = "\uE946", FontSize = 28, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["AccentPrimaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var headerText = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        headerText.Children.Add(new TextBlock { Text = "Turbocharger Assembly", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        headerText.Children.Add(new TextBlock { Text = "TRB-2024-001", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        headerPanel.Children.Add(iconBorder);
        headerPanel.Children.Add(headerText);
        content.Children.Add(headerPanel);

        var sep = new Border { Height = 1, Background = (Microsoft.UI.Xaml.Media.Brush)Resources["BorderDefaultBrush"] };
        content.Children.Add(sep);

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
        AddRow("Supplier", "Global Parts Inc");
        AddRow("Unit Price", "$1,250.00");
        AddRow("In Stock", "156 units");
        AddRow("Reorder Level", "50 units");
        AddRow("Lead Time", "8-12 days");
        AddRow("Last Updated", "2 hours ago");

        var dialog = new ContentDialog
        {
            Title = "Part Details",
            Content = content,
            PrimaryButtonText = "Edit Part",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async void OnAddPartClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };

        void AddField(string label, string placeholder)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
            panel.Children.Add(new TextBox { PlaceholderText = placeholder, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
            content.Children.Add(panel);
        }

        AddField("PART NAME", "Enter part name");
        AddField("PART ID", "e.g. TRB-2024-001");
        AddField("CATEGORY", "e.g. Engine Parts");
        AddField("UNIT PRICE", "e.g. 1250.00");
        AddField("INITIAL STOCK", "Enter quantity");

        var dialog = new ContentDialog
        {
            Title = "Add New Part",
            Content = content,
            PrimaryButtonText = "Add to Catalog",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Part Added", Content = new TextBlock { Text = "New part has been added to the catalog.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnCompareClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };
        content.Children.Add(new TextBlock { Text = "Select up to 3 parts to compare side by side.", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], TextWrapping = TextWrapping.Wrap });

        for (int i = 1; i <= 3; i++)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = $"PART {i}", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
            var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10), PlaceholderText = "Select part..." };
            combo.Items.Add("Turbocharger Assembly");
            combo.Items.Add("Fuel Injector");
            combo.Items.Add("Valve Assembly");
            panel.Children.Add(combo);
            content.Children.Add(panel);
        }

        var dialog = new ContentDialog
        {
            Title = "Compare Parts",
            Content = content,
            PrimaryButtonText = "Compare",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
