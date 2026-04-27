using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class InventoryPage : Page
{
    public InventoryPage()
    {
        InitializeComponent();
    }

    private async void OnAddStockClick(object sender, RoutedEventArgs e)
    {
        await ShowAddStockDialogAsync();
    }

    private async void OnEditInventoryClick(object sender, RoutedEventArgs e)
    {
        await ShowEditInventoryDialogAsync();
    }

    private async Task ShowAddStockDialogAsync()
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };

        // Part selector
        var partLabel = new TextBlock { Text = "SELECT PART", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var partCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        partCombo.Items.Add("Turbocharger Assembly");
        partCombo.Items.Add("Fuel Injector");
        partCombo.Items.Add("Valve Assembly");

        // Quantity field
        var qtyLabel = new TextBlock { Text = "QUANTITY TO ADD", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var qtyBox = new TextBox { PlaceholderText = "Enter quantity", FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };

        // Notes field
        var notesLabel = new TextBlock { Text = "NOTES (OPTIONAL)", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var notesBox = new TextBox { PlaceholderText = "Add notes about this stock addition...", FontSize = 13, Padding = new Thickness(12, 10, 12, 10), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80 };

        content.Children.Add(partLabel);
        content.Children.Add(partCombo);
        content.Children.Add(qtyLabel);
        content.Children.Add(qtyBox);
        content.Children.Add(notesLabel);
        content.Children.Add(notesBox);

        var dialog = new ContentDialog
        {
            Title = "Add Stock",
            PrimaryButtonText = "Add Stock",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = content,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ShowSuccessToast("Stock Added", $"Added {qtyBox.Text} units to inventory");
        }
    }

    private async Task ShowEditInventoryDialogAsync()
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 420 };

        // Part name (read-only)
        var partLabel = new TextBlock { Text = "PART NAME", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var partBox = new TextBox { Text = "Turbocharger Assembly", IsReadOnly = true, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };

        // Current quantity
        var currentQtyLabel = new TextBlock { Text = "CURRENT QUANTITY", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var currentQtyBox = new TextBox { Text = "156 units", IsReadOnly = true, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };

        // New quantity
        var newQtyLabel = new TextBlock { Text = "UPDATE QUANTITY", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var newQtyBox = new TextBox { PlaceholderText = "Enter new quantity", FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };

        // Status
        var statusLabel = new TextBlock { Text = "STATUS", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"], Margin = new Thickness(0, 0, 0, 6) };
        var statusCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        statusCombo.Items.Add("In Stock");
        statusCombo.Items.Add("Low Stock");
        statusCombo.Items.Add("Out of Stock");
        statusCombo.SelectedIndex = 0;

        content.Children.Add(partLabel);
        content.Children.Add(partBox);
        content.Children.Add(currentQtyLabel);
        content.Children.Add(currentQtyBox);
        content.Children.Add(newQtyLabel);
        content.Children.Add(newQtyBox);
        content.Children.Add(statusLabel);
        content.Children.Add(statusCombo);

        var dialog = new ContentDialog
        {
            Title = "Edit Inventory",
            PrimaryButtonText = "Update",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = content,
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ShowSuccessToast("Updated", "Inventory updated successfully");
        }
    }

    private void ShowSuccessToast(string title, string message)
    {
        // Simple toast notification
        var toast = new TeachingTip
        {
            Title = title,
            Subtitle = message,
            IsOpen = true,
            Target = this,
        };
    }
}

