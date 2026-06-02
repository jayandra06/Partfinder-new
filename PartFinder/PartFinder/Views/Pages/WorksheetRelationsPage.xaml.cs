using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PartFinder.Services;
using PartFinder.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Microsoft.UI.Xaml.Controls.Primitives;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace PartFinder.Views.Pages;

public sealed partial class WorksheetRelationsPage : Page
{
    private WorksheetRelationsViewModel? _vm;

    // Connection drawing state
    private bool _isDragging;
    private int _dragSourceIndex = -1;
    private bool _dragFromPrimary;
    private Point _dragStartPoint;
    private Path? _previewLine;

    // Stored connections (visual)
    private readonly List<DrawnConnection> _drawnConnections = [];

    public WorksheetRelationsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WorksheetRelationsViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var access = App.Services.GetRequiredService<ICurrentUserAccessService>();
        await access.RefreshAsync().ConfigureAwait(true);
        if (!access.Capabilities.CanViewTemplate)
        {
            NoPermissionOverlay.Message = "You don't have permission to view template links.";
            NoPermissionOverlay.Visibility = Visibility.Visible;
            return;
        }

        PageEntranceStoryboard.Begin();

        _vm = DataContext as WorksheetRelationsViewModel;
        if (_vm is null) return;

        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(WorksheetRelationsViewModel.PrimaryTemplate)
                or nameof(WorksheetRelationsViewModel.LookupTemplate)
                or nameof(WorksheetRelationsViewModel.HasNoPrimaryColumns)
                or nameof(WorksheetRelationsViewModel.HasNoLookupColumns))
            {
                RebuildColumnLists();
            }
        };

        await _vm.InitializeAsync();
        RebuildColumnLists();
    }

    private void RebuildColumnLists()
    {
        if (_vm is null) return;

        BuildColumnPanel(PrimaryColumnList, _vm.PrimaryColumns.ToList(), isPrimary: true);
        BuildColumnPanel(LookupColumnList, _vm.LookupColumns.ToList(), isPrimary: false);

        // Restore existing connections from ViewModel checked state
        _drawnConnections.Clear();
        ConnectionCanvas.Children.Clear();
        RestoreConnectionsFromViewModel();
    }

    private void BuildColumnPanel(StackPanel panel, List<RelationColumnPickItem> columns, bool isPrimary)
    {
        panel.Children.Clear();

        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var index = i;

            var row = new Border
            {
                BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 12, 10, 12),
                Tag = index,
            };

            var grid = new Grid();
            // Primary: [Label*] [Port-14px]   → dot on right, close to border
            // Lookup:  [Port-14px] [Label*]   → dot on left, close to border
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = isPrimary ? new GridLength(1, GridUnitType.Star) : new GridLength(14) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = isPrimary ? new GridLength(14) : new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = col.ColumnName,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = isPrimary ? new Thickness(0) : new Thickness(10, 0, 0, 0),
            };
            Grid.SetColumn(label, isPrimary ? 0 : 1);
            grid.Children.Add(label);

            // Connection port (dot) - Primary on RIGHT side, Lookup on LEFT side
            var port = new Ellipse
            {
                Width = 12, Height = 12,
                Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 31, 122, 224)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.7,
                Tag = index,
            };
            Grid.SetColumn(port, isPrimary ? 1 : 0);
            grid.Children.Add(port);

            // Port hover effects
            port.PointerEntered += (s, _) => { if (s is Ellipse e) { e.Opacity = 1; e.Width = 14; e.Height = 14; } };
            port.PointerExited += (s, _) => { if (s is Ellipse e) { e.Opacity = 0.7; e.Width = 12; e.Height = 12; } };

            // Drag start from port
            port.PointerPressed += (s, args) =>
            {
                _isDragging = true;
                _dragSourceIndex = index;
                _dragFromPrimary = isPrimary;
                _dragStartPoint = GetPortCenter(row, isPrimary);
                args.Handled = true;
                ConnectionCanvas.CapturePointer(args.Pointer);
            };

            row.Child = grid;
            panel.Children.Add(row);
        }
    }

    private void OnConnectionCanvasPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetCurrentPoint(ConnectionCanvas).Position;

        // Draw/update preview line
        if (_previewLine is not null)
            ConnectionCanvas.Children.Remove(_previewLine);

        _previewLine = CreateBezierPath(_dragStartPoint, currentPoint,
            new SolidColorBrush(ColorHelper.FromArgb(120, 31, 122, 224)), 2, true);
        ConnectionCanvas.Children.Add(_previewLine);
    }

    private void OnConnectionCanvasPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ConnectionCanvas.ReleasePointerCapture(e.Pointer);

        // Remove preview line
        if (_previewLine is not null)
        {
            ConnectionCanvas.Children.Remove(_previewLine);
            _previewLine = null;
        }

        // Find target port
        var releasePoint = e.GetCurrentPoint(ConnectionCanvas).Position;
        var (targetIndex, targetIsPrimary) = FindPortAtPoint(releasePoint);

        if (targetIndex < 0) return;
        if (_dragFromPrimary == targetIsPrimary) return; // Can't connect same side

        // Determine which is primary, which is lookup
        int primaryIdx = _dragFromPrimary ? _dragSourceIndex : targetIndex;
        int lookupIdx = _dragFromPrimary ? targetIndex : _dragSourceIndex;

        // Check if connection already exists
        if (_drawnConnections.Any(c => c.PrimaryIndex == primaryIdx && c.LookupIndex == lookupIdx))
            return;

        // Create connection
        AddConnection(primaryIdx, lookupIdx);
        SyncToViewModel();
    }

    private void OnConnectionCanvasRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var point = e.GetPosition(ConnectionCanvas);
        var hit = FindConnectionAtPoint(point);
        if (hit is null) return;

        var flyout = new MenuFlyout();

        var infoItem = new MenuFlyoutItem
        {
            Text = $"{hit.PrimaryName} → {hit.LookupName}",
            IsEnabled = false,
        };
        flyout.Items.Add(infoItem);
        flyout.Items.Add(new MenuFlyoutSeparator());

        var deleteItem = new MenuFlyoutItem { Text = "Delete Connection", Icon = new FontIcon { Glyph = "\uE74D" } };
        deleteItem.Click += (_, _) =>
        {
            _drawnConnections.Remove(hit);
            RedrawAllConnections();
            SyncToViewModel();
        };
        flyout.Items.Add(deleteItem);

        flyout.ShowAt(ConnectionCanvas, new FlyoutShowOptions { Position = point });
    }

    private void AddConnection(int primaryIdx, int lookupIdx)
    {
        if (_vm is null) return;

        var primaryName = primaryIdx < _vm.PrimaryColumns.Count ? _vm.PrimaryColumns[primaryIdx].ColumnName : "?";
        var lookupName = lookupIdx < _vm.LookupColumns.Count ? _vm.LookupColumns[lookupIdx].ColumnName : "?";

        _drawnConnections.Add(new DrawnConnection
        {
            PrimaryIndex = primaryIdx,
            LookupIndex = lookupIdx,
            PrimaryName = primaryName,
            LookupName = lookupName,
        });

        RedrawAllConnections();
    }

    private void RedrawAllConnections()
    {
        // Remove all lines (keep preview if active)
        var toRemove = ConnectionCanvas.Children.OfType<Path>().Where(p => p != _previewLine).ToList();
        foreach (var p in toRemove) ConnectionCanvas.Children.Remove(p);

        for (int i = 0; i < _drawnConnections.Count; i++)
        {
            var conn = _drawnConnections[i];
            var startPoint = GetPortCenterByIndex(PrimaryColumnList, conn.PrimaryIndex, isPrimary: true);
            var endPoint = GetPortCenterByIndex(LookupColumnList, conn.LookupIndex, isPrimary: false);

            if (startPoint == default || endPoint == default) continue;

            // Stagger vertically so lines don't overlap
            var stagger = (i - _drawnConnections.Count / 2.0) * 8;
            startPoint = new Point(startPoint.X, startPoint.Y + stagger);
            endPoint = new Point(endPoint.X, endPoint.Y + stagger);

            var line = CreateBezierPath(startPoint, endPoint,
                new SolidColorBrush(ColorHelper.FromArgb(200, 31, 122, 224)), 2.5, false);
            line.Tag = conn;
            ConnectionCanvas.Children.Add(line);
        }
    }

    private Path CreateBezierPath(Point start, Point end, Brush stroke, double thickness, bool isDashed)
    {
        // Bezier curve (smooth S-curve between panels)
        var controlOffset = Math.Abs(end.X - start.X) * 0.5;
        var cp1 = new Point(start.X + controlOffset, start.Y);
        var cp2 = new Point(end.X - controlOffset, end.Y);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new BezierSegment { Point1 = cp1, Point2 = cp2, Point3 = end });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        var path = new Path
        {
            Stroke = stroke,
            StrokeThickness = thickness,
            Data = geometry,
            IsHitTestVisible = !isDashed,
        };

        if (isDashed)
        {
            path.StrokeDashArray = [4, 3];
        }

        return path;
    }

    private Point GetPortCenter(Border row, bool isPrimary)
    {
        try
        {
            var transform = row.TransformToVisual(ConnectionCanvas);
            var topLeft = transform.TransformPoint(new Point(0, 0));
            var y = topLeft.Y + row.ActualHeight / 2;
            var x = isPrimary ? 0 : ConnectionCanvas.ActualWidth;
            return new Point(x, y);
        }
        catch { return default; }
    }

    private Point GetPortCenterByIndex(StackPanel panel, int index, bool isPrimary)
    {
        if (index < 0 || index >= panel.Children.Count) return default;
        if (panel.Children[index] is not Border row) return default;
        return GetPortCenter(row, isPrimary);
    }

    private (int index, bool isPrimary) FindPortAtPoint(Point canvasPoint)
    {
        // Check lookup panel (right side)
        var result = FindPortInPanel(LookupColumnList, canvasPoint, isPrimary: false);
        if (result >= 0) return (result, false);

        // Check primary panel (left side)
        result = FindPortInPanel(PrimaryColumnList, canvasPoint, isPrimary: true);
        if (result >= 0) return (result, true);

        return (-1, false);
    }

    private int FindPortInPanel(StackPanel panel, Point canvasPoint, bool isPrimary)
    {
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is not Border row) continue;
            try
            {
                var transform = row.TransformToVisual(ConnectionCanvas);
                var topLeft = transform.TransformPoint(new Point(0, 0));
                var rect = new Rect(topLeft.X, topLeft.Y, row.ActualWidth, row.ActualHeight);

                // Expand hit area towards the canvas center
                var expandedRect = isPrimary
                    ? new Rect(rect.Right - 30, rect.Top, 60, rect.Height)
                    : new Rect(rect.Left - 30, rect.Top, 60, rect.Height);

                if (expandedRect.Contains(canvasPoint))
                    return i;
            }
            catch { }
        }
        return -1;
    }

    private DrawnConnection? FindConnectionAtPoint(Point point)
    {
        foreach (var child in ConnectionCanvas.Children.OfType<Path>())
        {
            if (child.Tag is not DrawnConnection conn) continue;
            if (child.Data is not PathGeometry geo) continue;

            // Simple proximity check along the curve
            var figure = geo.Figures.FirstOrDefault();
            if (figure is null) continue;

            var start = figure.StartPoint;
            var segment = figure.Segments.FirstOrDefault() as BezierSegment;
            if (segment is null) continue;

            // Sample points along the bezier and check distance
            for (double t = 0; t <= 1.0; t += 0.05)
            {
                var p = BezierPoint(start, segment.Point1, segment.Point2, segment.Point3, t);
                if (Math.Abs(p.X - point.X) < 12 && Math.Abs(p.Y - point.Y) < 12)
                    return conn;
            }
        }
        return null;
    }

    private static Point BezierPoint(Point p0, Point p1, Point p2, Point p3, double t)
    {
        var u = 1 - t;
        var x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
        var y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
        return new Point(x, y);
    }

    private void SyncToViewModel()
    {
        if (_vm is null) return;

        // Update ViewModel checkboxes based on drawn connections
        foreach (var col in _vm.PrimaryColumns) col.IsChecked = false;
        foreach (var col in _vm.LookupColumns) col.IsChecked = false;

        foreach (var conn in _drawnConnections)
        {
            if (conn.PrimaryIndex < _vm.PrimaryColumns.Count)
                _vm.PrimaryColumns[conn.PrimaryIndex].IsChecked = true;
            if (conn.LookupIndex < _vm.LookupColumns.Count)
                _vm.LookupColumns[conn.LookupIndex].IsChecked = true;
        }
    }

    private void RestoreConnectionsFromViewModel()
    {
        if (_vm is null) return;

        // Find matching checked pairs and create visual connections
        var checkedPrimary = _vm.PrimaryColumns
            .Select((col, idx) => (col, idx))
            .Where(x => x.col.IsChecked)
            .ToList();

        var checkedLookup = _vm.LookupColumns
            .Select((col, idx) => (col, idx))
            .Where(x => x.col.IsChecked)
            .ToList();

        // Connect matching column names (match keys)
        foreach (var (pCol, pIdx) in checkedPrimary)
        {
            // Find matching lookup by name
            var matchLookup = checkedLookup.FirstOrDefault(l =>
                string.Equals(l.col.ColumnName, pCol.ColumnName, StringComparison.OrdinalIgnoreCase));

            if (matchLookup.col is not null)
            {
                _drawnConnections.Add(new DrawnConnection
                {
                    PrimaryIndex = pIdx,
                    LookupIndex = matchLookup.idx,
                    PrimaryName = pCol.ColumnName,
                    LookupName = matchLookup.col.ColumnName,
                });
            }
        }

        // Delay redraw to ensure layout is complete
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            RedrawAllConnections();
        });
    }

    private sealed class DrawnConnection
    {
        public int PrimaryIndex { get; init; }
        public int LookupIndex { get; init; }
        public string PrimaryName { get; init; } = "";
        public string LookupName { get; init; } = "";
    }
}
