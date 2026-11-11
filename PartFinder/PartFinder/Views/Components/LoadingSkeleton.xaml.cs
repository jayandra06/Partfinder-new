using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace PartFinder.Views.Components;

public enum SkeletonKind
{
    Lines,
    Table,
    Cards,
    Page,
    Block,
    Inline,
}

public sealed partial class LoadingSkeleton : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(SkeletonKind),
        typeof(LoadingSkeleton),
        new PropertyMetadata(SkeletonKind.Table, OnKindChanged));

    public LoadingSkeleton()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Rebuild();
            if (Resources.TryGetValue("PulseStoryboard", out var resource) && resource is Storyboard pulse)
            {
                try
                {
                    pulse.Begin();
                }
                catch
                {
                    // Placeholder bars still show if the pulse animation cannot start.
                }
            }
        };
        Unloaded += (_, _) =>
        {
            if (Resources.TryGetValue("PulseStoryboard", out var resource) && resource is Storyboard pulse)
            {
                pulse.Stop();
            }
        };
    }

    public SkeletonKind Kind
    {
        get => (SkeletonKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoadingSkeleton skeleton && skeleton.IsLoaded)
        {
            skeleton.Rebuild();
        }
    }

    private void Rebuild()
    {
        Host.Children.Clear();
        Host.Children.Add(Kind switch
        {
            SkeletonKind.Table => BuildTable(),
            SkeletonKind.Cards => BuildCards(),
            SkeletonKind.Page => BuildPage(),
            SkeletonKind.Block => BuildBlock(),
            SkeletonKind.Inline => Bar(10, radius: 5),
            _ => BuildLines(),
        });
    }

    private static UIElement BuildPage()
    {
        var root = new StackPanel { Spacing = 16 };
        root.Children.Add(Bar(26, 220));
        root.Children.Add(Bar(12, 340));
        root.Children.Add(BuildCards());
        root.Children.Add(BuildTable());
        return root;
    }

    private static UIElement BuildCards()
    {
        var row = new Grid { ColumnSpacing = 12, Height = 92 };
        for (var i = 0; i < 4; i++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var card = Bar(92, radius: 12);
            Grid.SetColumn(card, i);
            row.Children.Add(card);
        }

        return row;
    }

    private static UIElement BuildTable()
    {
        var root = new StackPanel { Spacing = 10 };
        root.Children.Add(Bar(14, 160));
        root.Children.Add(BuildTableRow(isHeader: true));
        for (var i = 0; i < 8; i++)
        {
            root.Children.Add(BuildTableRow(isHeader: false));
        }

        return root;
    }

    private static UIElement BuildTableRow(bool isHeader)
    {
        var row = new Grid { ColumnSpacing = 10, Height = isHeader ? 18 : 28 };
        double[] widths = isHeader ? [0.8, 1, 1.2, 0.7, 0.9] : [1, 0.9, 1.1, 0.75, 0.85];
        for (var i = 0; i < widths.Length; i++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i], GridUnitType.Star) });
            var cell = Bar(isHeader ? 12 : 16, radius: 4);
            cell.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(cell, i);
            row.Children.Add(cell);
        }

        return row;
    }

    private static UIElement BuildLines()
    {
        var root = new StackPanel { Spacing = 10 };
        double[] widths = [1, 0.92, 0.78, 0.96, 0.7, 0.88];
        foreach (var width in widths)
        {
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - width, GridUnitType.Star) });
            var bar = Bar(16);
            Grid.SetColumn(bar, 0);
            line.Children.Add(bar);
            root.Children.Add(line);
        }

        return root;
    }

    private static UIElement BuildBlock() => Bar(double.NaN, radius: 12);

    private static Border Bar(double height, double width = double.NaN, double radius = 6)
    {
        var bar = new Border
        {
            CornerRadius = new CornerRadius(radius),
            Background = ResolveBrush(),
            HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        if (!double.IsNaN(height))
        {
            bar.Height = height;
        }

        if (!double.IsNaN(width))
        {
            bar.Width = width;
        }

        return bar;
    }

    private static Brush ResolveBrush()
    {
        if (Application.Current.Resources.TryGetValue("AppSkeletonBrush", out var brush) && brush is Brush resolved)
        {
            return resolved;
        }

        return new SolidColorBrush(Microsoft.UI.Colors.Gray);
    }
}
