using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using SkiaSharp;

namespace PartFinder.Views.Pages;

public sealed partial class AnalyticsPage : Page
{
    public AnalyticsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeCharts();
    }

    private void InitializeCharts()
    {
        // Monthly Parts Trend - Smooth Line chart with area fill
        MonthlyTrendChart.Series = new ISeries[]
        {
            new LineSeries<double>
            {
                Values = new double[] { 142, 168, 195, 178, 210, 224, 198, 235, 247, 261, 238, 247 },
                Name = "Parts Added",
                Stroke = new SolidColorPaint(new SKColor(31, 122, 224)) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(new SKColor(31, 122, 224, 40)),
                GeometrySize = 8,
                GeometryStroke = new SolidColorPaint(new SKColor(31, 122, 224)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                LineSmoothness = 0.65,
            },
            new LineSeries<double>
            {
                Values = new double[] { 45, 62, 58, 71, 84, 79, 92, 88, 95, 102, 87, 94 },
                Name = "Parts Removed",
                Stroke = new SolidColorPaint(new SKColor(42, 189, 143)) { StrokeThickness = 3 },
                Fill = new SolidColorPaint(new SKColor(42, 189, 143, 30)),
                GeometrySize = 7,
                GeometryStroke = new SolidColorPaint(new SKColor(42, 189, 143)) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                LineSmoothness = 0.65,
            },
        };

        MonthlyTrendChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" },
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            }
        };
        MonthlyTrendChart.YAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            }
        };

        // Category Distribution Pie - 3D-style with inner radius (donut)
        CategoryPieChart.Series = new ISeries[]
        {
            new PieSeries<double> { Values = new double[] { 42 }, Name = "Engine", Fill = new SolidColorPaint(new SKColor(31, 122, 224)), InnerRadius = 60, MaxRadialColumnWidth = 40 },
            new PieSeries<double> { Values = new double[] { 24 }, Name = "Fuel Systems", Fill = new SolidColorPaint(new SKColor(42, 189, 143)), InnerRadius = 60, MaxRadialColumnWidth = 40 },
            new PieSeries<double> { Values = new double[] { 18 }, Name = "Valve Systems", Fill = new SolidColorPaint(new SKColor(254, 188, 46)), InnerRadius = 60, MaxRadialColumnWidth = 40 },
            new PieSeries<double> { Values = new double[] { 10 }, Name = "Cooling", Fill = new SolidColorPaint(new SKColor(139, 92, 246)), InnerRadius = 60, MaxRadialColumnWidth = 40 },
            new PieSeries<double> { Values = new double[] { 6 }, Name = "Electrical", Fill = new SolidColorPaint(new SKColor(255, 95, 87)), InnerRadius = 60, MaxRadialColumnWidth = 40 },
        };

        // Stock Movement - Rounded column bars
        StockMovementChart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new double[] { 120, 145, 98, 167, 134, 189, 156 },
                Name = "Inbound",
                Fill = new SolidColorPaint(new SKColor(31, 122, 224)),
                Rx = 4, Ry = 4,
            },
            new ColumnSeries<double>
            {
                Values = new double[] { 85, 92, 74, 118, 96, 142, 108 },
                Name = "Outbound",
                Fill = new SolidColorPaint(new SKColor(255, 95, 87)),
                Rx = 4, Ry = 4,
            },
        };
        StockMovementChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" },
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            }
        };
        StockMovementChart.YAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            }
        };

        // Inventory Health Donut - gauge style
        HealthDonutChart.Series = new ISeries[]
        {
            new PieSeries<double> { Values = new double[] { 1206 }, Name = "In Stock", Fill = new SolidColorPaint(new SKColor(42, 189, 143)), InnerRadius = 70, MaxRadialColumnWidth = 28 },
            new PieSeries<double> { Values = new double[] { 34 }, Name = "Low Stock", Fill = new SolidColorPaint(new SKColor(254, 188, 46)), InnerRadius = 70, MaxRadialColumnWidth = 28 },
            new PieSeries<double> { Values = new double[] { 8 }, Name = "Out of Stock", Fill = new SolidColorPaint(new SKColor(255, 95, 87)), InnerRadius = 70, MaxRadialColumnWidth = 28 },
        };

        // Supplier Spend - Horizontal bar chart
        SupplierSpendChart.Series = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Values = new double[] { 2.4, 1.8, 1.2, 0.9, 0.7, 0.5 },
                Name = "Spend ($M)",
                Fill = new SolidColorPaint(new SKColor(31, 122, 224)),
                Rx = 4, Ry = 4,
            },
            new ColumnSeries<double>
            {
                Values = new double[] { 142, 98, 67, 54, 41, 28 },
                Name = "Orders",
                Fill = new SolidColorPaint(new SKColor(42, 189, 143)),
                Rx = 4, Ry = 4,
                ScalesYAt = 1,
            },
        };
        SupplierSpendChart.XAxes = new Axis[]
        {
            new Axis
            {
                Labels = new[] { "Global Parts", "Premium Motors", "Tech Comp.", "Marine Sys.", "Valve Corp.", "Others" },
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            }
        };
        SupplierSpendChart.YAxes = new Axis[]
        {
            new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(123, 141, 168)),
                TextSize = 10,
                SeparatorsPaint = new SolidColorPaint(new SKColor(42, 61, 88, 60)),
            },
            new Axis
            {
                LabelsPaint = new SolidColorPaint(new SKColor(42, 189, 143)),
                TextSize = 10,
                ShowSeparatorLines = false,
                Position = LiveChartsCore.Measure.AxisPosition.End,
            }
        };
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var content = new StackPanel { Spacing = 16, MinWidth = 380 };

        var descLabel = new TextBlock
        {
            Text = "Choose the format for your analytics report export.",
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };

        var pdfButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
        };
        var pdfContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        pdfContent.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 255, 95, 87)),
            Child = new FontIcon { Glyph = "\uEA90", FontSize = 16, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["DangerBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var pdfText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        pdfText.Children.Add(new TextBlock { Text = "Export as PDF", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        pdfText.Children.Add(new TextBlock { Text = "Formatted report with charts", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        pdfContent.Children.Add(pdfText);
        pdfButton.Content = pdfContent;

        var excelButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
        };
        var excelContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        excelContent.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 42, 189, 143)),
            Child = new FontIcon { Glyph = "\uE8A1", FontSize = 16, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["SuccessBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var excelText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        excelText.Children.Add(new TextBlock { Text = "Export as Excel", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        excelText.Children.Add(new TextBlock { Text = "Spreadsheet with raw data", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        excelContent.Children.Add(excelText);
        excelButton.Content = excelContent;

        var csvButton = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
        };
        var csvContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        csvContent.Children.Add(new Border
        {
            Width = 36, Height = 36, CornerRadius = new CornerRadius(8),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(20, 31, 122, 224)),
            Child = new FontIcon { Glyph = "\uE8A5", FontSize = 16, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["AccentPrimaryBrush"], HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var csvText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        csvText.Children.Add(new TextBlock { Text = "Export as CSV", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        csvText.Children.Add(new TextBlock { Text = "Comma-separated values", FontSize = 11, Foreground = (Microsoft.UI.Xaml.Media.Brush)Resources["TextTertiaryBrush"] });
        csvContent.Children.Add(csvText);
        csvButton.Content = csvContent;

        content.Children.Add(descLabel);
        content.Children.Add(pdfButton);
        content.Children.Add(excelButton);
        content.Children.Add(csvButton);

        var dialog = new ContentDialog
        {
            Title = "Export Analytics Report",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            Content = content,
        };

        string? selectedFormat = null;
        pdfButton.Click += (_, _) => { selectedFormat = "PDF"; dialog.Hide(); };
        excelButton.Click += (_, _) => { selectedFormat = "Excel"; dialog.Hide(); };
        csvButton.Click += (_, _) => { selectedFormat = "CSV"; dialog.Hide(); };

        await dialog.ShowAsync();

        if (selectedFormat is not null)
        {
            await ShowExportConfirmationAsync(selectedFormat);
        }
    }

    private async Task ShowExportConfirmationAsync(string format)
    {
        var dialog = new ContentDialog
        {
            Title = "Export Started",
            Content = new TextBlock
            {
                Text = $"Your analytics report is being exported as {format}. The file will be ready shortly.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
            },
            PrimaryButtonText = "OK",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;

        // Animate refresh icon
        var storyboard = new Storyboard();
        var rotateAnim = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = new Duration(TimeSpan.FromSeconds(0.8)),
            RepeatBehavior = new RepeatBehavior(2),
        };
        Storyboard.SetTarget(rotateAnim, RefreshIcon);
        Storyboard.SetTargetProperty(rotateAnim, "(UIElement.RenderTransform).(RotateTransform.Angle)");
        RefreshIcon.RenderTransform = new Microsoft.UI.Xaml.Media.RotateTransform();
        RefreshIcon.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        storyboard.Children.Add(rotateAnim);
        storyboard.Begin();

        await Task.Delay(1600);
        storyboard.Stop();
        InitializeCharts();
        RefreshButton.IsEnabled = true;
    }
}
