using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;
using Windows.Storage.Pickers;
using System.Text;
using System;
using System.Collections.Generic;

namespace PartFinder.Views.Pages;

public sealed partial class AuditPage : Page
{
    public AuditViewModel ViewModel { get; }

    public AuditPage()
    {
        ViewModel = App.Services.GetRequiredService<AuditViewModel>();
        InitializeComponent();
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.LoadAuditLogsAsync();
    }

    private async void OnFilterClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 14, MinWidth = 400 };

        var typePanel = new StackPanel { Spacing = 6 };
        typePanel.Children.Add(new TextBlock { Text = "EVENT TYPE", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextSecondaryBrush"] });
        var typeCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 13, Padding = new Thickness(12, 10, 12, 10) };
        typeCombo.Items.Add("All Events");
        typeCombo.Items.Add("Stock Changes");
        typeCombo.Items.Add("User Actions");
        typeCombo.Items.Add("System Events");
        typeCombo.Items.Add("Templates");
        typeCombo.Items.Add("Alerts");
        typeCombo.SelectedItem = ViewModel.SelectedEventType;
        typePanel.Children.Add(typeCombo);
        content.Children.Add(typePanel);

        var dialog = new ContentDialog
        {
            Title = "Filter Audit Log",
            Content = content,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            if (typeCombo.SelectedItem is string selectedType)
            {
                ViewModel.SelectedEventType = selectedType;
            }
        }
    }

    private async void OnExportAuditClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Audit Log",
            Content = new TextBlock { Text = "Export the complete audit log for compliance and reporting.", FontSize = 13, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Export",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var picker = new FileSavePicker();
            var window = App.MainAppWindow;
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("CSV format", new List<string>() { ".csv" });
            picker.SuggestedFileName = $"AuditLog_{DateTime.Now:yyyyMMdd_HHmmss}";

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    var csv = new StringBuilder();
                    csv.AppendLine("Event ID,Type,Action,Details,User,IP Address,Timestamp,Session ID");
                    foreach (var entry in ViewModel.AuditLogs)
                    {
                        csv.AppendLine($"{Escape(entry.EventId)},{Escape(entry.EventType)},{Escape(entry.Action)},{Escape(entry.Details)},{Escape(entry.User)},{Escape(entry.IpAddress)},{entry.Timestamp:o},{Escape(entry.SessionId)}");
                    }
                    await System.IO.File.WriteAllTextAsync(file.Path, csv.ToString());

                    var logger = App.Services.GetRequiredService<PartFinder.Services.ActivityLogger>();
                    logger.LogUserAction("Export", $"Exported {ViewModel.AuditLogs.Count} audit records to {file.Name}");

                    var confirm = new ContentDialog
                    {
                        Title = "Export Complete",
                        Content = new TextBlock { Text = $"Successfully exported to {file.Name}", FontSize = 13 },
                        PrimaryButtonText = "OK",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot,
                    };
                    await confirm.ShowAsync();
                    
                    await ViewModel.LoadAuditLogsAsync();
                }
                catch (Exception ex)
                {
                    var err = new ContentDialog
                    {
                        Title = "Export Failed",
                        Content = new TextBlock { Text = ex.Message, FontSize = 13 },
                        CloseButtonText = "OK",
                        XamlRoot = XamlRoot,
                    };
                    await err.ShowAsync();
                }
            }
        }
    }

    private string Escape(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }
        return text;
    }

    private async void OnViewDetailClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AuditLogEntry entry)
        {
            var content = new StackPanel { Spacing = 10, MinWidth = 420 };

            void AddRow(string label, string value)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextTertiaryBrush"] };
                var val = new TextBlock { Text = value, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(val, 1);
                row.Children.Add(lbl);
                row.Children.Add(val);
                content.Children.Add(row);
            }

            AddRow("Event ID", entry.EventId[..Math.Min(16, entry.EventId.Length)]);
            AddRow("Type", entry.EventType);
            AddRow("Action", entry.Action);
            AddRow("Details", entry.Details);
            AddRow("User", entry.User);
            AddRow("Device & IP", string.IsNullOrWhiteSpace(entry.IpAddress) ? "Unknown" : entry.IpAddress);
            AddRow("Session ID", string.IsNullOrWhiteSpace(entry.SessionId) ? "Unknown" : entry.SessionId[..Math.Min(12, entry.SessionId.Length)]);
            AddRow("Timestamp", entry.FormattedTimestamp);

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
}
