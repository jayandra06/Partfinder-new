using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace PartFinder.Views.Components;

public sealed partial class InfiniteCanvasColumnCell : UserControl
{
    private bool _isHeaderDragging;
    private Point _headerDragStart;

    public InfiniteCanvasColumnCell()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateLastBorder();
    }

    public static readonly DependencyProperty IndexProperty = DependencyProperty.Register(
        nameof(Index),
        typeof(int),
        typeof(InfiniteCanvasColumnCell),
        new PropertyMetadata(0));

    public static readonly DependencyProperty IsLastProperty = DependencyProperty.Register(
        nameof(IsLast),
        typeof(bool),
        typeof(InfiniteCanvasColumnCell),
        new PropertyMetadata(false, OnIsLastChanged));

    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        nameof(HeaderText),
        typeof(string),
        typeof(InfiniteCanvasColumnCell),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CellContentProperty = DependencyProperty.Register(
        nameof(CellContent),
        typeof(string),
        typeof(InfiniteCanvasColumnCell),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected),
        typeof(bool),
        typeof(InfiniteCanvasColumnCell),
        new PropertyMetadata(false, OnIsSelectedChanged));

    public int Index
    {
        get => (int)GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public bool IsLast
    {
        get => (bool)GetValue(IsLastProperty);
        set => SetValue(IsLastProperty, value);
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string CellContent
    {
        get => (string)GetValue(CellContentProperty);
        set => SetValue(CellContentProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public event EventHandler<int>? InsertAfterRequested;
    public event EventHandler<ColumnHeaderDragEventArgs>? HeaderDragStarted;
    public event EventHandler<ColumnHeaderDragEventArgs>? HeaderDragDelta;
    public event EventHandler<ColumnHeaderDragEventArgs>? HeaderDragCompleted;
    public event EventHandler<ColumnContextMenuRequestedEventArgs>? ColumnContextMenuRequested;
    public event EventHandler<int>? HeaderSelectionRequested;

    private static void OnIsLastChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfiniteCanvasColumnCell cell)
        {
            cell.UpdateLastBorder();
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InfiniteCanvasColumnCell cell)
        {
            cell.UpdateSelectionVisual();
        }
    }

    private void UpdateLastBorder()
    {
        var borderThickness = IsLast ? new Thickness(1) : new Thickness(1, 1, 0, 1);
        HeaderBorder.BorderThickness = borderThickness;
        ContentBorder.BorderThickness = borderThickness;

        if (IsLast)
        {
            HeaderBorder.CornerRadius = new CornerRadius(0, 8, 0, 0);
            ContentBorder.CornerRadius = new CornerRadius(0, 0, 8, 0);
        }
        else if (Index == 0)
        {
            HeaderBorder.CornerRadius = new CornerRadius(8, 0, 0, 0);
            ContentBorder.CornerRadius = new CornerRadius(0, 0, 0, 8);
        }
        else
        {
            HeaderBorder.CornerRadius = new CornerRadius(0);
            ContentBorder.CornerRadius = new CornerRadius(0);
        }
    }

    private void OnInsertButtonClick(object sender, RoutedEventArgs e)
    {
        InsertAfterRequested?.Invoke(this, Index);
    }

    private void OnRootPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        Canvas.SetZIndex(this, 1000 + Index);
        AnimateHoverState(hovered: true);
        ShowInsertButton();
    }

    private void OnRootPointerExited(object sender, PointerRoutedEventArgs e)
    {
        Canvas.SetZIndex(this, Index);
        AnimateHoverState(hovered: false);
        HideInsertButton();
    }

    private void ShowInsertButton()
    {
        InsertButton.Visibility = Visibility.Visible;
        var storyboard = new Storyboard();

        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, InsertButton);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var scaleX = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleX, InsertButtonScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");

        var scaleY = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleY, InsertButtonScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");

        storyboard.Children.Add(fade);
        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);
        storyboard.Begin();
    }

    private void HideInsertButton()
    {
        var storyboard = new Storyboard();

        var fade = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, InsertButton);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var scaleX = new DoubleAnimation
        {
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleX, InsertButtonScale);
        Storyboard.SetTargetProperty(scaleX, "ScaleX");

        var scaleY = new DoubleAnimation
        {
            To = 0.6,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(scaleY, InsertButtonScale);
        Storyboard.SetTargetProperty(scaleY, "ScaleY");

        storyboard.Children.Add(fade);
        storyboard.Children.Add(scaleX);
        storyboard.Children.Add(scaleY);
        storyboard.Completed += (_, _) =>
        {
            if (!InsertButton.IsPointerOver)
            {
                InsertButton.Visibility = Visibility.Collapsed;
            }
        };
        storyboard.Begin();
    }

    private void OnInsertButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        var accentBrush = (Brush)Application.Current.Resources["AccentPrimaryBrush"];
        InsertButton.Background = accentBrush;
        InsertButton.Foreground = (Brush)Application.Current.Resources["TextOnAccentBrush"];
        AnimateButtonScale(1.2);
    }

    private void OnInsertButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        InsertButton.Background = (Brush)Application.Current.Resources["CardBackgroundBrush"];
        InsertButton.Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        AnimateButtonScale(1);
    }

    private void AnimateButtonScale(double target)
    {
        var storyboard = new Storyboard();
        var x = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(x, InsertButtonScale);
        Storyboard.SetTargetProperty(x, "ScaleX");

        var y = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(y, InsertButtonScale);
        Storyboard.SetTargetProperty(y, "ScaleY");

        storyboard.Children.Add(x);
        storyboard.Children.Add(y);
        storyboard.Begin();
    }

    private void AnimateHoverState(bool hovered)
    {
        if (HeaderBackgroundBrush is not SolidColorBrush headerBrush
            || ContentBackgroundBrush is not SolidColorBrush contentBrush
            || CellBorderBrush is not SolidColorBrush borderBrush)
        {
            return;
        }

        var headerTo = hovered
            ? ((SolidColorBrush)Application.Current.Resources["GridHeaderBackgroundBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["GridHeaderBackgroundBrush"]).Color;
        var contentTo = hovered
            ? ((SolidColorBrush)Application.Current.Resources["GridRowAltBackgroundBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["CardBackgroundBrush"]).Color;
        var borderTo = hovered
            ? ((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["BorderDefaultBrush"]).Color;

        var storyboard = new Storyboard();

        var headerAnim = new ColorAnimation
        {
            To = headerTo,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(headerAnim, headerBrush);
        Storyboard.SetTargetProperty(headerAnim, "Color");

        var contentAnim = new ColorAnimation
        {
            To = contentTo,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(contentAnim, contentBrush);
        Storyboard.SetTargetProperty(contentAnim, "Color");

        var borderAnim = new ColorAnimation
        {
            To = borderTo,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(borderAnim, borderBrush);
        Storyboard.SetTargetProperty(borderAnim, "Color");

        storyboard.Children.Add(headerAnim);
        storyboard.Children.Add(contentAnim);
        storyboard.Children.Add(borderAnim);
        storyboard.Begin();
    }

    private void OnContentDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        BeginEdit();
        e.Handled = true;
    }

    private void OnContentLostFocus(object sender, RoutedEventArgs e)
    {
        AnimateEditFocus(false);
    }

    private void AnimateEditFocus(bool focused)
    {
        if (CellBorderBrush is not SolidColorBrush borderBrush)
        {
            return;
        }

        var to = focused
            ? ((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color
            : ((SolidColorBrush)Application.Current.Resources["BorderDefaultBrush"]).Color;
        var animation = new ColorAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(animation, borderBrush);
        Storyboard.SetTargetProperty(animation, "Color");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void BeginEdit()
    {
        AnimateEditFocus(true);

        // Queue focus after input routing finishes so double-click consistently enters edit mode.
        DispatcherQueue.TryEnqueue(() =>
        {
            _ = ContentEditor.Focus(FocusState.Programmatic);
            ContentEditor.SelectAll();
        });
    }

    private void OnHeaderPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isHeaderDragging = true;
        _headerDragStart = e.GetCurrentPoint(this).Position;
        HeaderBorder.CapturePointer(e.Pointer);
        var pointerX = e.GetCurrentPoint(null).Position.X;
        HeaderDragStarted?.Invoke(this, new ColumnHeaderDragEventArgs(Index, 0, pointerX));
        e.Handled = true;
    }

    private void OnHeaderPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isHeaderDragging)
        {
            return;
        }

        var current = e.GetCurrentPoint(this).Position;
        var deltaX = current.X - _headerDragStart.X;
        var pointerX = e.GetCurrentPoint(null).Position.X;
        HeaderDragDelta?.Invoke(this, new ColumnHeaderDragEventArgs(Index, deltaX, pointerX));
        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isHeaderDragging)
        {
            return;
        }

        var current = e.GetCurrentPoint(this).Position;
        var deltaX = current.X - _headerDragStart.X;
        _isHeaderDragging = false;
        HeaderBorder.ReleasePointerCapture(e.Pointer);
        var pointerX = e.GetCurrentPoint(null).Position.X;
        HeaderDragCompleted?.Invoke(this, new ColumnHeaderDragEventArgs(Index, deltaX, pointerX));
        e.Handled = true;
    }

    private void OnCellRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        var position = e.GetPosition(this);
        ColumnContextMenuRequested?.Invoke(this, new ColumnContextMenuRequestedEventArgs(Index, this, position));
        e.Handled = true;
    }

    private void OnHeaderTapped(object sender, TappedRoutedEventArgs e)
    {
        HeaderSelectionRequested?.Invoke(this, Index);
        e.Handled = true;
    }

    private void UpdateSelectionVisual()
    {
        if (CellBorderBrush is not SolidColorBrush borderBrush
            || HeaderBackgroundBrush is not SolidColorBrush headerBrush
            || ContentBackgroundBrush is not SolidColorBrush contentBrush)
        {
            return;
        }

        var accent = ((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color;
        var defaultBorder = ((SolidColorBrush)Application.Current.Resources["BorderDefaultBrush"]).Color;
        var headerBase = ((SolidColorBrush)Application.Current.Resources["GridHeaderBackgroundBrush"]).Color;
        var contentBase = ((SolidColorBrush)Application.Current.Resources["CardBackgroundBrush"]).Color;

        borderBrush.Color = IsSelected ? accent : defaultBorder;
        headerBrush.Color = IsSelected ? Blend(headerBase, accent, 0.24) : headerBase;
        contentBrush.Color = IsSelected ? Blend(contentBase, accent, 0.1) : contentBase;
    }

    private static Windows.UI.Color Blend(Windows.UI.Color baseColor, Windows.UI.Color tintColor, double amount)
    {
        var alpha = Math.Clamp(amount, 0, 1);
        byte Lerp(byte a, byte b) => (byte)Math.Round(a + ((b - a) * alpha));
        return Windows.UI.Color.FromArgb(
            255,
            Lerp(baseColor.R, tintColor.R),
            Lerp(baseColor.G, tintColor.G),
            Lerp(baseColor.B, tintColor.B));
    }
}

public sealed class ColumnHeaderDragEventArgs : EventArgs
{
    public ColumnHeaderDragEventArgs(int index, double deltaX, double pointerX)
    {
        Index = index;
        DeltaX = deltaX;
        PointerX = pointerX;
    }

    public int Index { get; }
    public double DeltaX { get; }
    public double PointerX { get; }
}

public sealed class ColumnContextMenuRequestedEventArgs : EventArgs
{
    public ColumnContextMenuRequestedEventArgs(int index, FrameworkElement target, Point position)
    {
        Index = index;
        Target = target;
        Position = position;
    }

    public int Index { get; }
    public FrameworkElement Target { get; }
    public Point Position { get; }
}
