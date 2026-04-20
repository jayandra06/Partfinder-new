using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using PartFinder.Services;
using SkiaSharp;
using System.Collections.ObjectModel;
using System.Globalization;

namespace PartFinder.ViewModels;

public class DashboardViewModel : ViewModelBase
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
            new KpiItem("Total Parts", "-", string.Empty),
            new KpiItem("Low Stock", "-", string.Empty),
            new KpiItem("Active Templates", "-", string.Empty),
            new KpiItem("Import Success", "-", string.Empty)
        ];

        RecentActivity = [];

        TrendSeries = [];
        TrendXLabels = [];
        LazyLoadTrendCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadDashboardAsync);
    }

    public ObservableCollection<KpiItem> Kpis { get; }
    public ObservableCollection<string> RecentActivity { get; }
    public ISeries[] TrendSeries { get; private set; }
    public string[] TrendXLabels { get; private set; }
    public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand LazyLoadTrendCommand { get; }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private async Task LoadDashboardAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var (statsOk, _, stats) = await _api.GetDashboardStatsAsync().ConfigureAwait(true);
            var (trendOk, _, trend) = await _api.GetDashboardTrendAsync().ConfigureAwait(true);

            if (statsOk && stats is not null)
            {
                Kpis[0].Value = stats.TotalParts.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[1].Value = stats.LowStock.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[2].Value = stats.ActiveTemplates.ToString("N0", CultureInfo.InvariantCulture);
                Kpis[3].Value = $"{stats.ImportSuccessRate:0.0}%";

                RecentActivity.Clear();
                foreach (var line in stats.RecentActivity.Take(10))
                {
                    RecentActivity.Add(line);
                }
            }

            if (!trendOk)
            {
                return;
            }

            var points = trend.Select(t => t.Value).ToArray();
            if (points.Length == 0)
            {
                points = [0d];
            }

            TrendSeries =
            [
                new LineSeries<double>
                {
                    Values = points,
                    GeometrySize = 0,
                    LineSmoothness = 0.6,
                    Fill = null,
                    Stroke = new SolidColorPaint(new SKColor(59, 130, 246), 3)
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

    private void StartAutoRefresh()
    {
        if (_refreshTimer is not null)
        {
            return;
        }

        _refreshTimer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        _refreshCts = new CancellationTokenSource();
        _ = Task.Run(async () =>
        {
            try
            {
                while (_refreshTimer is not null &&
                       await _refreshTimer.WaitForNextTickAsync(_refreshCts.Token).ConfigureAwait(false))
                {
                    if (_uiQueue is not null)
                    {
                        _ = _uiQueue.TryEnqueue(async () => await LoadDashboardAsync().ConfigureAwait(true));
                    }
                }
            }
            catch
            {
            }
        });
    }
}
public sealed partial class KpiItem : ObservableObject
{
    public KpiItem(string label, string value, string delta)
    {
        Label = label;
        Value = value;
        Delta = delta;
    }

    public string Label { get; }

    [ObservableProperty]
    private string _value;

    [ObservableProperty]
    private string _delta;
}
