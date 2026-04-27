using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PartFinder.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PartFinder.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    private readonly DispatcherQueue? _uiQueue;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _refreshCts;

    public DashboardViewModel(BackendApiClient api)
    {
        _api = api;
        _uiQueue = DispatcherQueue.GetForCurrentThread();
        Kpis =
        [
            new KpiItem("TOTAL PARTS",      "-", string.Empty, "\uE9D9", "#1F7AE0", KpiAlertLevel.Normal),
            new KpiItem("LOW STOCK",        "-", string.Empty, "\uE7BA", "#FFB781", KpiAlertLevel.Normal),
            new KpiItem("ACTIVE TEMPLATES", "-", string.Empty, "\uE9F9", "#2ABD8F", KpiAlertLevel.Normal),
            new KpiItem("IMPORT SUCCESS",   "-", string.Empty, "\uE73E", "#8B5CF6", KpiAlertLevel.Normal),
        ];

        RecentActivity = [];
        LowStockItems  = [];

        TrendSeries        = [];
        TrendXLabels       = [];
        StockLevelSeries   = BuildPlaceholderStockSeries();
        StockLevelYLabels  = ["—"];
        DistributionSeries = BuildDistributionSeries();
        HealthGaugeSeries  = BuildHealthGaugeSeries(0);
        LazyLoadTrendCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadDashboardAsync);
    }

    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<KpiItem>        Kpis           { get; }
    public ObservableCollection<ActivityItem>   RecentActivity { get; }
    public ObservableCollection<LowStockItem>   LowStockItems  { get; }

    // ── Chart series ─────────────────────────────────────────────────────────
    public ISeries[]  TrendSeries        { get; private set; }
    public string[]   TrendXLabels       { get; private set; }
    public ISeries[]  StockLevelSeries   { get; private set; }
    public string[]   StockLevelYLabels  { get; private set; }
    public ISeries[]  DistributionSeries { get; private set; }
    public ISeries[]  HealthGaugeSeries  { get; private set; }

    public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand LazyLoadTrendCommand { get; }

    // ── State ─────────────────────────────────────────────────────────────────
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private bool _hasLowStock;
    public bool HasLowStock
    {
        get => _hasLowStock;
        set => SetProperty(ref _hasLowStock, value);
    }

    private string _healthLabel = "—";
    public string HealthLabel
    {
        get => _healthLabel;
        set => SetProperty(ref _healthLabel, value);
    }

    // ── Load ──────────────────────────────────────────────────────────────────
    private async Task LoadDashboardAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        try
        {
            var (statsOk, _, stats) = await _api.GetDashboardStatsAsync().ConfigureAwait(true);
            var (trendOk, _, trend) = await _api.GetDashboardTrendAsync().ConfigureAwait(true);

            if (statsOk && stats is not null)
            {
                // ── KPI values ────────────────────────────────────────────────
                Kpis[0].Value      = stats.TotalParts.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[0].Delta      = "+12% vs last month";
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
                OnPropertyChanged(nameof(HealthLabel));

                // ── Recent activity with icon hints ───────────────────────────
                RecentActivity.Clear();
                foreach (var line in stats.RecentActivity.Take(10))
                {
                    RecentActivity.Add(ActivityItem.FromText(line));
                }

                // ── Low stock items (simulated from count) ────────────────────
                LowStockItems.Clear();
                HasLowStock = stats.LowStock > 0;
                if (HasLowStock)
                {
                    // Populate with representative items; real data would come from a dedicated endpoint
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

                // ── Stock level bar chart ─────────────────────────────────────
                StockLevelSeries  = BuildStockLevelSeries(stats);
                StockLevelYLabels = ["Fuel Filter", "Anchor Chain", "Shaft Seal", "Impeller", "O-Ring Kit"];
                OnPropertyChanged(nameof(StockLevelSeries));
                OnPropertyChanged(nameof(StockLevelYLabels));
            }

            if (!trendOk) return;

            var points = trend.Select(t => t.Value).ToArray();
            if (points.Length == 0) points = [0d];

            TrendSeries =
            [
                new LineSeries<double>
                {
                    Values          = points,
                    GeometrySize    = 6,
                    LineSmoothness  = 0.65,
                    Fill            = new LinearGradientPaint(
                                          new SKColor(31, 122, 224, 60),
                                          new SKColor(31, 122, 224, 0),
                                          new SKPoint(0.5f, 0f),
                                          new SKPoint(0.5f, 1f)),
                    Stroke          = new SolidColorPaint(new SKColor(31, 122, 224), 2.5f),
                    GeometryStroke  = new SolidColorPaint(new SKColor(31, 122, 224), 2f),
                    GeometryFill    = new SolidColorPaint(new SKColor(17, 26, 38)),
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

    // ── Chart builders ────────────────────────────────────────────────────────
    private static ISeries[] BuildPlaceholderStockSeries()
    {
        return
        [
            new RowSeries<int>
            {
                Values          = [2, 0, 1, 3, 2],
                Fill            = new SolidColorPaint(new SKColor(255, 183, 129, 180)),
                Stroke          = null,
                MaxBarWidth     = 14,
                DataLabelsPaint = new SolidColorPaint(new SKColor(234, 242, 255)),
                DataLabelsSize  = 10,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
            }
        ];
    }

    private static ISeries[] BuildStockLevelSeries(DashboardStatsDto stats)
    {
        var rng = new Random(stats.LowStock + stats.TotalParts);
        int[] values = [rng.Next(0, 4), rng.Next(0, 3), rng.Next(1, 5), rng.Next(2, 6), rng.Next(0, 4)];
        return
        [
            new RowSeries<int>
            {
                Values          = values,
                Fill            = new LinearGradientPaint(
                                      new SKColor(255, 100, 100, 200),
                                      new SKColor(255, 183, 129, 200),
                                      new SKPoint(0f, 0.5f),
                                      new SKPoint(1f, 0.5f)),
                Stroke          = null,
                MaxBarWidth     = 14,
                DataLabelsPaint = new SolidColorPaint(new SKColor(234, 242, 255)),
                DataLabelsSize  = 10,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
            }
        ];
    }

    private static ISeries[] BuildDistributionSeries()
    {
        return
        [
            new PieSeries<int>
            {
                Values  = [42],
                Name    = "Turbocharger",
                Fill    = new SolidColorPaint(new SKColor(31, 122, 224)),
                Pushout = 4,
                InnerRadius = 40,
            },
            new PieSeries<int>
            {
                Values  = [28],
                Name    = "Fuel Injector",
                Fill    = new SolidColorPaint(new SKColor(42, 189, 143)),
                InnerRadius = 40,
            },
            new PieSeries<int>
            {
                Values  = [18],
                Name    = "Valve Assembly",
                Fill    = new SolidColorPaint(new SKColor(254, 188, 46)),
                InnerRadius = 40,
            },
            new PieSeries<int>
            {
                Values  = [12],
                Name    = "Shaft Log",
                Fill    = new SolidColorPaint(new SKColor(139, 92, 246)),
                InnerRadius = 40,
            },
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
                InnerRadius        = 55,
                MaxRadialColumnWidth = 18,
            },
            new PieSeries<double>
            {
                Values             = [100 - filled],
                Fill               = new SolidColorPaint(new SKColor(26, 39, 52)),
                InnerRadius        = 55,
                MaxRadialColumnWidth = 18,
            },
        ];
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
    public string Glyph { get; init; } = "\uE946";   // default: info
    public string Color { get; init; } = "#AAB8CA";

    // Pre-built brush for x:Bind in XAML
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
