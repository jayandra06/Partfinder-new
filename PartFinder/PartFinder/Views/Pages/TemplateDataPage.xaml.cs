using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.Foundation;

namespace PartFinder.Views.Pages;

public sealed partial class TemplateDataPage : Page
{
    private const int ColumnFloorWidth = 72;
    private const int GridCellHorizontalPadding = 12;
    private const int CellBorderExtraWidth = 2;
    private const int MaxRowsForWidthScan = 500;
    private const double WidthSafetyMargin = 10;

    private readonly TemplateDataViewModel _viewModel;
    private readonly TextBlock _measureHeader = new() { FontSize = 12, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock _measureCell = new() { FontSize = 12, TextWrapping = TextWrapping.NoWrap };
    private string? _pendingTemplateId;

    public TemplateDataPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<TemplateDataViewModel>();
        DataContext = _viewModel;
        Loaded += (_, _) =>
        {
            if (!_viewModel.IsLoading && _viewModel.Columns.Count > 0)
            {
                RebuildGrid();
            }
        };
        DataScrollViewer.SizeChanged += (_, _) =>
        {
            if (!_viewModel.IsLoading && _viewModel.Columns.Count > 0)
            {
                RebuildGrid();
            }
        };
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(TemplateDataViewModel.Columns)
                or nameof(TemplateDataViewModel.Rows)
                or nameof(TemplateDataViewModel.IsLoading))
            {
                if (!_viewModel.IsLoading)
                {
                    RebuildGrid();
                }
            }
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var templateId = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        _pendingTemplateId = templateId;
        _ = _viewModel.LoadAsync(templateId);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (App.Services.GetRequiredService<INavigationService>().CanGoBack)
        {
            App.Services.GetRequiredService<INavigationService>().GoBack();
            return;
        }

        App.Services.GetRequiredService<INavigationService>().Navigate(AppPage.Templates);
    }

    private void OnEditDataClick(object sender, RoutedEventArgs e)
    {
        var templateId = _pendingTemplateId ?? _viewModel.TemplateId;
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        App.Services.GetRequiredService<INavigationService>().Navigate(
            AppPage.TemplateDataEdit,
            new MasterDataNavigationArgs
            {
                Mode = MasterDataSurfaceMode.TemplateEdit,
                TemplateId = templateId,
                StartInEditMode = true,
            });
    }

    private double GetAvailableTableWidth()
    {
        var width = DataScrollViewer.ViewportWidth;
        if (width <= 1 || double.IsNaN(width))
        {
            width = DataScrollViewer.ActualWidth;
        }

        return width > 1 ? width : 720;
    }

    private double MeasureTextWidth(string text, bool isHeader)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var probe = isHeader ? _measureHeader : _measureCell;
        probe.Text = text;
        probe.FontWeight = isHeader
            ? Microsoft.UI.Text.FontWeights.SemiBold
            : Microsoft.UI.Text.FontWeights.Normal;
        probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return probe.DesiredSize.Width;
    }

    private static IEnumerable<string> CellValuesForColumn(
        string key,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var scanCount = Math.Min(rows.Count, MaxRowsForWidthScan);
        for (var r = 0; r < scanCount; r++)
        {
            if (rows[r].TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                yield return value;
            }
        }
    }

    private double ComputeColumnWidth(
        TemplateFieldDefinition field,
        IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        var headerText = field.Label ?? field.Key ?? string.Empty;
        var maxContent = MeasureTextWidth(headerText, isHeader: true);

        foreach (var cell in CellValuesForColumn(field.Key, rows))
        {
            foreach (var line in cell.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                maxContent = Math.Max(maxContent, MeasureTextWidth(line.Trim(), isHeader: false));
            }

            maxContent = Math.Max(maxContent, MeasureTextWidth(cell.Trim(), isHeader: false));
        }

        var total = maxContent
                    + GridCellHorizontalPadding * 2
                    + CellBorderExtraWidth
                    + WidthSafetyMargin;
        return Math.Max(ColumnFloorWidth, Math.Ceiling(total));
    }

    private static TextBlock CreateSelectableText(
        string text,
        Brush foreground,
        bool isHeader,
        TextWrapping wrap,
        double maxInnerWidth)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = isHeader
                ? Microsoft.UI.Text.FontWeights.SemiBold
                : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = foreground,
            TextWrapping = wrap,
            MaxWidth = maxInnerWidth,
            IsTextSelectionEnabled = true,
            IsHitTestVisible = true,
        };
    }

    private void RebuildGrid()
    {
        if (_viewModel.Columns.Count == 0)
        {
            GridHost.Child = new TextBlock
            {
                Text = _viewModel.StatusMessage ?? "No columns defined.",
                Margin = new Thickness(20),
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                IsTextSelectionEnabled = true,
            };
            return;
        }

        var colCount = _viewModel.Columns.Count;
        var rowCount = _viewModel.Rows.Count;
        var columnWidths = _viewModel.Columns
            .Select(c => ComputeColumnWidth(c, _viewModel.Rows))
            .ToArray();
        var totalContentWidth = columnWidths.Sum();

        var availableWidth = GetAvailableTableWidth();
        var equalShare = availableWidth / Math.Max(1, colCount);
        var needsWiderThanShare = columnWidths.Any(w => w > equalShare + 1);
        var useHorizontalScroll = totalContentWidth > availableWidth + 1 || needsWiderThanShare;

        DataScrollViewer.HorizontalScrollMode = useHorizontalScroll ? ScrollMode.Enabled : ScrollMode.Disabled;
        DataScrollViewer.HorizontalScrollBarVisibility = useHorizontalScroll
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;
        DataScrollViewer.HorizontalContentAlignment = useHorizontalScroll
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Stretch;

        GridHost.HorizontalAlignment = useHorizontalScroll
            ? HorizontalAlignment.Left
            : HorizontalAlignment.Stretch;
        GridHost.MinWidth = useHorizontalScroll ? totalContentWidth : availableWidth;
        GridHost.MaxWidth = double.PositiveInfinity;

        var grid = new Grid
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
            HorizontalAlignment = useHorizontalScroll ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
        };

        if (useHorizontalScroll)
        {
            grid.Width = totalContentWidth;
            grid.MinWidth = totalContentWidth;
        }
        else
        {
            grid.MinWidth = availableWidth;
            grid.Width = availableWidth;
        }

        for (var c = 0; c < colCount; c++)
        {
            if (useHorizontalScroll)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(columnWidths[c], GridUnitType.Pixel),
                });
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                });
            }
        }

        for (var r = 0; r <= rowCount; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var headerBg = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"];
        var borderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"];
        var primary = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var secondary = (Brush)Application.Current.Resources["TextSecondaryBrush"];
        var wrap = useHorizontalScroll ? TextWrapping.NoWrap : TextWrapping.WrapWholeWords;

        for (var c = 0; c < colCount; c++)
        {
            var innerWidth = Math.Max(0, columnWidths[c] - GridCellHorizontalPadding * 2 - CellBorderExtraWidth);
            var field = _viewModel.Columns[c];
            var header = new Border
            {
                Background = headerBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(GridCellHorizontalPadding, 10, GridCellHorizontalPadding, 10),
                Width = useHorizontalScroll ? columnWidths[c] : double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = CreateSelectableText(
                    field.Label ?? string.Empty,
                    primary,
                    isHeader: true,
                    wrap,
                    innerWidth),
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }

        for (var r = 0; r < rowCount; r++)
        {
            var row = _viewModel.Rows[r];
            for (var c = 0; c < colCount; c++)
            {
                var innerWidth = Math.Max(0, columnWidths[c] - GridCellHorizontalPadding * 2 - CellBorderExtraWidth);
                var field = _viewModel.Columns[c];
                row.TryGetValue(field.Key, out var value);
                value ??= string.Empty;

                var cell = new Border
                {
                    Background = (Brush)Application.Current.Resources[
                        r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(GridCellHorizontalPadding, 8, GridCellHorizontalPadding, 8),
                    Width = useHorizontalScroll ? columnWidths[c] : double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Child = CreateSelectableText(
                        value,
                        secondary,
                        isHeader: false,
                        wrap,
                        innerWidth),
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        GridHost.Child = grid;
    }
}
