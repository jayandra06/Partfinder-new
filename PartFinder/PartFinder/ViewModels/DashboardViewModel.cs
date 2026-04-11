using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    public DashboardViewModel()
    {
        Kpis =
        [
            new KpiItem("Total Parts", "1,250,430", "+2.1%"),
            new KpiItem("Low Stock", "2,480", "-3.3%"),
            new KpiItem("Active Templates", "18", "+1"),
            new KpiItem("Import Success", "99.4%", "+0.2%")
        ];

        RecentActivity =
        [
            "Template B updated by Operations Team",
            "98,230 rows imported from Supplier Feed",
            "Low stock alert acknowledged for 141 SKUs"
        ];

        TrendSeries = [];
        TrendXLabels = [];
        LazyLoadTrendCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(LoadTrendAsync);
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

    private bool _trendLoaded;

    private async Task LoadTrendAsync()
    {
        if (_trendLoaded || IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            await Task.Delay(180);

            // Keep series bounded to reduce chart render cost.
            var points = Enumerable.Range(1, 120)
                .Select(i => 900d + Math.Sin(i / 8d) * 70d + (i % 9) * 3d)
                .ToArray();

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

            TrendXLabels = Enumerable.Range(1, points.Length)
                .Where(i => i % 12 == 0)
                .Select(i => $"M{i / 12}")
                .ToArray();

            OnPropertyChanged(nameof(TrendSeries));
            OnPropertyChanged(nameof(TrendXLabels));
            _trendLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public sealed record KpiItem(string Label, string Value, string Delta);
