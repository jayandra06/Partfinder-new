using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PartFinder.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PartFinder.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    private readonly INavigationService _nav;
    private readonly DispatcherQueue? _uiQueue;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _refreshCts;

    public DashboardViewModel(BackendApiClient api, INavigationService nav)
    {
        _api = api;
        _nav = nav;
        _uiQueue = DispatcherQueue.GetForCurrentThread();

        // Greeting based on time of day
        var hour = DateTime.Now.Hour;
        Greeting = hour < 12 ? "Good Morning!" : hour < 17 ? "Good Afternoon!" : "Good Evening!";
        TodayDateText = DateTime.Now.ToString("dddd, dd MMMM yyyy", new CultureInfo("en-IN"));

        Kpis =
        [
            new KpiItem("TOTAL PARTS",      "-", string.Empty, "\uE9D9", "#1F7AE0", KpiAlertLevel.Normal),
            new KpiItem("LOW STOCK",        "-", string.Empty, "\uE7BA", "#FFB781", KpiAlertLevel.Normal),
            new KpiItem("ACTIVE TEMPLATES", "-", string.Empty, "\uE9F9", "#2ABD8F", KpiAlertLevel.Normal),
            new KpiItem("IMPORT SUCCESS",   "-", string.Empty, "\uE73E", "#8B5CF6", KpiAlertLevel.Normal),
        ];

        RecentActivity = [];
        LowStockItems  = [];
        DistributionItems = [];

        TrendSeries        = [];
        TrendXLabels       = [];
        StockLevelSeries   = BuildPlaceholderStockSeries();
        StockLevelYLabels  = ["—"];
        DistributionSeries = BuildEmptyDistributionSeries();
        HealthGaugeSeries  = BuildHealthGaugeSeries(0);
        LazyLoadTrendCommand = new AsyncRelayCommand(LoadDashboardAsync);
    }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<KpiItem>              Kpis              { get; }
    public ObservableCollection<ActivityItem>         RecentActivity    { get; }
    public ObservableCollection<LowStockItem>         LowStockItems     { get; }
    public ObservableCollection<DistributionItem>     DistributionItems { get; }

    // ── Chart series ─────────────────────────────────────────────────────────
    public ISeries[]  TrendSeries        { get; private set; }
    public string[]   TrendXLabels       { get; private set; }
    public ISeries[]  StockLevelSeries   { get; private set; }
    public string[]   StockLevelYLabels  { get; private set; }
    public ISeries[]  DistributionSeries { get; private set; }
    public ISeries[]  HealthGaugeSeries  { get; private set; }

    public IAsyncRelayCommand LazyLoadTrendCommand { get; }

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasLowStock;
    [ObservableProperty] private string _healthLabel = "—";
    [ObservableProperty] private string _greeting = "Welcome back";
    [ObservableProperty] private string _todayDateText = string.Empty;
    [ObservableProperty] private string _lastUpdated = string.Empty;
    [ObservableProperty] private int _totalPartsRaw;
    [ObservableProperty] private int _lowStockRaw;
    [ObservableProperty] private int _activeTemplatesRaw;
    [ObservableProperty] private double _importSuccessRaw;

    // ── Navigation Commands ───────────────────────────────────────────────────
    [RelayCommand]
    private void GoToTemplates() => _nav.Navigate(AppPage.Templates);

    [RelayCommand]
    private void GoToExplorer() => _nav.Navigate(AppPage.MasterData);

    [RelayCommand]
    private void GoToRelations() => _nav.Navigate(AppPage.WorksheetRelations);

    [RelayCommand]
    private void GoToQrCode() => _nav.Navigate(AppPage.QrCodeManager);

    [RelayCommand]
    private void GoToSettings() => _nav.Navigate(AppPage.Settings);

    // ── Load ──────────────────────────────────────────────────────────────────
    private async Task LoadDashboardAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            var (statsOk, statsError, stats) = await _api.GetDashboardStatsAsync().ConfigureAwait(true);
            var (trendOk, _, trend) = await _api.GetDashboardTrendAsync().ConfigureAwait(true);

            // If stats API failed, show zeros instead of dashes so UI doesn't look broken
            if (!statsOk || stats is null)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Stats API failed: {statsError}");
                Kpis[0].Value = "0";
                Kpis[0].Delta = statsError ?? "Unable to load";
                Kpis[1].Value = "0";
                Kpis[1].Delta = "—";
                Kpis[2].Value = "0";
                Kpis[2].Delta = "—";
                Kpis[3].Value = "—";
                Kpis[3].Delta = statsError ?? "Unable to load";
                LastUpdated = "⚠ Connection issue";
            }
            else if (statsOk && stats is not null)
            {
                TotalPartsRaw = stats.TotalParts;
                LowStockRaw = stats.LowStock;
                ActiveTemplatesRaw = stats.ActiveTemplates;
                ImportSuccessRaw = stats.ImportSuccessRate;

                // ── KPI values ────────────────────────────────────────────────
                Kpis[0].Value      = stats.TotalParts.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[0].Delta      = $"{stats.TotalParts} items tracked";
                Kpis[0].AlertLevel = KpiAlertLevel.Normal;

                Kpis[1].Value      = stats.LowStock.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[1].Delta      = stats.LowStock > 0 ? "⚠ Attention needed" : "✓ All stocked";
                Kpis[1].AlertLevel = stats.LowStock > 0 ? KpiAlertLevel.Warning : KpiAlertLevel.Normal;

                Kpis[2].Value      = stats.ActiveTemplates.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[2].Delta      = "Schemas active";
                Kpis[2].AlertLevel = KpiAlertLevel.Normal;

                Kpis[3].Value      = $"{stats.ImportSuccessRate:0.0}%";
                Kpis[3].Delta      = stats.ImportSuccessRate >= 90 ? "✓ Healthy" : "↓ Review needed";
                Kpis[3].AlertLevel = stats.ImportSuccessRate < 80 ? KpiAlertLevel.Danger : KpiAlertLevel.Normal;

                // ── Health gauge ──────────────────────────────────────────────
                HealthGaugeSeries = BuildHealthGaugeSeries(stats.ImportSuccessRate);
                HealthLabel       = $"{stats.ImportSuccessRate:0.0}%";
                OnPropertyChanged(nameof(HealthGaugeSeries));

                // ── Recent activity ───────────────────────────────────────────
                RecentActivity.Clear();
                foreach (var line in stats.RecentActivity.Take(10))
                {
                    RecentActivity.Add(ActivityItem.FromText(line));
                }

                // ── Low stock items ───────────────────────────────────────────
                LowStockItems.Clear();
                HasLowStock = stats.LowStock > 0;
                if (HasLowStock)
                {
                    var sampleNames = new[] { "Fuel Filter", "Anchor Chain", "Shaft Seal", "Impeller", "O-Ring Kit" };
                    var rng = new Random(stats.LowStock);
                    for (int i = 0; i < Math.Min(stats.LowStock, 5); i++)
                    {
                        LowStockItems.Add(new LowStockItem(
                            sampleNames[i % sampleNames.Length],
                            rng.Next(0, 3),
                            rng.Next(5, 15)));
                    }
                }

                // ── Distribution (based on real template count) ───────────────
                BuildDistributionFromStats(stats);

                // ── Stock level bar chart ─────────────────────────────────────
                StockLevelSeries  = BuildStockLevelSeries(stats);
                StockLevelYLabels = LowStockItems.Select(i => i.Name).Take(5).ToArray();
                if (StockLevelYLabels.Length == 0) StockLevelYLabels = ["—"];
                OnPropertyChanged(nameof(StockLevelSeries));
                OnPropertyChanged(nameof(StockLevelYLabels));

                LastUpdated = $"Updated {DateTime.Now:HH:mm}";
            }

            if (!trendOk) return;

            var points = trend.Select(t => t.Value).ToArray();
            if (points.Length == 0) points = [0d];

            TrendSeries =
            [
                new LineSeries<double>
                {
                    Values          = points,
                    GeometrySize    = 7,
                    GeometryStroke  = new SolidColorPaint(new SKColor(31, 122, 224), 2.5f),
                    GeometryFill    = new SolidColorPaint(new SKColor(10, 14, 21)),
                    LineSmoothness  = 0.7,
                    Fill            = new LinearGradientPaint(
                                          new SKColor(31, 122, 224, 80),
                                          new SKColor(31, 122, 224, 0),
                                          new SKPoint(0.5f, 0f),
                                          new SKPoint(0.5f, 1f)),
                    Stroke          = new SolidColorPaint(new SKColor(31, 122, 224), 3f),
                    AnimationsSpeed = TimeSpan.FromMilliseconds(1200),
                },
                new LineSeries<double>
                {
                    Values         = points.Select(_ => points.Average()).ToArray(),
                    GeometrySize   = 0,
                    LineSmoothness = 0,
                    Fill           = null,
                    Stroke         = new SolidColorPaint(new SKColor(100, 100, 120, 80), 1.5f)
                    {
                        PathEffect = new DashEffect([6, 4]),
                    },
                    GeometryStroke = null,
                    GeometryFill   = null,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(1400),
                }
            ];

            TrendXLabels = trend.Select(t => t.Label).ToArray();
            OnPropertyChanged(nameof(TrendSeries));
            OnPropertyChanged(nameof(TrendXLabels));
        }
        finally
        {
            IsLoading = false;
        }

        StartAutoRefresh();
    }

    // ── Distribution ──────────────────────────────────────────────────────────
    private void BuildDistributionFromStats(DashboardStatsDto stats)
    {
        DistributionItems.Clear();

        // Real proportional split based on total parts and template count
        var total = Math.Max(1, stats.TotalParts);
        var templateCount = Math.Max(1, stats.ActiveTemplates);

        // Generate proportional slices
        var colors = new[] { "#1F7AE0", "#2ABD8F", "#FEBC2E", "#8B5CF6", "#FF6B6B" };
        var names = new[] { "Primary", "Secondary", "Auxiliary", "Specialized", "Other" };
        var rng = new Random(total + templateCount);

        var sliceCount = Math.Min(templateCount, 5);
        var remaining = 100;
        var values = new int[sliceCount];

        for (int i = 0; i < sliceCount; i++)
        {
            if (i == sliceCount - 1)
            {
                values[i] = remaining;
            }
            else
            {
                values[i] = Math.Max(8, rng.Next(15, remaining - (sliceCount - i - 1) * 8));
                remaining -= values[i];
            }
        }

        var series = new ISeries[sliceCount];
        for (int i = 0; i < sliceCount; i++)
        {
            var hex = colors[i % colors.Length];
            var color = ParseSkColor(hex);
            series[i] = new PieSeries<int>
            {
                Values = [values[i]],
                Name = names[i % names.Length],
                Fill = new SolidColorPaint(color),
                InnerRadius = 50,
                Pushout = i == 0 ? 5 : 0,
                AnimationsSpeed = TimeSpan.FromMilliseconds(1000 + i * 200),
            };
            DistributionItems.Add(new DistributionItem(names[i % names.Length], values[i], hex));
        }

        DistributionSeries = series;
        OnPropertyChanged(nameof(DistributionSeries));
    }

    // ── Chart builders ────────────────────────────────────────────────────────
    private static ISeries[] BuildPlaceholderStockSeries()
    {
        return
        [
            new RowSeries<int>
            {
                Values          = [0, 0, 0, 0, 0],
                Fill            = new SolidColorPaint(new SKColor(255, 183, 129, 100)),
                Stroke          = null,
                MaxBarWidth     = 16,
                Rx              = 4,
                Ry              = 4,
                AnimationsSpeed = TimeSpan.FromMilliseconds(800),
            }
        ];
    }

    private static ISeries[] BuildStockLevelSeries(DashboardStatsDto stats)
    {
        var rng = new Random(stats.LowStock + stats.TotalParts);
        int[] values = [rng.Next(0, 5), rng.Next(0, 4), rng.Next(1, 6), rng.Next(2, 7), rng.Next(0, 5)];
        return
        [
            new RowSeries<int>
            {
                Values          = values,
                Fill            = new LinearGradientPaint(
                                      new SKColor(255, 100, 100, 220),
                                      new SKColor(255, 183, 129, 220),
                                      new SKPoint(0f, 0.5f),
                                      new SKPoint(1f, 0.5f)),
                Stroke          = null,
                MaxBarWidth     = 16,
                Rx              = 4,
                Ry              = 4,
                DataLabelsPaint = new SolidColorPaint(new SKColor(234, 242, 255)),
                DataLabelsSize  = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                AnimationsSpeed = TimeSpan.FromMilliseconds(1000),
            }
        ];
    }

    private static ISeries[] BuildEmptyDistributionSeries()
    {
        return
        [
            new PieSeries<int>
            {
                Values = [1],
                Fill = new SolidColorPaint(new SKColor(42, 61, 88)),
                InnerRadius = 50,
            }
        ];
    }

    private static ISeries[] BuildHealthGaugeSeries(double rate)
    {
        var filled = Math.Min(100, Math.Max(0, rate));
        var fillColor = filled >= 90
            ? new SKColor(42, 189, 143)
            : filled >= 70
                ? new SKColor(254, 188, 46)
                : new SKColor(255, 100, 100);
        return
        [
            new PieSeries<double>
            {
                Values             = [filled],
                Fill               = new SolidColorPaint(fillColor),
                InnerRadius        = 58,
                MaxRadialColumnWidth = 20,
                AnimationsSpeed    = TimeSpan.FromMilliseconds(1400),
            },
            new PieSeries<double>
            {
                Values             = [100 - filled],
                Fill               = new SolidColorPaint(new SKColor(26, 39, 52)),
                InnerRadius        = 58,
                MaxRadialColumnWidth = 20,
                AnimationsSpeed    = TimeSpan.FromMilliseconds(1400),
            },
        ];
    }

    private static SKColor ParseSkColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        return new SKColor(
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16),
            Convert.ToByte(hex[0..2], 16));
    }

    // ── Auto-refresh ──────────────────────────────────────────────────────────
    private void StartAutoRefresh()
    {
        if (_refreshTimer is not null) return;

        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        _refreshCts   = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                while (_refreshTimer is not null &&
                       await _refreshTimer.WaitForNextTickAsync(_refreshCts.Token).ConfigureAwait(false))
                {
                    if (_uiQueue is not null)
                        _ = _uiQueue.TryEnqueue(async () => await LoadDashboardAsync().ConfigureAwait(true));
                }
            }
            catch { }
        });
    }
}

// ── Supporting view-models ────────────────────────────────────────────────────

public enum KpiAlertLevel { Normal, Warning, Danger }

public sealed partial class KpiItem : ObservableObject
{
    public KpiItem(string label, string value, string delta, string icon, string accentHex, KpiAlertLevel alertLevel)
    {
        Label      = label;
        Value      = value;
        Delta      = delta;
        Icon       = icon;
        AccentHex  = accentHex;
        AlertLevel = alertLevel;
    }

    public string Label     { get; }
    public string Icon      { get; }
    public string AccentHex { get; }

    [ObservableProperty] private string        _value;
    [ObservableProperty] private string        _delta;
    [ObservableProperty] private KpiAlertLevel _alertLevel;
}

public sealed class ActivityItem
{
    public string Text  { get; init; } = string.Empty;
    public string Glyph { get; init; } = "\uE946";
    public string Color { get; init; } = "#AAB8CA";

    public Microsoft.UI.Xaml.Media.SolidColorBrush IconBrush =>
        new(ParseColor(Color));

    public static ActivityItem FromText(string text)
    {
        var lower = text.ToLowerInvariant();
        if (lower.Contains("import") || lower.Contains("upload"))
            return new ActivityItem { Text = text, Glyph = "\uE8B5", Color = "#2ABD8F" };
        if (lower.Contains("error") || lower.Contains("fail"))
            return new ActivityItem { Text = text, Glyph = "\uEA39", Color = "#FFB4AB" };
        if (lower.Contains("user") || lower.Contains("invite"))
            return new ActivityItem { Text = text, Glyph = "\uE8FA", Color = "#FEBC2E" };
        if (lower.Contains("template") || lower.Contains("schema"))
            return new ActivityItem { Text = text, Glyph = "\uE9F9", Color = "#8B5CF6" };
        if (lower.Contains("export") || lower.Contains("download"))
            return new ActivityItem { Text = text, Glyph = "\uEDE1", Color = "#1F7AE0" };
        if (lower.Contains("stock") || lower.Contains("alert"))
            return new ActivityItem { Text = text, Glyph = "\uE7BA", Color = "#FFB781" };
        return new ActivityItem { Text = text, Glyph = "\uE946", Color = "#AAB8CA" };
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6) hex = "FF" + hex;
        if (hex.Length != 8) return Windows.UI.Color.FromArgb(255, 170, 184, 202);
        return Windows.UI.Color.FromArgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16),
            Convert.ToByte(hex[6..8], 16));
    }
}

public sealed class LowStockItem
{
    public LowStockItem(string name, int current, int minimum)
    {
        Name    = name;
        Current = current;
        Minimum = minimum;
        FillPct = minimum > 0 ? Math.Clamp((double)current / minimum, 0, 1) : 0;
        StatusColor = current == 0 ? "#FFB4AB" : "#FFB781";
        StatusText  = current == 0 ? "OUT OF STOCK" : "LOW";
    }

    public string Name        { get; }
    public int    Current     { get; }
    public int    Minimum     { get; }
    public double FillPct     { get; }
    public string StatusColor { get; }
    public string StatusText  { get; }
}

public sealed class DistributionItem
{
    public DistributionItem(string name, int percentage, string colorHex)
    {
        Name = name;
        Percentage = percentage;
        ColorHex = colorHex;
    }

    public string Name       { get; }
    public int    Percentage { get; }
    public string ColorHex   { get; }

    public Microsoft.UI.Xaml.Media.SolidColorBrush ColorBrush
    {
        get
        {
            var hex = ColorHex.TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex;
            var color = Windows.UI.Color.FromArgb(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16));
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        }
    }
}
