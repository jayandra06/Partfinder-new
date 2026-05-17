using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using PartFinder.Models;
using Windows.Foundation;

namespace PartFinder.Helpers;

/// <summary>
/// Renders connector lines between cells on the Template Canvas overlay.
/// Handles drawing, hit-testing, hover effects, and label positioning.
/// </summary>
public sealed class ConnectorRenderer
{
    private readonly Canvas _overlayCanvas;
    private readonly List<ConnectorVisual> _visuals = [];

    public ConnectorRenderer(Canvas overlayCanvas)
    {
        _overlayCanvas = overlayCanvas;
    }

    /// <summary>
    /// Redraws all connector lines based on current cell positions and connections.
    /// </summary>
    public void Redraw(IReadOnlyList<CellConnection> connections, IReadOnlyList<Rect> cellBounds)
    {
        Clear();

        foreach (var conn in connections)
        {
            if (conn.SourceCellIndex < 0 || conn.SourceCellIndex >= cellBounds.Count ||
                conn.TargetCellIndex < 0 || conn.TargetCellIndex >= cellBounds.Count)
                continue;

            var sourceRect = cellBounds[conn.SourceCellIndex];
            var targetRect = cellBounds[conn.TargetCellIndex];

            var sourcePoint = GetPortPoint(sourceRect, conn.SourceSide);
            var targetPoint = GetPortPoint(targetRect, conn.TargetSide);

            var visual = CreateConnectorVisual(conn, sourcePoint, targetPoint);
            _visuals.Add(visual);
        }
    }

    /// <summary>
    /// Draws a preview (dashed) line from a source port to the current pointer position.
    /// </summary>
    public void DrawPreviewLine(Point sourcePoint, Point currentPoint)
    {
        RemovePreviewLine();

        var line = new Line
        {
            X1 = sourcePoint.X,
            Y1 = sourcePoint.Y,
            X2 = currentPoint.X,
            Y2 = currentPoint.Y,
            Stroke = new SolidColorBrush(ColorHelper.FromArgb(200, 31, 122, 224)),
            StrokeThickness = 2,
            StrokeDashArray = [4, 3],
            Tag = "preview-line",
            IsHitTestVisible = false,
        };

        _overlayCanvas.Children.Add(line);
    }

    /// <summary>
    /// Removes the preview line from the overlay.
    /// </summary>
    public void RemovePreviewLine()
    {
        for (int i = _overlayCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (_overlayCanvas.Children[i] is FrameworkElement fe && Equals(fe.Tag, "preview-line"))
            {
                _overlayCanvas.Children.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Clears all rendered connector visuals from the overlay.
    /// </summary>
    public void Clear()
    {
        _overlayCanvas.Children.Clear();
        _visuals.Clear();
    }

    /// <summary>
    /// Hit-tests a point against rendered connector lines.
    /// Returns the CellConnection if a line is hit, null otherwise.
    /// </summary>
    public CellConnection? HitTest(Point point, double tolerance = 10.0)
    {
        foreach (var visual in _visuals)
        {
            // Check if point is near the label chip
            var labelLeft = Canvas.GetLeft(visual.LabelBorder);
            var labelTop = Canvas.GetTop(visual.LabelBorder);
            if (point.X >= labelLeft - 4 && point.X <= labelLeft + 60 &&
                point.Y >= labelTop - 4 && point.Y <= labelTop + 20)
            {
                return visual.Connection;
            }

            // Check if point is near the line path
            if (IsPointNearPath(point, visual.SourcePoint, visual.TargetPoint, visual.IsCurved, tolerance))
            {
                return visual.Connection;
            }
        }
        return null;
    }

    /// <summary>
    /// Highlights a connector line on hover.
    /// </summary>
    public void SetHover(CellConnection? connection)
    {
        foreach (var visual in _visuals)
        {
            if (visual.Connection == connection)
            {
                visual.Path.StrokeThickness = 3;
                visual.Path.Opacity = 1.0;
            }
            else
            {
                visual.Path.StrokeThickness = 2;
                visual.Path.Opacity = 0.85;
            }
        }
    }

    /// <summary>
    /// Gets the midpoint of a connection's rendered path for label positioning.
    /// </summary>
    public Point GetLabelPosition(CellConnection connection)
    {
        var visual = _visuals.FirstOrDefault(v => v.Connection == connection);
        if (visual is null) return new Point(0, 0);

        return new Point(
            (visual.SourcePoint.X + visual.TargetPoint.X) / 2,
            (visual.SourcePoint.Y + visual.TargetPoint.Y) / 2
        );
    }

    /// <summary>
    /// Calculates the port point (center of edge) for a given cell rect and side.
    /// </summary>
    public static Point GetPortPoint(Rect cellRect, ConnectionPortSide side)
    {
        return side switch
        {
            ConnectionPortSide.Top => new Point(cellRect.X + cellRect.Width / 2, cellRect.Y),
            ConnectionPortSide.Bottom => new Point(cellRect.X + cellRect.Width / 2, cellRect.Y + cellRect.Height),
            ConnectionPortSide.Left => new Point(cellRect.X, cellRect.Y + cellRect.Height / 2),
            ConnectionPortSide.Right => new Point(cellRect.X + cellRect.Width, cellRect.Y + cellRect.Height / 2),
            _ => new Point(cellRect.X, cellRect.Y),
        };
    }

    private ConnectorVisual CreateConnectorVisual(CellConnection conn, Point source, Point target)
    {
        var accentBrush = new SolidColorBrush(ColorHelper.FromArgb(204, 31, 122, 224)); // 80% opacity

        // Bracket-style lines: go UP from source, horizontal across, then DOWN to target
        // Like a bracket/bridge connecting cells from the top
        var heightOffset = 40.0; // How high the horizontal bar goes above the cells

        // For top-to-top connections, use bracket style
        // For other connections, use straight elbow lines
        Shape pathShape;
        Point labelPos;

        if (conn.SourceSide == ConnectionPortSide.Top && conn.TargetSide == ConnectionPortSide.Top)
        {
            // Bracket style: source up → horizontal → target down
            // Calculate dynamic height based on how far apart the cells are
            var distance = Math.Abs(target.X - source.X);
            heightOffset = Math.Max(30.0, Math.Min(60.0, distance * 0.15));

            var midY = Math.Min(source.Y, target.Y) - heightOffset;

            var pathFigure = new PathFigure { StartPoint = source, IsClosed = false };
            // Go up from source
            pathFigure.Segments.Add(new LineSegment { Point = new Point(source.X, midY) });
            // Go horizontal to target X
            pathFigure.Segments.Add(new LineSegment { Point = new Point(target.X, midY) });
            // Go down to target
            pathFigure.Segments.Add(new LineSegment { Point = new Point(target.X, target.Y) });

            var geometry = new PathGeometry();
            geometry.Figures.Add(pathFigure);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = accentBrush,
                StrokeThickness = 2,
                Opacity = 0.85,
                IsHitTestVisible = false,
                Data = geometry,
                StrokeLineJoin = PenLineJoin.Round,
            };
            pathShape = path;
            labelPos = new Point((source.X + target.X) / 2, midY);
        }
        else if (conn.SourceSide == ConnectionPortSide.Bottom && conn.TargetSide == ConnectionPortSide.Bottom)
        {
            // Bottom bracket: source down → horizontal → target up
            var distance = Math.Abs(target.X - source.X);
            heightOffset = Math.Max(30.0, Math.Min(60.0, distance * 0.15));

            var midY = Math.Max(source.Y, target.Y) + heightOffset;

            var pathFigure = new PathFigure { StartPoint = source, IsClosed = false };
            pathFigure.Segments.Add(new LineSegment { Point = new Point(source.X, midY) });
            pathFigure.Segments.Add(new LineSegment { Point = new Point(target.X, midY) });
            pathFigure.Segments.Add(new LineSegment { Point = new Point(target.X, target.Y) });

            var geometry = new PathGeometry();
            geometry.Figures.Add(pathFigure);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = accentBrush,
                StrokeThickness = 2,
                Opacity = 0.85,
                IsHitTestVisible = false,
                Data = geometry,
                StrokeLineJoin = PenLineJoin.Round,
            };
            pathShape = path;
            labelPos = new Point((source.X + target.X) / 2, midY);
        }
        else if ((conn.SourceSide == ConnectionPortSide.Right && conn.TargetSide == ConnectionPortSide.Left) ||
                 (conn.SourceSide == ConnectionPortSide.Left && conn.TargetSide == ConnectionPortSide.Right))
        {
            // Horizontal straight line
            var line = new Line
            {
                X1 = source.X,
                Y1 = source.Y,
                X2 = target.X,
                Y2 = target.Y,
                Stroke = accentBrush,
                StrokeThickness = 2,
                Opacity = 0.85,
                IsHitTestVisible = false,
            };
            pathShape = line;
            labelPos = new Point((source.X + target.X) / 2, (source.Y + target.Y) / 2);
        }
        else
        {
            // Mixed sides: use elbow connector (L-shape)
            var midX = (source.X + target.X) / 2;
            var midY = (source.Y + target.Y) / 2;

            var pathFigure = new PathFigure { StartPoint = source, IsClosed = false };

            if (conn.SourceSide == ConnectionPortSide.Top || conn.SourceSide == ConnectionPortSide.Bottom)
            {
                // Go vertical first, then horizontal
                pathFigure.Segments.Add(new LineSegment { Point = new Point(source.X, target.Y) });
                pathFigure.Segments.Add(new LineSegment { Point = target });
            }
            else
            {
                // Go horizontal first, then vertical
                pathFigure.Segments.Add(new LineSegment { Point = new Point(target.X, source.Y) });
                pathFigure.Segments.Add(new LineSegment { Point = target });
            }

            var geometry = new PathGeometry();
            geometry.Figures.Add(pathFigure);

            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Stroke = accentBrush,
                StrokeThickness = 2,
                Opacity = 0.85,
                IsHitTestVisible = false,
                Data = geometry,
                StrokeLineJoin = PenLineJoin.Round,
            };
            pathShape = path;
            labelPos = new Point(midX, midY);
        }

        _overlayCanvas.Children.Add(pathShape);

        // Add small compact label chip at the midpoint of the horizontal bar
        var labelBorder = new Border
        {
            Background = new SolidColorBrush(ColorHelper.FromArgb(240, 8, 20, 40)),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(160, 31, 122, 224)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            IsHitTestVisible = false,
            Tag = $"label-border-{conn.Id}",
        };

        var labelBlock = new TextBlock
        {
            Text = conn.Label,
            FontSize = 9,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 120, 200, 255)),
            IsHitTestVisible = false,
            Tag = $"label-{conn.Id}",
        };
        labelBorder.Child = labelBlock;

        // Position label centered on the horizontal bar
        Canvas.SetLeft(labelBorder, labelPos.X - 16);
        Canvas.SetTop(labelBorder, labelPos.Y - 10);
        _overlayCanvas.Children.Add(labelBorder);

        // Add small dots at connection points
        AddPortDot(source);
        AddPortDot(target);

        return new ConnectorVisual
        {
            Connection = conn,
            Path = pathShape,
            LabelBorder = labelBorder,
            SourcePoint = source,
            TargetPoint = target,
            IsCurved = false,
        };
    }

    private void AddPortDot(Point point)
    {
        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(ColorHelper.FromArgb(255, 31, 122, 224)),
            IsHitTestVisible = false,
        };
        Canvas.SetLeft(dot, point.X - 3);
        Canvas.SetTop(dot, point.Y - 3);
        _overlayCanvas.Children.Add(dot);
    }

    private static bool NeedsCurve(ConnectionPortSide sourceSide, ConnectionPortSide targetSide)
    {
        // Straight line only for left-to-right or right-to-left
        if ((sourceSide == ConnectionPortSide.Left && targetSide == ConnectionPortSide.Right) ||
            (sourceSide == ConnectionPortSide.Right && targetSide == ConnectionPortSide.Left))
            return false;

        return true;
    }

    /// <summary>
    /// Calculates the actual midpoint on the curve/line at t=0.5.
    /// For curves, this is the true bezier midpoint (on the arc).
    /// </summary>
    private static Point GetCurveMidpoint(Point source, Point target, ConnectionPortSide sourceSide, ConnectionPortSide targetSide, bool isCurved)
    {
        if (!isCurved)
        {
            return new Point((source.X + target.X) / 2, (source.Y + target.Y) / 2);
        }

        // Calculate bezier midpoint at t=0.5
        var dist = Math.Sqrt(Math.Pow(target.X - source.X, 2) + Math.Pow(target.Y - source.Y, 2));
        var offset = Math.Max(40.0, dist * 0.4);

        var cp1 = GetControlPoint(source, sourceSide, offset);
        var cp2 = GetControlPoint(target, targetSide, offset);

        // Cubic bezier at t=0.5: B(0.5) = (1-t)^3*P0 + 3*(1-t)^2*t*P1 + 3*(1-t)*t^2*P2 + t^3*P3
        const double t = 0.5;
        var mt = 1.0 - t;
        var x = mt * mt * mt * source.X + 3 * mt * mt * t * cp1.X + 3 * mt * t * t * cp2.X + t * t * t * target.X;
        var y = mt * mt * mt * source.Y + 3 * mt * mt * t * cp1.Y + 3 * mt * t * t * cp2.Y + t * t * t * target.Y;

        return new Point(x, y);
    }

    private static Geometry CreateBezierGeometry(Point source, Point target, ConnectionPortSide sourceSide, ConnectionPortSide targetSide)
    {
        // Calculate offset based on distance between points for natural curves
        var dist = Math.Sqrt(Math.Pow(target.X - source.X, 2) + Math.Pow(target.Y - source.Y, 2));
        var offset = Math.Max(40.0, dist * 0.4); // Dynamic offset based on distance

        var cp1 = GetControlPoint(source, sourceSide, offset);
        var cp2 = GetControlPoint(target, targetSide, offset);

        var figure = new PathFigure
        {
            StartPoint = source,
            IsClosed = false,
        };

        figure.Segments.Add(new BezierSegment
        {
            Point1 = cp1,
            Point2 = cp2,
            Point3 = target,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private static Point GetControlPoint(Point port, ConnectionPortSide side, double offset)
    {
        // Control point goes AWAY from the cell in the direction of the port side
        // Top port → control point goes UP (negative Y)
        // Bottom port → control point goes DOWN (positive Y)
        // Left port → control point goes LEFT (negative X)
        // Right port → control point goes RIGHT (positive X)
        return side switch
        {
            ConnectionPortSide.Top => new Point(port.X, port.Y - offset),
            ConnectionPortSide.Bottom => new Point(port.X, port.Y + offset),
            ConnectionPortSide.Left => new Point(port.X - offset, port.Y),
            ConnectionPortSide.Right => new Point(port.X + offset, port.Y),
            _ => port,
        };
    }

    private static bool IsPointNearPath(Point point, Point source, Point target, bool isCurved, double tolerance)
    {
        // For bracket-style lines, check distance to each segment
        // Segment 1: source → (source.X, midY)
        // Segment 2: (source.X, midY) → (target.X, midY)
        // Segment 3: (target.X, midY) → target
        var midY = Math.Min(source.Y, target.Y) - 40;

        var seg1Start = source;
        var seg1End = new Point(source.X, midY);
        if (DistanceToLineSegment(point, seg1Start, seg1End) <= tolerance) return true;

        var seg2Start = seg1End;
        var seg2End = new Point(target.X, midY);
        if (DistanceToLineSegment(point, seg2Start, seg2End) <= tolerance) return true;

        var seg3Start = seg2End;
        var seg3End = target;
        if (DistanceToLineSegment(point, seg3Start, seg3End) <= tolerance) return true;

        // Also check simple straight line (for left-right connections)
        return DistanceToLineSegment(point, source, target) <= tolerance;
    }

    private static Point GetBezierPoint(Point source, Point target, double t)
    {
        // Simple quadratic approximation for hit testing
        var midX = (source.X + target.X) / 2;
        var midY = Math.Min(source.Y, target.Y) - 30; // Arc upward

        var x = (1 - t) * (1 - t) * source.X + 2 * (1 - t) * t * midX + t * t * target.X;
        var y = (1 - t) * (1 - t) * source.Y + 2 * (1 - t) * t * midY + t * t * target.Y;
        return new Point(x, y);
    }

    private static double DistanceToLineSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lenSq = dx * dx + dy * dy;

        if (lenSq == 0) return Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2));

        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq, 0, 1);
        var projX = a.X + t * dx;
        var projY = a.Y + t * dy;

        return Math.Sqrt(Math.Pow(p.X - projX, 2) + Math.Pow(p.Y - projY, 2));
    }

    private sealed class ConnectorVisual
    {
        public required CellConnection Connection { get; init; }
        public required Shape Path { get; init; }
        public required Border LabelBorder { get; init; }
        public required Point SourcePoint { get; init; }
        public required Point TargetPoint { get; init; }
        public required bool IsCurved { get; init; }
    }
}
