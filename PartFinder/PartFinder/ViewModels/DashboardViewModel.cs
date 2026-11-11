using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.Themes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PartFinder.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;
    private readonly INavigationService _nav;
    private readonly IThemeService _themeService;
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _masterData;
    private readonly MongoInventoryService _inventory;
    private readonly MongoAuditService _audit;
    private readonly IAppStateStore _appState;
    private readonly DispatcherQueue? _uiQueue;
    private PeriodicTimer? _refreshTimer;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _loadCts;
    private CancellationTokenSource? _distributionCts;
    private int _loadGeneration;
    private DateTimeOffset _lastLoadUtc = DateTimeOffset.MinValue;
    private bool _hasCompletedInitialLoad;
    private double _lastImportSuccessRate;

    public DashboardViewModel(
        BackendApiClient api,
        INavigationService nav,
        IThemeService themeService,
        ITemplateSchemaService templateSchema,
        IMasterDataRecordsService masterData,
        MongoInventoryService inventory,
        MongoAuditService audit,
        IAppStateStore appState)
    {
        _api = api;
        _nav = nav;
        _themeService = themeService;
        _templateSchema = templateSchema;
        _masterData = masterData;
        _inventory = inventory;
        _audit = audit;
        _appState = appState;
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
        LazyLoadTrendCommand = new AsyncRelayCommand(() => PrepareForPageDisplayAsync());

        _themeService.ThemeChanged += (_, _) => OnAppThemeChanged();
        RefreshChartAppearance();
    }

    public DrawMarginFrame ChartDrawMarginFrame { get; private set; } = new();
    public Axis[] ChartXAxes { get; private set; } = [];
    public Axis[] ChartYAxes { get; private set; } = [];
    public Axis[] TrendXAxes { get; private set; } = [];
    public Axis[] StockLevelYAxes { get; private set; } = [];
    // ── Collections ──────────────────────────────────────────────────────────
    public ObservableCollection<KpiItem>              Kpis              { get; }
    public ObservableCollection<ActivityItem>         RecentActivity    { get; }
    public ObservableCollection<LowStockItem>         LowStockItems     { get; }
    public ObservableCollection<DistributionItem>     DistributionItems { get; }

    // ── Chart series ─────────────────────────────────────────────────────────
    public ISeries[]  TrendSeries        { get; private set; }
    public string[]   TrendXLabels       { get; private set; } = [];
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
    private void QuickActionNewTemplate()
    {
        _appState.TemplatesPendingAction = "Create";
        _nav.Navigate(AppPage.Templates);
    }

    [RelayCommand]
    private void QuickActionImportData() => _nav.Navigate(AppPage.MasterData);

    [RelayCommand]
    private void QuickActionInviteMember() => _nav.Navigate(AppPage.UserManagement);

    [RelayCommand]
    private void QuickActionExportReport() => _nav.Navigate(AppPage.Audit);

    [RelayCommand]
    private void QuickActionScanQr() => _nav.Navigate(AppPage.QrCodeManager);

    [RelayCommand]
    private void GoToExplorer() => _nav.Navigate(AppPage.MasterData);

    [RelayCommand]
    private void GoToRelations() => _nav.Navigate(AppPage.WorksheetRelations);

    [RelayCommand]
    private void GoToQrCode() => _nav.Navigate(AppPage.QrCodeManager);

    [RelayCommand]
    private void GoToSettings() => _nav.Navigate(AppPage.Settings);

    // ── Load ──────────────────────────────────────────────────────────────────
    public Task WarmUpAsync() => LoadDashboardCoreAsync(showLoadingIndicator: false);

    /// <summary>Show cached dashboard instantly when fresh; otherwise load without blocking navigation.</summary>
    public Task PrepareForPageDisplayAsync()
    {
        var hasCached = _hasCompletedInitialLoad && Kpis[0].Value != "-";
        var isFresh = _lastLoadUtc > DateTimeOffset.UtcNow.AddSeconds(-25);
        if (hasCached && isFresh)
        {
            return Task.CompletedTask;
        }

        return LoadDashboardCoreAsync(showLoadingIndicator: !hasCached);
    }

    private async Task LoadDashboardCoreAsync(bool showLoadingIndicator)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        var generation = Interlocked.Increment(ref _loadGeneration);

        if (showLoadingIndicator)
        {
            IsLoading = true;
        }

        var loadDistributionAfter = false;
        var animateCharts = !_hasCompletedInitialLoad;
        try
        {
            ct.ThrowIfCancellationRequested();

            var statsTask = BuildWorkspaceStatsAsync(ct);
            var trendTask = _api.GetDashboardTrendAsync(ct);
            await Task.WhenAll(statsTask, trendTask).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();

            var stats = await statsTask.ConfigureAwait(true);
            var (trendOk, _, apiTrend) = await trendTask.ConfigureAwait(true);

            if (stats is null)
            {
                Kpis[0].Value = "0";
                Kpis[0].Delta = "Inventory unavailable";
                Kpis[1].Value = "0";
                Kpis[1].Delta = "—";
                Kpis[2].Value = "0";
                Kpis[2].Delta = "—";
                Kpis[3].Value = "—";
                Kpis[3].Delta = "—";
                LastUpdated = "⚠ Could not read workspace data";
            }
            else
            {
                TotalPartsRaw = stats.TotalParts;
                LowStockRaw = stats.LowStock;
                ActiveTemplatesRaw = stats.ActiveTemplates;
                ImportSuccessRaw = stats.HasImportHistory ? stats.ImportSuccessRate : 0;

                Kpis[0].Value = stats.TotalParts.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[0].Delta = stats.PartsSource == "inventory"
                    ? $"{stats.MasterDataRows:N0} master data rows"
                    : "From master data";
                Kpis[0].AlertLevel = KpiAlertLevel.Normal;

                Kpis[1].Value = stats.LowStock.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[1].Delta = stats.LowStock == 0
                    ? "✓ All stocked"
                    : stats.OutOfStock > 0 && stats.BelowMinimum > 0
                        ? $"{stats.BelowMinimum} low · {stats.OutOfStock} out"
                        : stats.OutOfStock > 0
                            ? "Out of stock"
                            : "⚠ Below minimum";
                Kpis[1].AlertLevel = stats.LowStock > 0 ? KpiAlertLevel.Warning : KpiAlertLevel.Normal;

                Kpis[2].Value = stats.ActiveTemplates.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[2].Delta = stats.LinkedTemplateCount > 0
                    ? $"{stats.LinkedTemplateCount} linked"
                    : "In master data";
                Kpis[2].AlertLevel = KpiAlertLevel.Normal;

                if (stats.HasImportHistory)
                {
                    Kpis[3].Value = $"{stats.ImportSuccessRate:0.0}%";
                    Kpis[3].Delta = stats.ImportSuccessRate >= 90 ? "✓ Healthy" : "↓ Review needed";
                    Kpis[3].AlertLevel = stats.ImportSuccessRate < 80 ? KpiAlertLevel.Danger : KpiAlertLevel.Normal;
                    _lastImportSuccessRate = stats.ImportSuccessRate;
                    HealthLabel = $"{stats.ImportSuccessRate:0.0}%";
                }
                else
                {
                    Kpis[3].Value = "—";
                    Kpis[3].Delta = "No imports yet";
                    Kpis[3].AlertLevel = KpiAlertLevel.Normal;
                    _lastImportSuccessRate = 0;
                    HealthLabel = "—";
                }

                HealthGaugeSeries = BuildHealthGaugeSeries(_lastImportSuccessRate);
                OnPropertyChanged(nameof(HealthGaugeSeries));

                RecentActivity.Clear();
                foreach (var line in stats.RecentActivity.Take(10))
                {
                    RecentActivity.Add(ActivityItem.FromText(line));
                }

                LowStockItems.Clear();
                foreach (var item in stats.LowStockItems)
                {
                    LowStockItems.Add(item);
                }

                HasLowStock = stats.LowStock > 0;

                StockLevelSeries = BuildStockLevelSeries(stats.LowStockItems, !_themeService.IsDark);
                StockLevelYLabels = LowStockItems.Select(i => i.Name).Take(5).ToArray();
                if (StockLevelYLabels.Length == 0)
                {
                    StockLevelYLabels = ["—"];
                }

                StockLevelYAxes = DashboardChartAppearance.BuildStockLevelYAxes(StockLevelYLabels, _themeService.IsDark);
                OnPropertyChanged(nameof(StockLevelSeries));
                OnPropertyChanged(nameof(StockLevelYLabels));
                OnPropertyChanged(nameof(StockLevelYAxes));

                LastUpdated = $"Updated {DateTime.Now:HH:mm}";
                loadDistributionAfter = true;
            }

            ct.ThrowIfCancellationRequested();

            IReadOnlyList<DashboardTrendPointDto> trendPoints = stats is { Trend.Count: > 0 }
                ? stats.Trend
                : trendOk ? apiTrend : [];
            var points = trendPoints.Select(t => t.Value).ToArray();
            if (points.Length == 0)
            {
                points = [0d];
            }

            var trendAnim = animateCharts ? TimeSpan.FromMilliseconds(400) : TimeSpan.Zero;
            var avgAnim = animateCharts ? TimeSpan.FromMilliseconds(450) : TimeSpan.Zero;

            TrendSeries =
            [
                new LineSeries<double>
                {
                    Values          = points,
                    GeometrySize    = 7,
                    GeometryStroke  = new SolidColorPaint(new SKColor(31, 122, 224), 2.5f),
                    GeometryFill    = new SolidColorPaint(DashboardChartAppearance.LinePointFill(!_themeService.IsDark)),
                    LineSmoothness  = 0.7,
                    Fill            = new LinearGradientPaint(
                                          new SKColor(31, 122, 224, 80),
                                          new SKColor(31, 122, 224, 0),
                                          new SKPoint(0.5f, 0f),
                                          new SKPoint(0.5f, 1f)),
                    Stroke          = new SolidColorPaint(new SKColor(31, 122, 224), 3f),
                    AnimationsSpeed = trendAnim,
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
                    AnimationsSpeed = avgAnim,
                }
            ];

            TrendXLabels = trendPoints.Select(t => t.Label).ToArray();
            TrendXAxes = DashboardChartAppearance.BuildTrendXAxes(TrendXLabels, _themeService.IsDark);
            OnPropertyChanged(nameof(TrendSeries));
            OnPropertyChanged(nameof(TrendXLabels));
            OnPropertyChanged(nameof(TrendXAxes));
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] LoadDashboardCoreAsync failed: {ex.Message}");
        }
        finally
        {
            if (generation == _loadGeneration && showLoadingIndicator)
            {
                IsLoading = false;
            }
        }

        if (generation != _loadGeneration)
        {
            return;
        }

        _lastLoadUtc = DateTimeOffset.UtcNow;
        _hasCompletedInitialLoad = true;

        if (loadDistributionAfter)
        {
            _ = BuildDistributionAsync(ct);
        }

        StartAutoRefresh();
    }

    // ── Distribution ──────────────────────────────────────────────────────────
    private async Task BuildDistributionAsync(CancellationToken cancellationToken = default)
    {
        _distributionCts?.Cancel();
        _distributionCts?.Dispose();
        _distributionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _distributionCts.Token;

        DistributionItems.Clear();

        var sliceStroke = _themeService.IsDark
            ? new SKColor(10, 14, 21)
            : new SKColor(255, 255, 255);

        try
        {
            ct.ThrowIfCancellationRequested();

            var templates = (await _templateSchema.GetTemplatesAsync(ct).ConfigureAwait(true)).ToList();
            ct.ThrowIfCancellationRequested();

            if (templates.Count == 0)
            {
                DistributionSeries = BuildEmptyDistributionSeries();
                OnPropertyChanged(nameof(DistributionSeries));
                return;
            }

            var counts = await Task.WhenAll(
                templates.Select(t => _masterData.GetRowCountAsync(t.Id, ct))).ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            var slices = templates
                .Select((template, index) => (Name: template.Name, Count: counts[index]))
                .Where(slice => slice.Count > 0)
                .OrderByDescending(slice => slice.Count)
                .Take(5)
                .ToList();

            if (slices.Count == 0)
            {
                DistributionSeries = BuildEmptyDistributionSeries();
                OnPropertyChanged(nameof(DistributionSeries));
                return;
            }

            var totalParts = Math.Max(1, slices.Sum(s => s.Count));
            var series = new ISeries[slices.Count];

            for (var i = 0; i < slices.Count; i++)
            {
                var (name, count) = slices[i];
                var pct = (int)Math.Round(100.0 * count / totalParts);
                var hex = ResolveDistributionColorHex(name, i);
                var color = ParseSkColor(hex);

                series[i] = new PieSeries<int>
                {
                    Values = [count],
                    Name = name,
                    Fill = new SolidColorPaint(color),
                    Stroke = new SolidColorPaint(sliceStroke) { StrokeThickness = 2f },
                    InnerRadius = 45,
                    Pushout = 2,
                    AnimationsSpeed = TimeSpan.FromMilliseconds(900 + i * 120),
                };
                DistributionItems.Add(new DistributionItem(name, pct, hex));
            }

            DistributionSeries = series;
            OnPropertyChanged(nameof(DistributionSeries));
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer dashboard load.
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] BuildDistributionAsync failed: {ex.Message}");
            DistributionSeries = BuildEmptyDistributionSeries();
            OnPropertyChanged(nameof(DistributionSeries));
        }
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

    private static ISeries[] BuildStockLevelSeries(IReadOnlyList<LowStockItem> items, bool isLight)
    {
        int[] values = items.Count == 0
            ? [0]
            : items.Select(item => item.Current).ToArray();
        var labelColor = isLight ? new SKColor(33, 47, 66) : new SKColor(234, 242, 255);
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
                DataLabelsPaint = new SolidColorPaint(labelColor),
                DataLabelsSize  = 11,
                DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.End,
                AnimationsSpeed = TimeSpan.FromMilliseconds(1000),
            }
        ];
    }

    /// <summary>Template distribution: vendor = yellow, partfinder.parts = green, customers = blue.</summary>
    private static string ResolveDistributionColorHex(string templateName, int fallbackIndex)
    {
        var name = templateName.Trim();
        if (name.Contains("vendor", StringComparison.OrdinalIgnoreCase))
        {
            return "#FEBC2E";
        }

        if (name.Contains("partfinder.parts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "parts", StringComparison.OrdinalIgnoreCase))
        {
            return "#2ABD8F";
        }

        if (name.Contains("customer", StringComparison.OrdinalIgnoreCase))
        {
            return "#1F7AE0";
        }

        var fallback = new[] { "#1F7AE0", "#2ABD8F", "#FEBC2E", "#8B5CF6", "#FF6B6B" };
        return fallback[fallbackIndex % fallback.Length];
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

    private ISeries[] BuildHealthGaugeSeries(double rate)
    {
        var filled = Math.Min(100, Math.Max(0, rate));
        var fillColor = filled >= 90
            ? new SKColor(42, 189, 143)
            : filled >= 70
                ? new SKColor(254, 188, 46)
                : new SKColor(255, 100, 100);
        var trackColor = DashboardChartAppearance.GaugeTrackColor(_themeService.IsDark);
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
                Fill               = new SolidColorPaint(trackColor),
                InnerRadius        = 58,
                MaxRadialColumnWidth = 20,
                AnimationsSpeed    = TimeSpan.FromMilliseconds(1400),
            },
        ];
    }

    private async Task<WorkspaceDashboardStats?> BuildWorkspaceStatsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var templatesTask = _templateSchema.GetTemplatesAsync(cancellationToken);
            var inventoryTask = _inventory.GetAllAsync(cancellationToken);
            var relationsTask = _api.GetRelationsAsync(cancellationToken);
            var auditTask = _audit.GetRecentAsync(40, null, cancellationToken);
            await Task.WhenAll(templatesTask, inventoryTask, relationsTask, auditTask).ConfigureAwait(true);

            var templates = (await templatesTask.ConfigureAwait(true)).ToList();
            var inventory = await inventoryTask.ConfigureAwait(true);
            var (relationsOk, _, relations) = await relationsTask.ConfigureAwait(true);
            var audits = await auditTask.ConfigureAwait(true);
            var relationList = relationsOk ? relations : Array.Empty<WorksheetRelationDto>();

            var linkedTemplateIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var relation in relationList)
            {
                if (!string.IsNullOrWhiteSpace(relation.PrimaryTemplateId))
                {
                    linkedTemplateIds.Add(relation.PrimaryTemplateId);
                }

                if (!string.IsNullOrWhiteSpace(relation.LookupTemplateId))
                {
                    linkedTemplateIds.Add(relation.LookupTemplateId);
                }
            }

            var counts = templates.Count == 0
                ? Array.Empty<int>()
                : await Task.WhenAll(templates.Select(t => _masterData.GetRowCountAsync(t.Id, cancellationToken)))
                    .ConfigureAwait(true);
            var masterDataRows = counts.Sum();

            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < templates.Count; i++)
            {
                if (counts[i] > 0 || linkedTemplateIds.Contains(templates[i].Id))
                {
                    activeIds.Add(templates[i].Id);
                }
            }

            var activeTemplates = activeIds.Count > 0 ? activeIds.Count : templates.Count;

            int totalParts;
            int belowMinimum;
            int outOfStock;
            List<LowStockItem> lowItems;
            string partsSource;

            if (inventory.Count > 0)
            {
                partsSource = "inventory";
                totalParts = inventory.Count;
                (lowItems, belowMinimum, outOfStock) = CollectInventoryStock(inventory);
            }
            else
            {
                partsSource = "master data";
                var partsRows = 0;
                for (var i = 0; i < templates.Count; i++)
                {
                    if (IsPartsTemplate(templates[i]) || templates[i].Fields.Any(IsQuantityField))
                    {
                        partsRows += counts[i];
                    }
                }

                totalParts = partsRows > 0 ? partsRows : masterDataRows;
                (lowItems, belowMinimum, outOfStock) = await ScanMasterDataStockAsync(templates, cancellationToken)
                    .ConfigureAwait(true);
            }

            lowItems = lowItems
                .OrderBy(item => item.Current)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            var (importRate, hasImports) = ComputeImportSuccess(audits);

            return new WorkspaceDashboardStats
            {
                TotalParts = totalParts,
                LowStock = belowMinimum + outOfStock,
                BelowMinimum = belowMinimum,
                OutOfStock = outOfStock,
                ActiveTemplates = activeTemplates,
                LinkedTemplateCount = relationList.Count,
                MasterDataRows = masterDataRows,
                ImportSuccessRate = importRate,
                HasImportHistory = hasImports,
                PartsSource = partsSource,
                RecentActivity = BuildActivityLines(audits, totalParts, masterDataRows, relationList.Count, partsSource),
                LowStockItems = lowItems,
                Trend = BuildInventoryTrend(inventory),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Dashboard] BuildWorkspaceStatsAsync failed: {ex.Message}");
            return null;
        }
    }

    private static (List<LowStockItem> Items, int BelowMinimum, int OutOfStock) CollectInventoryStock(
        IReadOnlyList<InventoryDoc> inventory)
    {
        var items = new List<LowStockItem>();
        var belowMinimum = 0;
        var outOfStock = 0;

        foreach (var doc in inventory)
        {
            var minimum = doc.MinStock > 0 ? doc.MinStock : 10;
            var quantity = doc.Quantity;
            if (quantity >= minimum)
            {
                continue;
            }

            if (quantity == 0)
            {
                outOfStock++;
            }
            else
            {
                belowMinimum++;
            }

            var name = string.IsNullOrWhiteSpace(doc.PartName) ? doc.PartId : doc.PartName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Unnamed part";
            }

            items.Add(new LowStockItem(name.Trim(), quantity, minimum));
        }

        return (items, belowMinimum, outOfStock);
    }

    private async Task<(List<LowStockItem> Items, int BelowMinimum, int OutOfStock)> ScanMasterDataStockAsync(
        IReadOnlyList<PartTemplateDefinition> templates,
        CancellationToken cancellationToken)
    {
        var items = new List<LowStockItem>();
        var belowMinimum = 0;
        var outOfStock = 0;

        foreach (var template in templates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quantityField = template.Fields.FirstOrDefault(IsQuantityField);
            if (quantityField is null)
            {
                continue;
            }

            var minimumField = template.Fields.FirstOrDefault(IsMinimumField);
            var nameField = template.Fields.FirstOrDefault(IsNameField);
            var rows = await _masterData.GetRowsAsync(template.Id, cancellationToken).ConfigureAwait(true);

            foreach (var row in rows)
            {
                if (!TryReadNumber(row.Values, quantityField, out var quantityValue))
                {
                    continue;
                }

                var quantity = (int)Math.Round(quantityValue);
                double minimumValue = 0;
                var hasMinimum = minimumField is not null
                    && TryReadNumber(row.Values, minimumField, out minimumValue)
                    && minimumValue > 0;
                if (!hasMinimum)
                {
                    if (quantity > 0)
                    {
                        continue;
                    }

                    outOfStock++;
                    items.Add(new LowStockItem(ReadRowName(row, nameField, template.Name), 0, 1));
                    continue;
                }

                var minimum = (int)Math.Round(minimumValue);
                if (quantity >= minimum)
                {
                    continue;
                }

                if (quantity <= 0)
                {
                    outOfStock++;
                }
                else
                {
                    belowMinimum++;
                }

                items.Add(new LowStockItem(
                    ReadRowName(row, nameField, template.Name),
                    Math.Max(0, quantity),
                    minimum));
            }
        }

        return (items, belowMinimum, outOfStock);
    }

    private static List<string> BuildActivityLines(
        IReadOnlyList<AuditDoc> audits,
        int totalParts,
        int masterDataRows,
        int linkedCount,
        string partsSource)
    {
        var lines = new List<string>();
        foreach (var doc in audits.Take(8))
        {
            var action = string.IsNullOrWhiteSpace(doc.Action) ? doc.EventType : doc.Action;
            var details = doc.Details?.Trim();
            var line = string.IsNullOrWhiteSpace(details) ? action : $"{action}: {details}";
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line.Trim());
            }
        }

        if (lines.Count == 0)
        {
            if (totalParts > 0)
            {
                lines.Add($"{totalParts} parts tracked in {partsSource}");
            }

            if (masterDataRows > 0)
            {
                lines.Add($"{masterDataRows} master data rows");
            }

            if (linkedCount > 0)
            {
                lines.Add($"{linkedCount} linked template relations");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("No recent activity");
        }

        return lines;
    }

    private static (double Rate, bool HasHistory) ComputeImportSuccess(IReadOnlyList<AuditDoc> audits)
    {
        var imports = audits.Where(doc =>
            ContainsText(doc, "import")).ToList();
        if (imports.Count == 0)
        {
            return (0, false);
        }

        var failed = imports.Count(doc => ContainsText(doc, "fail") || ContainsText(doc, "error"));
        var rate = 100.0 * (imports.Count - failed) / imports.Count;
        return (rate, true);
    }

    private static bool ContainsText(AuditDoc doc, string term)
    {
        var text = $"{doc.EventType} {doc.Action} {doc.Details}";
        return text.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DashboardTrendPointDto> BuildInventoryTrend(IReadOnlyList<InventoryDoc> inventory)
    {
        if (inventory.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var buckets = new int[6];
        foreach (var doc in inventory)
        {
            var stamp = doc.LastUpdated == default ? now : doc.LastUpdated.ToUniversalTime();
            var index = ((stamp.Year - start.Year) * 12) + stamp.Month - start.Month;
            if (index is >= 0 and < 6)
            {
                buckets[index]++;
            }
        }

        if (buckets.Sum() == 0)
        {
            buckets[5] = inventory.Count;
        }

        return Enumerable.Range(0, 6)
            .Select(index => new DashboardTrendPointDto
            {
                Label = start.AddMonths(index).ToString("MMM", CultureInfo.InvariantCulture),
                Value = buckets[index],
            })
            .ToList();
    }

    private static bool IsPartsTemplate(PartTemplateDefinition template) =>
        MongoSharedPartsCatalog.IsCatalogTemplateName(template.Name)
        || template.Name.Contains("part", StringComparison.OrdinalIgnoreCase);

    private static bool IsQuantityField(TemplateFieldDefinition field) =>
        MatchesField(field, QuantityFieldKeys);

    private static bool IsMinimumField(TemplateFieldDefinition field) =>
        MatchesField(field, MinimumFieldKeys);

    private static bool IsNameField(TemplateFieldDefinition field) =>
        MatchesField(field, NameFieldKeys);

    private static readonly string[] QuantityFieldKeys =
        ["quantity", "qty", "stock", "stocklevel", "onhand", "onhandqty", "availableqty"];

    private static readonly string[] MinimumFieldKeys =
        ["minstock", "minimumstock", "reorderlevel", "reorderqty", "threshold", "minqty", "minimumqty", "safetystock"];

    private static readonly string[] NameFieldKeys =
        ["partname", "itemname", "name", "description", "title"];

    private static bool MatchesField(TemplateFieldDefinition field, string[] keys)
    {
        var key = NormalizeFieldName(field.Key);
        var label = NormalizeFieldName(field.Label);
        return keys.Contains(key) || keys.Contains(label);
    }

    private static string NormalizeFieldName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private static bool TryReadNumber(
        IReadOnlyDictionary<string, string> values,
        TemplateFieldDefinition field,
        out double number)
    {
        number = 0;
        if (!values.TryGetValue(field.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var cleaned = raw.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
            || double.TryParse(cleaned, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
    }

    private static string ReadRowName(
        MasterDataRowRecord row,
        TemplateFieldDefinition? nameField,
        string fallback)
    {
        if (nameField is not null
            && row.Values.TryGetValue(nameField.Key, out var name)
            && !string.IsNullOrWhiteSpace(name))
        {
            return name.Trim();
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Unnamed part" : fallback.Trim();
    }

    private sealed class WorkspaceDashboardStats
    {
        public int TotalParts { get; init; }
        public int LowStock { get; init; }
        public int BelowMinimum { get; init; }
        public int OutOfStock { get; init; }
        public int ActiveTemplates { get; init; }
        public int LinkedTemplateCount { get; init; }
        public int MasterDataRows { get; init; }
        public double ImportSuccessRate { get; init; }
        public bool HasImportHistory { get; init; }
        public string PartsSource { get; init; } = "inventory";
        public List<string> RecentActivity { get; init; } = [];
        public List<LowStockItem> LowStockItems { get; init; } = [];
        public IReadOnlyList<DashboardTrendPointDto> Trend { get; init; } = [];
    }

    private void RefreshChartAppearance()
    {
        var appearance = DashboardChartAppearance.ForLightMode(_themeService.IsDark);
        ChartDrawMarginFrame = appearance.DrawMarginFrame;
        ChartXAxes = appearance.XAxes;
        ChartYAxes = appearance.YAxes;
        if (TrendXLabels is { Length: > 0 })
        {
            TrendXAxes = DashboardChartAppearance.BuildTrendXAxes(TrendXLabels, _themeService.IsDark);
            OnPropertyChanged(nameof(TrendXAxes));
        }

        StockLevelYAxes = DashboardChartAppearance.BuildStockLevelYAxes(StockLevelYLabels, _themeService.IsDark);
        OnPropertyChanged(nameof(StockLevelYAxes));

        OnPropertyChanged(nameof(ChartDrawMarginFrame));
        OnPropertyChanged(nameof(ChartXAxes));
        OnPropertyChanged(nameof(ChartYAxes));
    }

    private void OnAppThemeChanged()
    {
        RefreshChartAppearance();
        HealthGaugeSeries = BuildHealthGaugeSeries(_lastImportSuccessRate);
        OnPropertyChanged(nameof(HealthGaugeSeries));
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
                    {
                        _ = _uiQueue.TryEnqueue(() => _ = LoadDashboardCoreAsync(showLoadingIndicator: false));
                    }
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

    public string PercentDisplay => $"{Percentage}%";

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
