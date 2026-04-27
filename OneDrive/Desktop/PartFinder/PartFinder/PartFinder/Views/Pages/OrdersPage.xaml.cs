using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class OrdersPage : Page
{
    public OrdersPage()
    {
        InitializeComponent();
    }

    private async void OnNewOrderClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 440 };

        void AddField(string label, string placeholder)
        {
            var panel = new StackPanel { Spacing = 6 };
            panel.Children.Add(new TextBlock { Text = label, FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
            panel.Children.Add(new TextBox { PlaceholderText = placeholder, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) });
            content.Children.Add(panel);
        }

        var supplierPanel = new StackPanel { Spacing = 6 };
        supplierPanel.Children.Add(new TextBlock { Text = "SUPPLIER", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        var supplierCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10), PlaceholderText = "Select supplier" };
        supplierCombo.Items.Add("Global Parts Inc");
        supplierCombo.Items.Add("Premium Motors");
        supplierCombo.Items.Add("Tech Components Ltd");
        supplierPanel.Children.Add(supplierCombo);
        content.Children.Add(supplierPanel);

        AddField("PART NAME", "e.g. Turbocharger Assembly");
        AddField("QUANTITY", "e.g. 50");
        AddField("UNIT PRICE ($)", "e.g. 1250.00");
        AddField("EXPECTED DELIVERY", "e.g. 2026-05-10");

        var notesPanel = new StackPanel { Spacing = 6 };
        notesPanel.Children.Add(new TextBlock { Text = "NOTES", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"] });
        notesPanel.Children.Add(new TextBox { PlaceholderText = "Order notes...", FontSize = 13, Padding = new Thickness(12, 10, 12, 10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 });
        content.Children.Add(notesPanel);

        var dialog = new ContentDialog
        {
            Title = "Create Purchase Order",
            Content = content,
            PrimaryButtonText = "Place Order",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Order Placed", Content = new TextBlock { Text = "Purchase order has been created and sent to the supplier.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnViewOrderClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 440 };

        var headerPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
        var iconBorder = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(10), Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 31, 122, 224)) };
        iconBorder.Child = new FontIcon { Glyph = "\uE8B5", FontSize = 22, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["AccentPrimaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var headerText = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        headerText.Children.Add(new TextBlock { Text = "PO-2024-0142", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.Bold });
        headerText.Children.Add(new TextBlock { Text = "Global Parts Inc", FontSize = 12, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        headerPanel.Children.Add(iconBorder);
        headerPanel.Children.Add(headerText);
        content.Children.Add(headerPanel);
        content.Children.Add(new Border { Height = 1, Background = (Microsoft.UI.Xaml.Media.Brush)Resources["BorderDefaultBrush"] });

        void AddRow(string label, string value)
        {
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] };
            var val = new TextBlock { Text = value, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            Grid.SetColumn(val, 1);
            row.Children.Add(lbl);
            row.Children.Add(val);
            content.Children.Add(row);
        }

        AddRow("Part", "Turbocharger Assembly");
        AddRow("Quantity", "50 units");
        AddRow("Unit Price", "$1,250.00");
        AddRow("Total Amount", "$62,500.00");
        AddRow("Order Date", "Apr 20, 2026");
        AddRow("Expected Delivery", "May 2, 2026");
        AddRow("Status", "In Transit");

        var dialog = new ContentDialog
        {
            Title = "Order Details",
            Content = content,
            PrimaryButtonText = "Mark Delivered",
            SecondaryButtonText = "Cancel Order",
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Order Updated", Content = new TextBlock { Text = "Order marked as delivered. Inventory has been updated.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }

    private async void OnExportOrdersClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Orders",
            Content = new TextBlock { Text = "Export all purchase orders to Excel? Includes order details, status, and payment information.", TextWrapping = TextWrapping.Wrap, FontSize = 13 },
            PrimaryButtonText = "Export Excel",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var confirm = new ContentDialog { Title = "Export Started", Content = new TextBlock { Text = "Orders are being exported. File will be ready shortly.", FontSize = 13 }, PrimaryButtonText = "OK", DefaultButton = ContentDialogButton.Primary, XamlRoot = XamlRoot };
            await confirm.ShowAsync();
        }
    }
}
