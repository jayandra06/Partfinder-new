using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Shapes;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.Views.Components;
using Windows.Foundation;
using Microsoft.UI.Xaml.Navigation;

namespace PartFinder.Views.Pages;

public sealed partial class InfiniteCanvasSpreadsheetPage : Page
{
    private const double MinZoom = 0.2;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.1;
    private const double BaseX = 0;
    private const double BaseY = 0;
    private const double DefaultColumnWidth = 128;
    private const double DefaultColumnHeight = 108;
    private const double CanvasTopInset = 170;
    private const double LeftViewportInset = 8;
    private const double RightViewportGutterAtMinZoom = 14;
    private const double RightViewportGutterAtMaxZoom = 30;

    private readonly ObservableCollection<InfiniteCanvasColumnItem> _columns = [];
    private readonly Dictionary<InfiniteCanvasColumnItem, InfiniteCanvasColumnCell> _cellMap = [];
    private readonly Dictionary<string, PartTemplateDefinition> _templateByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PartTemplateDefinition> _templateById = new(StringComparer.Ordinal);
    private readonly List<string> _templateNames = [];
    private readonly ITemplateSchemaService _templateSchemaService;
    private PartTemplateDefinition? _activeTemplate;
    private readonly HashSet<int> _selectedColumnIndices = [];
    private readonly List<CanvasColumnGroup> _groups = [];

    private bool _isPanning;
    private bool _isInitialViewPositioned;
    private Point _panStartScreen;
    private double _startTranslateX;
    private double _startTranslateY;
    private double _dragStartPointerX;
    private int _contextColumnIndex = -1;
    private InfiniteCanvasColumnItem? _copiedColumn;
    private MenuFlyout? _columnContextMenu;
    private MenuFlyout? _groupContextMenu;
    private CanvasColumnGroup? _contextGroup;
    private Border? _groupLane;
    private Border? _groupDragPreview;
    private bool _isGroupLaneDragging;
    private int _groupDragStartIndex = -1;
    private int _groupDragEndIndex = -1;
    private int? _selectionAnchorIndex;
    private InfiniteCanvasColumnItem? _draggingColumn;
    private int _dragOriginalIndex = -1;
    private int _dragTargetIndex = -1;
    private double _dragStartLeft;
    private string? _pendingTemplateToLoad;

    public InfiniteCanvasSpreadsheetPage()
    {
        InitializeComponent();
        _templateSchemaService = App.Services.GetRequiredService<ITemplateSchemaService>();
        Loaded += OnLoaded;
        Viewport.SizeChanged += OnViewportSizeChanged;
        InitializeColumns();
        InitializeGroupLane();
        AnimateHintFadeOut();
        SetZoomText();
        UpdateSelectionSummary();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionInitialViewport();
        await LoadTemplateSuggestionsAsync();
        if (!string.IsNullOrWhiteSpace(_pendingTemplateToLoad))
        {
            LoadTemplateIntoCanvas(_pendingTemplateToLoad);
            TemplateSuggestBox.Text = _pendingTemplateToLoad;
            _pendingTemplateToLoad = null;
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _pendingTemplateToLoad = e.Parameter as string;
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Viewport.Clip = new RectangleGeometry
        {
            Rect = new Rect(0, 0, Math.Max(0, Viewport.ActualWidth), Math.Max(0, Viewport.ActualHeight)),
        };
        ClampWorldTranslation();
        PositionInitialViewport();
    }

    private void PositionInitialViewport()
    {
        if (_isInitialViewPositioned || Viewport.ActualWidth < 1 || Viewport.ActualHeight < 1)
        {
            return;
        }

        var scale = WorldScaleTransform.ScaleX;
        WorldTranslateTransform.X = GetCenteredX(scale);
        WorldTranslateTransform.Y = CanvasTopInset;
        ClampWorldTranslation();
        _isInitialViewPositioned = true;
    }

    private void InitializeGroupLane()
    {
        _groupLane = new Border
        {
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color)
            {
                Opacity = 0.1,
            },
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"],
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "Drag here to create group",
                FontSize = 10,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextSecondaryBrush"],
            },
        };
        _groupLane.PointerPressed += OnGroupLanePointerPressed;
        _groupLane.PointerMoved += OnGroupLanePointerMoved;
        _groupLane.PointerReleased += OnGroupLanePointerReleased;
        _groupLane.PointerCanceled += OnGroupLanePointerCanceled;
        WorldCanvas.Children.Add(_groupLane);
        Canvas.SetZIndex(_groupLane, 3900);
        RefreshGroupLaneVisual();
    }

    private void ClampWorldTranslation()
    {
        if (Viewport.ActualWidth <= 0)
        {
            return;
        }

        var scale = WorldScaleTransform.ScaleX;
        var contentWidth = Math.Max(DefaultColumnWidth, _columns.Count * DefaultColumnWidth);
        var scaledContentWidth = contentWidth * scale;
        var rightGutter = GetRightViewportGutter(scale);
        var availableWidth = Math.Max(0, Viewport.ActualWidth - LeftViewportInset - rightGutter);

        // Keep content inside viewport bounds (left inset + right gutter).
        // When content is narrower than available width, allow free panning in that lane.
        if (scaledContentWidth <= availableWidth)
        {
            var minX = LeftViewportInset;
            var maxX = LeftViewportInset + (availableWidth - scaledContentWidth);
            WorldTranslateTransform.X = Math.Clamp(WorldTranslateTransform.X, minX, maxX);
        }
        else
        {
            var maxX = LeftViewportInset;
            var minX = LeftViewportInset + (availableWidth - scaledContentWidth);
            WorldTranslateTransform.X = Math.Clamp(WorldTranslateTransform.X, minX, maxX);
        }

        // Keep top controls clear while allowing downward pan.
        WorldTranslateTransform.Y = Math.Max(CanvasTopInset, WorldTranslateTransform.Y);
    }

    private double GetCenteredX(double scale)
    {
        var contentWidth = Math.Max(DefaultColumnWidth, _columns.Count * DefaultColumnWidth);
        var scaledContentWidth = contentWidth * scale;
        var rightGutter = GetRightViewportGutter(scale);
        var availableWidth = Math.Max(0, Viewport.ActualWidth - LeftViewportInset - rightGutter);
        var centered = LeftViewportInset + ((availableWidth - scaledContentWidth) / 2);

        if (scaledContentWidth <= availableWidth)
        {
            var minX = LeftViewportInset;
            var maxX = LeftViewportInset + (availableWidth - scaledContentWidth);
            return Math.Clamp(centered, minX, maxX);
        }

        var maxOverflowX = LeftViewportInset;
        var minOverflowX = LeftViewportInset + (availableWidth - scaledContentWidth);
        return Math.Clamp(centered, minOverflowX, maxOverflowX);
    }

    private static double GetRightViewportGutter(double scale)
    {
        var normalizedZoom = (Math.Clamp(scale, MinZoom, MaxZoom) - MinZoom) / (MaxZoom - MinZoom);
        return RightViewportGutterAtMinZoom
             + ((RightViewportGutterAtMaxZoom - RightViewportGutterAtMinZoom) * normalizedZoom);
    }

    private async Task LoadTemplateSuggestionsAsync()
    {
        try
        {
            var templates = await _templateSchemaService.GetTemplatesAsync();
            _templateNames.Clear();
            _templateByName.Clear();
            _templateById.Clear();

            foreach (var template in templates)
            {
                _templateById[template.Id] = template;
            }

            foreach (var template in templates.OrderByDescending(t => t.Version))
            {
                var name = template.Name?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!_templateByName.TryGetValue(name, out var existing)
                    || template.Version > existing.Version
                    || (template.Version == existing.Version && template.Fields.Count > existing.Fields.Count))
                {
                    _templateByName[name] = template;
                }
            }

            _templateNames.AddRange(
                _templateByName.Keys
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase));
            TemplateSuggestBox.ItemsSource = _templateNames;
        }
        catch
        {
            _templateNames.Clear();
            TemplateSuggestBox.ItemsSource = null;
        }
    }

    private void InitializeColumns()
    {
        for (var i = 0; i < 3; i++)
        {
            _columns.Add(
                new InfiniteCanvasColumnItem
                {
                    Index = i,
                    Width = DefaultColumnWidth,
                    Height = DefaultColumnHeight,
                    HeaderText = $"Column {i + 1}",
                    CellContent = string.Empty,
                    IsLast = i == 2,
                });
        }

        foreach (var column in _columns)
        {
            var cell = CreateCell(column);
            _cellMap[column] = cell;
            WorldCanvas.Children.Add(cell);
        }

        RefreshPositions(animate: false);
    }

    private InfiniteCanvasColumnCell CreateCell(InfiniteCanvasColumnItem item)
    {
        var cell = new InfiniteCanvasColumnCell
        {
            Width = item.Width,
            Height = item.Height,
            Index = item.Index,
            HeaderText = item.HeaderText,
            CellContent = item.CellContent,
            IsLast = item.IsLast,
        };
        cell.InsertAfterRequested += OnInsertAfterRequested;
        cell.HeaderDragStarted += OnHeaderDragStarted;
        cell.HeaderDragDelta += OnHeaderDragDelta;
        cell.HeaderDragCompleted += OnHeaderDragCompleted;
        cell.ColumnContextMenuRequested += OnColumnContextMenuRequested;
        cell.HeaderSelectionRequested += OnHeaderSelectionRequested;
        return cell;
    }

    private void OnInsertAfterRequested(object? sender, int index)
    {
        InsertColumnAfter(index);
    }

    private void InsertColumnAfter(int index)
    {
        InsertColumnAfter(index, seed: null);
    }

    private void InsertColumnAfter(int index, InfiniteCanvasColumnItem? seed)
    {
        var insertAt = Math.Clamp(index + 1, 0, _columns.Count);
        var item = new InfiniteCanvasColumnItem
        {
            Index = insertAt,
            Width = seed?.Width ?? DefaultColumnWidth,
            Height = seed?.Height ?? DefaultColumnHeight,
            HeaderText = seed?.HeaderText ?? "New column",
            CellContent = seed?.CellContent ?? string.Empty,
            IsLast = false,
        };

        _columns.Insert(insertAt, item);
        ReindexColumns();
        ClampWorldTranslation();

        var cell = CreateCell(item);
        _cellMap[item] = cell;
        WorldCanvas.Children.Add(cell);

        var targetX = GetColumnX(item.Index);
        Canvas.SetLeft(cell, targetX + 20);
        Canvas.SetTop(cell, BaseY);
        cell.Opacity = 0;

        RefreshPositions(animate: true, staggerFromIndex: insertAt);
        AnimateNewCell(cell, targetX);
    }

    private void OnColumnContextMenuRequested(object? sender, ColumnContextMenuRequestedEventArgs e)
    {
        _contextColumnIndex = e.Index;
        _columnContextMenu ??= BuildColumnContextMenu();
        _columnContextMenu.ShowAt(e.Target, e.Position);
    }

    private MenuFlyout BuildColumnContextMenu()
    {
        var menu = new MenuFlyout();

        var deleteItem = new MenuFlyoutItem { Text = "Delete Column" };
        deleteItem.Click += (_, _) => DeleteContextColumn();
        menu.Items.Add(deleteItem);

        var copyItem = new MenuFlyoutItem { Text = "Copy" };
        copyItem.Click += (_, _) => CopyContextColumn();
        menu.Items.Add(copyItem);

        var cloneItem = new MenuFlyoutItem { Text = "Clone" };
        cloneItem.Click += (_, _) => CloneContextColumn();
        menu.Items.Add(cloneItem);

        var pasteItem = new MenuFlyoutItem { Text = "Paste" };
        pasteItem.Click += (_, _) => PasteAfterContextColumn();
        menu.Items.Add(pasteItem);

        return menu;
    }

    private void DeleteContextColumn()
    {
        if (_contextColumnIndex < 0 || _contextColumnIndex >= _columns.Count || _columns.Count <= 1)
        {
            return;
        }

        var item = _columns[_contextColumnIndex];
        if (_cellMap.TryGetValue(item, out var cell))
        {
            WorldCanvas.Children.Remove(cell);
            _cellMap.Remove(item);
        }

        _columns.RemoveAt(_contextColumnIndex);
        ReindexColumns();
        ClampWorldTranslation();
        RefreshPositions(animate: true);
        SetTemplateStatus("Column deleted.");
    }

    private void CopyContextColumn()
    {
        if (_contextColumnIndex < 0 || _contextColumnIndex >= _columns.Count)
        {
            return;
        }

        var src = _columns[_contextColumnIndex];
        _copiedColumn = new InfiniteCanvasColumnItem
        {
            Width = src.Width,
            Height = src.Height,
            HeaderText = src.HeaderText,
            CellContent = src.CellContent,
            Index = 0,
            IsLast = false,
        };
        SetTemplateStatus("Column copied.");
    }

    private void CloneContextColumn()
    {
        if (_contextColumnIndex < 0 || _contextColumnIndex >= _columns.Count)
        {
            return;
        }

        var src = _columns[_contextColumnIndex];
        var clone = new InfiniteCanvasColumnItem
        {
            Width = src.Width,
            Height = src.Height,
            HeaderText = src.HeaderText,
            CellContent = src.CellContent,
            Index = 0,
            IsLast = false,
        };
        InsertColumnAfter(_contextColumnIndex, clone);
        SetTemplateStatus("Column cloned.");
    }

    private void PasteAfterContextColumn()
    {
        if (_copiedColumn is null || _contextColumnIndex < 0 || _contextColumnIndex >= _columns.Count)
        {
            return;
        }

        var pasted = new InfiniteCanvasColumnItem
        {
            Width = _copiedColumn.Width,
            Height = _copiedColumn.Height,
            HeaderText = _copiedColumn.HeaderText,
            CellContent = _copiedColumn.CellContent,
            Index = 0,
            IsLast = false,
        };
        InsertColumnAfter(_contextColumnIndex, pasted);
        SetTemplateStatus("Column pasted.");
    }

    private void ReindexColumns()
    {
        for (var i = 0; i < _columns.Count; i++)
        {
            var item = _columns[i];
            var previousIndex = item.Index;
            var previousHeader = item.HeaderText?.Trim() ?? string.Empty;
            item.Index = i;

            // Keep user-defined names intact, but auto-renumber generated/default headers.
            var shouldAutoNumberHeader =
                string.IsNullOrWhiteSpace(previousHeader)
                || string.Equals(previousHeader, "New column", StringComparison.OrdinalIgnoreCase)
                || string.Equals(previousHeader, $"Column {previousIndex + 1}", StringComparison.OrdinalIgnoreCase);

            if (shouldAutoNumberHeader)
            {
                item.HeaderText = $"Column {i + 1}";
            }
            item.IsLast = i == _columns.Count - 1;

            if (_cellMap.TryGetValue(item, out var cell))
            {
                cell.Index = item.Index;
                cell.HeaderText = item.HeaderText ?? string.Empty;
                cell.IsLast = item.IsLast;
                cell.IsSelected = _selectedColumnIndices.Contains(item.Index);
            }
        }
        _selectedColumnIndices.RemoveWhere(i => i < 0 || i >= _columns.Count);
        RefreshGroupVisuals();
    }

    private void RefreshPositions(bool animate, int staggerFromIndex = -1)
    {
        foreach (var item in _columns)
        {
            if (!_cellMap.TryGetValue(item, out var cell))
            {
                continue;
            }

            var x = GetColumnX(item.Index);
            if (!animate)
            {
                Canvas.SetLeft(cell, x);
                Canvas.SetTop(cell, BaseY);
                Canvas.SetZIndex(cell, item.Index);
                continue;
            }

            var delay = item.Index >= staggerFromIndex && staggerFromIndex >= 0
                ? TimeSpan.FromMilliseconds((item.Index - staggerFromIndex) * 50)
                : TimeSpan.Zero;
            AnimateCanvasLeft(cell, x, 180, delay);
            Canvas.SetTop(cell, BaseY);
            Canvas.SetZIndex(cell, item.Index);
        }
        RefreshGroupVisuals();
        RefreshGroupLaneVisual();
    }

    private void RefreshGroupLaneVisual()
    {
        if (_groupLane is null)
        {
            return;
        }

        var width = Math.Max(DefaultColumnWidth, _columns.Count * DefaultColumnWidth);
        _groupLane.Width = width;
        Canvas.SetLeft(_groupLane, BaseX);
        Canvas.SetTop(_groupLane, BaseY - 50);
    }

    private int GetColumnIndexFromWorldX(double worldX)
    {
        var rawIndex = (int)Math.Floor((worldX - BaseX) / DefaultColumnWidth);
        return Math.Clamp(rawIndex, 0, Math.Max(0, _columns.Count - 1));
    }

    private void OnGroupLanePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_groupLane is null || _columns.Count == 0)
        {
            return;
        }

        var worldPoint = e.GetCurrentPoint(WorldCanvas).Position;
        _groupDragStartIndex = GetColumnIndexFromWorldX(worldPoint.X);
        _groupDragEndIndex = _groupDragStartIndex;
        _isGroupLaneDragging = true;
        _groupLane.CapturePointer(e.Pointer);
        EnsureGroupDragPreview();
        UpdateGroupDragPreview();
        e.Handled = true;
    }

    private void OnGroupLanePointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isGroupLaneDragging)
        {
            return;
        }

        var worldPoint = e.GetCurrentPoint(WorldCanvas).Position;
        _groupDragEndIndex = GetColumnIndexFromWorldX(worldPoint.X);
        UpdateGroupDragPreview();
        e.Handled = true;
    }

    private async void OnGroupLanePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isGroupLaneDragging || _groupLane is null)
        {
            return;
        }

        _isGroupLaneDragging = false;
        _groupLane.ReleasePointerCapture(e.Pointer);
        HideGroupDragPreview();

        var start = Math.Min(_groupDragStartIndex, _groupDragEndIndex);
        var end = Math.Max(_groupDragStartIndex, _groupDragEndIndex);
        if (start == end)
        {
            SetTemplateStatus("Drag across at least 2 columns to create a group.", isError: true);
            e.Handled = true;
            return;
        }

        var groupName = await PromptGroupNameAsync($"Group {_groups.Count + 1}");
        if (groupName is null)
        {
            SetTemplateStatus("Grouping cancelled.");
            e.Handled = true;
            return;
        }

        CreateGroup(start, end, groupName);
        _selectedColumnIndices.Clear();
        UpdateSelectionVisualState();
        e.Handled = true;
    }

    private void OnGroupLanePointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_groupLane is null)
        {
            return;
        }

        _isGroupLaneDragging = false;
        _groupLane.ReleasePointerCapture(e.Pointer);
        HideGroupDragPreview();
    }

    private void EnsureGroupDragPreview()
    {
        if (_groupDragPreview is not null)
        {
            return;
        }

        _groupDragPreview = new Border
        {
            Height = 22,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"],
            Background = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color)
            {
                Opacity = 0.2,
            },
            Visibility = Visibility.Collapsed,
        };
        WorldCanvas.Children.Add(_groupDragPreview);
        Canvas.SetZIndex(_groupDragPreview, 3950);
    }

    private void UpdateGroupDragPreview()
    {
        if (_groupDragPreview is null)
        {
            return;
        }

        var start = Math.Min(_groupDragStartIndex, _groupDragEndIndex);
        var end = Math.Max(_groupDragStartIndex, _groupDragEndIndex);
        _groupDragPreview.Visibility = Visibility.Visible;
        _groupDragPreview.Width = ((end - start) + 1) * DefaultColumnWidth - 2;
        Canvas.SetLeft(_groupDragPreview, GetColumnX(start) + 1);
        Canvas.SetTop(_groupDragPreview, BaseY - 26);
    }

    private void HideGroupDragPreview()
    {
        if (_groupDragPreview is not null)
        {
            _groupDragPreview.Visibility = Visibility.Collapsed;
        }
    }

    private static void AnimateCanvasLeft(UIElement element, double to, int durationMs, TimeSpan beginTime)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            BeginTime = beginTime,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, element);
        Storyboard.SetTargetProperty(animation, "(Canvas.Left)");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private static void AnimateNewCell(UIElement cell, double toX)
    {
        var storyboard = new Storyboard();
        var slide = new DoubleAnimation
        {
            To = toX,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(slide, cell);
        Storyboard.SetTargetProperty(slide, "(Canvas.Left)");

        var fade = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        Storyboard.SetTarget(fade, cell);
        Storyboard.SetTargetProperty(fade, "Opacity");

        storyboard.Children.Add(slide);
        storyboard.Children.Add(fade);
        storyboard.Begin();
    }

    private static double GetColumnX(int index)
    {
        return BaseX + (index * DefaultColumnWidth);
    }

    private void AnimateHintFadeOut()
    {
        var storyboard = new Storyboard();
        var fade = new DoubleAnimation
        {
            BeginTime = TimeSpan.FromSeconds(4),
            Duration = TimeSpan.FromMilliseconds(450),
            To = 0,
        };
        Storyboard.SetTarget(fade, HintPill);
        Storyboard.SetTargetProperty(fade, "Opacity");
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) => HintPill.Visibility = Visibility.Collapsed;
        storyboard.Begin();
    }

    private void OnViewportPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (IsPointerOnInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _isPanning = true;
        _panStartScreen = e.GetCurrentPoint(Viewport).Position;
        _startTranslateX = WorldTranslateTransform.X;
        _startTranslateY = WorldTranslateTransform.Y;
        Viewport.CapturePointer(e.Pointer);
    }

    private void OnViewportPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var current = e.GetCurrentPoint(Viewport).Position;
        var nextX = _startTranslateX + (current.X - _panStartScreen.X);
        var nextY = _startTranslateY + (current.Y - _panStartScreen.Y);
        WorldTranslateTransform.X = nextX;
        WorldTranslateTransform.Y = nextY;
        ClampWorldTranslation();
    }

    private void OnViewportPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isPanning = false;
        Viewport.ReleasePointerCapture(e.Pointer);
    }

    private static bool IsPointerOnInteractiveControl(DependencyObject? origin)
    {
        while (origin is not null)
        {
            // Do not start pan when pointer originates from interactive inputs/buttons.
            if (origin is TextBox || origin is Button || origin is AutoSuggestBox)
            {
                return true;
            }

            origin = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(origin);
        }

        return false;
    }

    private void OnViewportPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (!ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            return;
        }

        var point = e.GetCurrentPoint(Viewport);
        var delta = point.Properties.MouseWheelDelta > 0 ? ZoomStep : -ZoomStep;
        SetZoomAtPoint(WorldScaleTransform.ScaleX + delta, point.Position);
    }

    private void OnZoomInClicked(object sender, RoutedEventArgs e)
    {
        SetZoomAtPoint(WorldScaleTransform.ScaleX + ZoomStep, new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2));
    }

    private void OnZoomOutClicked(object sender, RoutedEventArgs e)
    {
        SetZoomAtPoint(WorldScaleTransform.ScaleX - ZoomStep, new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2));
    }

    private void OnZoomResetClicked(object sender, RoutedEventArgs e)
    {
        SetZoomAtPoint(1, new Point(Viewport.ActualWidth / 2, Viewport.ActualHeight / 2));
    }

    private void SetZoomAtPoint(double requestedScale, Point viewportPoint)
    {
        var oldScale = WorldScaleTransform.ScaleX;
        var newScale = Math.Clamp(requestedScale, MinZoom, MaxZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001)
        {
            return;
        }

        var worldX = (viewportPoint.X - WorldTranslateTransform.X) / oldScale;
        var worldY = (viewportPoint.Y - WorldTranslateTransform.Y) / oldScale;

        WorldScaleTransform.ScaleX = newScale;
        WorldScaleTransform.ScaleY = newScale;
        WorldTranslateTransform.X = viewportPoint.X - (worldX * newScale);
        WorldTranslateTransform.Y = viewportPoint.Y - (worldY * newScale);
        ClampWorldTranslation();

        SetZoomText();
    }

    private void SetZoomText()
    {
        ZoomText.Text = $"{Math.Round(WorldScaleTransform.ScaleX * 100)}%";
    }

    private void OnTemplateSuggestTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = sender.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            sender.ItemsSource = _templateNames;
            return;
        }

        var matches = _templateNames
            .Where(n => n.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();
        sender.ItemsSource = matches;
    }

    private void OnTemplateSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string name)
        {
            sender.Text = name;
            LoadTemplateIntoCanvas(name);
        }
    }

    private void OnTemplateQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var typedName = args.ChosenSuggestion as string ?? sender.Text?.Trim();
        if (string.IsNullOrWhiteSpace(typedName))
        {
            return;
        }

        LoadTemplateIntoCanvas(typedName);
    }

    private void LoadTemplateIntoCanvas(string name)
    {
        if (!_templateByName.TryGetValue(name, out var template))
        {
            _activeTemplate = null;
            SetTemplateStatus("Template not found.", isError: true);
            return;
        }

        _activeTemplate = template;
        ApplyTemplateToCanvas(template);
        SetTemplateStatus($"Loaded template: {template.Name}");
    }

    private void ApplyTemplateToCanvas(PartTemplateDefinition template)
    {
        var fieldNames = template.Fields
            .OrderBy(f => f.DisplayOrder)
            .Select(f => string.IsNullOrWhiteSpace(f.Label) ? f.Key : f.Label)
            .ToList();

        if (fieldNames.Count == 0)
        {
            fieldNames.Add("Column 1");
        }

        while (_columns.Count < fieldNames.Count)
        {
            var item = new InfiniteCanvasColumnItem
            {
                Width = DefaultColumnWidth,
                Height = DefaultColumnHeight,
                HeaderText = string.Empty,
                CellContent = string.Empty,
            };
            _columns.Add(item);
            var cell = CreateCell(item);
            _cellMap[item] = cell;
            WorldCanvas.Children.Add(cell);
        }

        while (_columns.Count > fieldNames.Count)
        {
            var removeItem = _columns[^1];
            if (_cellMap.TryGetValue(removeItem, out var removeCell))
            {
                WorldCanvas.Children.Remove(removeCell);
                _cellMap.Remove(removeItem);
            }

            _columns.RemoveAt(_columns.Count - 1);
        }

        for (var i = 0; i < fieldNames.Count; i++)
        {
            _columns[i].HeaderText = fieldNames[i];
        }

        ReindexColumns();
        ClampWorldTranslation();
        RefreshPositions(animate: false);
    }

    private async void OnSaveTemplateClicked(object sender, RoutedEventArgs e)
    {
        var templateName = TemplateSuggestBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(templateName))
        {
            SetTemplateStatus("Type a template name before saving.", isError: true);
            return;
        }

        SaveTemplateButton.IsEnabled = false;
        try
        {
            if (_templateById.Count == 0)
            {
                await LoadTemplateSuggestionsAsync();
            }

            var trimmedCount = TrimTrailingEmptyColumnsForSave();
            var fields = _columns
                .Select((c, i) => new TemplateFieldDefinition
                {
                    Key = BuildFieldKey(c.HeaderText, i),
                    Label = string.IsNullOrWhiteSpace(c.HeaderText) ? $"Column {i + 1}" : c.HeaderText.Trim(),
                    Type = TemplateFieldType.Text,
                    IsRequired = false,
                    DisplayOrder = i,
                    ValidationPattern = null,
                    Options = null,
                    LinkedTemplateId = null,
                    LinkedDisplayFieldKey = null,
                })
                .ToList();

            var existingByName = _templateByName.TryGetValue(templateName, out var matchedByName)
                ? matchedByName
                : null;
            var currentTemplate = _activeTemplate is not null
                && _templateById.TryGetValue(_activeTemplate.Id, out var matchedById)
                ? matchedById
                : _activeTemplate;
            var targetTemplate = currentTemplate ?? existingByName;

            var templateToSave = new PartTemplateDefinition
            {
                Id = targetTemplate?.Id ?? Guid.NewGuid().ToString("N"),
                Name = templateName,
                Version = (targetTemplate?.Version ?? 0) + 1,
                IsPublished = targetTemplate?.IsPublished ?? true,
                Fields = fields,
            };

            await _templateSchemaService.SaveTemplateAsync(templateToSave);
            _activeTemplate = templateToSave;
            _templateById[templateToSave.Id] = templateToSave;
            _templateByName[templateName] = templateToSave;
            if (!_templateNames.Contains(templateName, StringComparer.OrdinalIgnoreCase))
            {
                _templateNames.Add(templateName);
                _templateNames.Sort(StringComparer.OrdinalIgnoreCase);
            }

            TemplateSuggestBox.ItemsSource = _templateNames;
            if (trimmedCount > 0)
            {
                SetTemplateStatus($"Saved template: {templateName} ({trimmedCount} empty column(s) removed)");
            }
            else
            {
                SetTemplateStatus($"Saved template: {templateName}");
            }
        }
        catch
        {
            SetTemplateStatus("Failed to save template.", isError: true);
        }
        finally
        {
            SaveTemplateButton.IsEnabled = true;
        }
    }

    private static string BuildFieldKey(string? header, int index)
    {
        var source = string.IsNullOrWhiteSpace(header) ? $"column_{index + 1}" : header.Trim();
        var chars = source
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var normalized = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? $"column_{index + 1}" : normalized;
    }

    private int TrimTrailingEmptyColumnsForSave()
    {
        var removed = 0;
        while (_columns.Count > 1 && IsTrailingColumnDisposable(_columns[^1], _columns.Count))
        {
            var item = _columns[^1];
            if (_cellMap.TryGetValue(item, out var cell))
            {
                WorldCanvas.Children.Remove(cell);
                _cellMap.Remove(item);
            }

            _columns.RemoveAt(_columns.Count - 1);
            removed++;
        }

        if (removed > 0)
        {
            ReindexColumns();
            ClampWorldTranslation();
            RefreshPositions(animate: false);
        }

        return removed;
    }

    private static bool IsTrailingColumnDisposable(InfiniteCanvasColumnItem item, int oneBasedPosition)
    {
        var contentEmpty = string.IsNullOrWhiteSpace(item.CellContent);
        var header = item.HeaderText?.Trim() ?? string.Empty;
        var isDefaultHeader = string.IsNullOrWhiteSpace(header)
                              || string.Equals(header, $"Column {oneBasedPosition}", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(header, "New column", StringComparison.OrdinalIgnoreCase);
        return contentEmpty && isDefaultHeader;
    }

    private void SetTemplateStatus(string message, bool isError = false)
    {
        TemplateStatusText.Text = message;
        TemplateStatusText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources[
            isError ? "DangerBrush" : "TextSecondaryBrush"];
        TemplateStatusText.Visibility = Visibility.Visible;
    }

    private void OnHeaderSelectionRequested(object? sender, int index)
    {
        var ctrlDown = IsCtrlDown();
        var shiftDown = IsShiftDown();

        if (shiftDown && _selectionAnchorIndex.HasValue)
        {
            var start = Math.Min(_selectionAnchorIndex.Value, index);
            var end = Math.Max(_selectionAnchorIndex.Value, index);
            _selectedColumnIndices.Clear();
            for (var i = start; i <= end; i++)
            {
                _selectedColumnIndices.Add(i);
            }
        }
        else if (ctrlDown)
        {
            if (_selectedColumnIndices.Contains(index))
            {
                _selectedColumnIndices.Remove(index);
            }
            else
            {
                _selectedColumnIndices.Add(index);
            }
            _selectionAnchorIndex = index;
        }
        else
        {
            _selectedColumnIndices.Clear();
            _selectedColumnIndices.Add(index);
            _selectionAnchorIndex = index;
        }

        UpdateSelectionVisualState();
    }

    private async void OnGroupColumnsClicked(object sender, RoutedEventArgs e)
    {
        if (_selectedColumnIndices.Count < 2)
        {
            SetTemplateStatus("Select at least 2 columns (tap headers), then click Group.", isError: true);
            return;
        }

        var start = _selectedColumnIndices.Min();
        var end = _selectedColumnIndices.Max();
        var suggestedName = $"Group {_groups.Count + 1}";
        var groupName = await PromptGroupNameAsync(suggestedName);
        if (groupName is null)
        {
            SetTemplateStatus("Grouping cancelled.");
            return;
        }

        CreateGroup(start, end, groupName);
        _selectedColumnIndices.Clear();
        UpdateSelectionVisualState();
    }

    private void CreateGroup(int start, int end, string groupName)
    {
        var groupLabel = new TextBlock
        {
            Text = groupName,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextPrimaryBrush"],
        };
        var groupBorder = new Border
        {
            Height = 22,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(((SolidColorBrush)Application.Current.Resources["AccentPrimaryBrush"]).Color) { Opacity = 0.14 },
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["AccentPrimaryBrush"],
            BorderThickness = new Thickness(1),
            Child = groupLabel,
        };
        groupBorder.RightTapped += OnGroupRightTapped;
        WorldCanvas.Children.Add(groupBorder);
        Canvas.SetZIndex(groupBorder, 4000);
        _groups.Add(new CanvasColumnGroup(start, end, groupName, groupBorder, groupLabel));
        RefreshGroupVisuals();
        SetTemplateStatus($"Created group '{groupName}' for columns {start + 1} to {end + 1}.");
    }

    private void OnClearSelectionClicked(object sender, RoutedEventArgs e)
    {
        _selectedColumnIndices.Clear();
        _selectionAnchorIndex = null;
        UpdateSelectionVisualState();
    }

    private void UpdateSelectionVisualState()
    {
        foreach (var item in _columns)
        {
            if (_cellMap.TryGetValue(item, out var cell))
            {
                cell.IsSelected = _selectedColumnIndices.Contains(item.Index);
            }
        }

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var count = _selectedColumnIndices.Count;
        if (count == 0)
        {
            GroupSelectedButton.IsEnabled = false;
            SelectionInfoHost.Visibility = Visibility.Collapsed;
            return;
        }

        var ordered = _selectedColumnIndices.OrderBy(i => i).ToList();
        var parts = BuildSelectionSegments(ordered);
        SelectionSummaryText.Text = count == 1
            ? $"1 column selected: {parts}"
            : $"{count} columns selected: {parts}";
        GroupSelectedButton.IsEnabled = count >= 2;
        SelectionInfoHost.Visibility = Visibility.Visible;
    }

    private static string BuildSelectionSegments(IReadOnlyList<int> orderedIndices)
    {
        if (orderedIndices.Count == 0)
        {
            return string.Empty;
        }

        var segments = new List<string>();
        var start = orderedIndices[0];
        var previous = orderedIndices[0];
        for (var i = 1; i < orderedIndices.Count; i++)
        {
            var current = orderedIndices[i];
            if (current == previous + 1)
            {
                previous = current;
                continue;
            }

            segments.Add(start == previous ? $"{start + 1}" : $"{start + 1}-{previous + 1}");
            start = current;
            previous = current;
        }

        segments.Add(start == previous ? $"{start + 1}" : $"{start + 1}-{previous + 1}");
        return string.Join(", ", segments);
    }

    private static bool IsCtrlDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private static bool IsShiftDown()
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private void OnGroupRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not Border border)
        {
            return;
        }

        _contextGroup = _groups.FirstOrDefault(g => ReferenceEquals(g.Visual, border));
        if (_contextGroup is null)
        {
            return;
        }

        _groupContextMenu ??= BuildGroupContextMenu();
        _groupContextMenu.ShowAt(border, e.GetPosition(border));
        e.Handled = true;
    }

    private MenuFlyout BuildGroupContextMenu()
    {
        var menu = new MenuFlyout();

        var renameItem = new MenuFlyoutItem { Text = "Rename Group" };
        renameItem.Click += async (_, _) => await RenameContextGroupAsync();
        menu.Items.Add(renameItem);

        var ungroupItem = new MenuFlyoutItem { Text = "Ungroup" };
        ungroupItem.Click += (_, _) => UngroupContextGroup();
        menu.Items.Add(ungroupItem);

        return menu;
    }

    private async Task RenameContextGroupAsync()
    {
        if (_contextGroup is null)
        {
            return;
        }

        var newName = await PromptGroupNameAsync(_contextGroup.Name);
        if (newName is null)
        {
            return;
        }

        _contextGroup.Name = newName;
        _contextGroup.Label.Text = newName;
        SetTemplateStatus($"Renamed group to '{newName}'.");
    }

    private void UngroupContextGroup()
    {
        if (_contextGroup is null)
        {
            return;
        }

        _contextGroup.Visual.RightTapped -= OnGroupRightTapped;
        WorldCanvas.Children.Remove(_contextGroup.Visual);
        _groups.Remove(_contextGroup);
        SetTemplateStatus($"Ungrouped '{_contextGroup.Name}'.");
        _contextGroup = null;
    }

    private async Task<string?> PromptGroupNameAsync(string suggestedName)
    {
        var inputBox = new TextBox
        {
            PlaceholderText = "Enter group name",
            Text = suggestedName,
            SelectionStart = 0,
            SelectionLength = suggestedName.Length,
            MinWidth = 260,
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Create Column Group",
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = inputBox,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var name = inputBox.Text?.Trim();
        return string.IsNullOrWhiteSpace(name) ? suggestedName : name;
    }

    private void RefreshGroupVisuals()
    {
        foreach (var group in _groups)
        {
            var left = GetColumnX(group.StartIndex);
            var width = Math.Max(DefaultColumnWidth, ((group.EndIndex - group.StartIndex) + 1) * DefaultColumnWidth);
            group.Visual.Width = width - 2;
            Canvas.SetLeft(group.Visual, left + 1);
            Canvas.SetTop(group.Visual, BaseY - 26);
        }
    }

    private void OnHeaderDragStarted(object? sender, ColumnHeaderDragEventArgs e)
    {
        var item = _columns.FirstOrDefault(c => c.Index == e.Index);
        if (item is null || !_cellMap.TryGetValue(item, out var cell))
        {
            return;
        }

        _draggingColumn = item;
        _dragOriginalIndex = item.Index;
        _dragTargetIndex = item.Index;
        _dragStartLeft = Canvas.GetLeft(cell);
        _dragStartPointerX = e.PointerX;
        Canvas.SetZIndex(cell, 5000);
    }

    private void OnHeaderDragDelta(object? sender, ColumnHeaderDragEventArgs e)
    {
        if (_draggingColumn is null || !_cellMap.TryGetValue(_draggingColumn, out var draggedCell))
        {
            return;
        }

        var deltaX = e.PointerX - _dragStartPointerX;
        var draggedLeft = _dragStartLeft + deltaX;
        Canvas.SetLeft(draggedCell, draggedLeft);

        var targetIndex = _dragOriginalIndex + (int)Math.Round(deltaX / DefaultColumnWidth, MidpointRounding.AwayFromZero);
        targetIndex = Math.Clamp(targetIndex, 0, _columns.Count - 1);
        if (targetIndex != _dragTargetIndex)
        {
            _dragTargetIndex = targetIndex;
        }

        UpdateDragPreviewPositions();
    }

    private void OnHeaderDragCompleted(object? sender, ColumnHeaderDragEventArgs e)
    {
        if (_draggingColumn is null)
        {
            return;
        }

        var movedItem = _draggingColumn;
        var fromIndex = _dragOriginalIndex;
        var toIndex = _dragTargetIndex;

        _draggingColumn = null;
        _dragOriginalIndex = -1;
        _dragTargetIndex = -1;

        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
        {
            RefreshPositions(animate: false);
            return;
        }

        _columns.RemoveAt(fromIndex);
        _columns.Insert(toIndex, movedItem);
        ReindexColumns();
        ClampWorldTranslation();
        RefreshPositions(animate: true);
    }

    private void UpdateDragPreviewPositions()
    {
        if (_draggingColumn is null)
        {
            return;
        }

        foreach (var item in _columns)
        {
            if (!_cellMap.TryGetValue(item, out var cell))
            {
                continue;
            }

            if (ReferenceEquals(item, _draggingColumn))
            {
                continue;
            }

            var previewIndex = item.Index;
            if (_dragOriginalIndex < _dragTargetIndex && item.Index > _dragOriginalIndex && item.Index <= _dragTargetIndex)
            {
                previewIndex = item.Index - 1;
            }
            else if (_dragOriginalIndex > _dragTargetIndex && item.Index >= _dragTargetIndex && item.Index < _dragOriginalIndex)
            {
                previewIndex = item.Index + 1;
            }

            Canvas.SetLeft(cell, GetColumnX(previewIndex));
            Canvas.SetTop(cell, BaseY);
            Canvas.SetZIndex(cell, previewIndex);
        }
        RefreshGroupVisuals();
    }

    private sealed class CanvasColumnGroup
    {
        public CanvasColumnGroup(int startIndex, int endIndex, string name, Border visual, TextBlock label)
        {
            StartIndex = startIndex;
            EndIndex = endIndex;
            Name = name;
            Visual = visual;
            Label = label;
        }

        public int StartIndex { get; }
        public int EndIndex { get; }
        public string Name { get; set; }
        public Border Visual { get; }
        public TextBlock Label { get; }
    }

}
