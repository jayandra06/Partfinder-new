using LiveChartsCore.Drawing;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace PartFinder.Helpers;

internal static class DashboardChartAppearance
{
    public sealed record ChartAppearance(
        DrawMarginFrame DrawMarginFrame,
        Axis[] XAxes,
        Axis[] YAxes);

    public static ChartAppearance ForLightMode(bool isDark)
    {
        var surface = isDark ? new SKColor(20, 29, 42) : new SKColor(255, 255, 255);
        var border = isDark ? new SKColor(40, 55, 75) : new SKColor(230, 236, 245);
        var grid = isDark ? new SKColor(80, 100, 130, 120) : new SKColor(200, 210, 225, 200);
        var labels = isDark ? new SKColor(196, 208, 223) : new SKColor(33, 47, 66);

        var frame = new DrawMarginFrame
        {
            Fill = new SolidColorPaint(surface),
            Stroke = new SolidColorPaint(border) { StrokeThickness = 1f },
        };

        var axisPaint = new SolidColorPaint(labels);
        var gridPaint = new SolidColorPaint(grid) { StrokeThickness = 1f };

        var xAxes = new[]
        {
            new Axis
            {
                LabelsPaint = axisPaint,
                SeparatorsPaint = gridPaint,
                TextSize = 11,
                ShowSeparatorLines = true,
                SubseparatorsCount = 4,
            },
        };

        var yAxes = new[]
        {
            new Axis
            {
                LabelsPaint = axisPaint,
                SeparatorsPaint = gridPaint,
                TextSize = 11,
                ShowSeparatorLines = true,
                SubseparatorsCount = 4,
            },
        };

        return new ChartAppearance(frame, xAxes, yAxes);
    }

    public static Axis[] BuildTrendXAxes(string[] labels, bool isDark)
    {
        var appearance = ForLightMode(isDark);
        var axisPaint = appearance.XAxes[0].LabelsPaint;
        var gridPaint = appearance.XAxes[0].SeparatorsPaint;
        var rotation = labels.Length > 8 ? 35 : 0;

        return
        [
            new Axis
            {
                Labels = labels,
                LabelsRotation = rotation,
                TextSize = 11,
                LabelsPaint = axisPaint,
                SeparatorsPaint = gridPaint,
                ShowSeparatorLines = true,
                SubseparatorsCount = 4,
            },
        ];
    }

    public static Axis[] BuildStockLevelYAxes(string[] labels, bool isDark)
    {
        var appearance = ForLightMode(isDark);
        var axisPaint = appearance.YAxes[0].LabelsPaint;
        var gridPaint = appearance.YAxes[0].SeparatorsPaint;
        var displayLabels = labels is { Length: > 0 } ? labels : ["—"];

        return
        [
            new Axis
            {
                Labels = displayLabels,
                LabelsPaint = axisPaint,
                SeparatorsPaint = gridPaint,
                TextSize = 11,
                ShowSeparatorLines = true,
                SubseparatorsCount = 4,
            },
        ];
    }

    public static SKColor GaugeTrackColor(bool isDark) =>
        isDark ? new SKColor(26, 39, 52) : new SKColor(230, 236, 245);

    public static SKColor LinePointFill(bool isDark) =>
        isDark ? new SKColor(10, 14, 21) : new SKColor(255, 255, 255);
}
