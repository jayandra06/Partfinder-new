using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.Foundation;
using Windows.System;

namespace PartFinder.Views.Components;

public sealed partial class FloatingChatbotControl : UserControl
{
    private const double ButtonSize = 56;
    private const double EdgeMargin = 20;
    private const double DragThresholdPx = 5;
    private const double PanelWidth = 360;
    private const double PanelHeight = 460;
    private const double PanelGap = 12;

    private readonly ChatbotStateService _state;
    private readonly ChatbotViewModel _viewModel;
    private readonly IThemeService? _themeService;

    private bool _isPointerDown;
    private bool _isDragging;
    private Point _dragStartPointer;
    private double _dragStartLeft;
    private double _dragStartTop;
    private double _currentLeft;
    private double _currentTop;
    private bool _hasLaidOut;

    public FloatingChatbotControl()
    {
        InitializeComponent();

        _state = App.Services.GetRequiredService<ChatbotStateService>();
        _viewModel = App.Services.GetRequiredService<ChatbotViewModel>();
        _themeService = App.Services.GetService<IThemeService>();

        DataContext = _viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.Messages.CollectionChanged += OnMessagesCollectionChanged;
        if (_themeService is not null)
        {
            _themeService.ThemeChanged += OnThemeChanged;
        }

        ApplyThemeBrushes();
        SyncPanelVisibility();
        ApplySavedOrDefaultPosition();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Messages.CollectionChanged -= OnMessagesCollectionChanged;
        if (_themeService is not null)
        {
            _themeService.ThemeChanged -= OnThemeChanged;
        }
    }

    private void OnThemeChanged(object? sender, EventArgs e) => ApplyThemeBrushes();

    private void ApplyThemeBrushes()
    {
        var res = Application.Current.Resources;
        if (res.TryGetValue("ElevatedCardBackgroundBrush", out var elevated) && elevated is Brush elevatedBrush)
        {
            ChatPanel.Background = elevatedBrush;
        }

        if (res.TryGetValue("GlassStrokeBrush", out var stroke) && stroke is Brush strokeBrush)
        {
            ChatPanel.BorderBrush = strokeBrush;
        }

        if (res.TryGetValue("CardBackgroundBrush", out var card) && card is Brush cardBrush)
        {
            ChatHeaderBorder.Background = cardBrush;
            ChatInputBorder.Background = cardBrush;
        }

        if (res.TryGetValue("BorderDefaultBrush", out var border) && border is Brush borderBrush)
        {
            ChatHeaderBorder.BorderBrush = borderBrush;
            ChatInputBorder.BorderBrush = borderBrush;
        }

        if (res.TryGetValue("TextPrimaryBrush", out var primary) && primary is Brush primaryBrush)
        {
            ChatTitleText.Foreground = primaryBrush;
            ClearIcon.Foreground = primaryBrush;
            CloseIcon.Foreground = primaryBrush;
        }

        if (res.TryGetValue("TextSecondaryBrush", out var secondary) && secondary is Brush secondaryBrush)
        {
            ChatSubtitleText.Foreground = secondaryBrush;
            ThinkingText.Foreground = secondaryBrush;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatbotViewModel.IsOpen))
        {
            SyncPanelVisibility();
        }
    }

    private void OnMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            MessagesScroll.UpdateLayout();
            MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null, disableAnimation: false);
        });
    }

    private void SyncPanelVisibility()
    {
        ChatPanel.Visibility = _viewModel.IsOpen ? Visibility.Visible : Visibility.Collapsed;
        if (_viewModel.IsOpen)
        {
            UpdateChatPanelPosition();
            DispatcherQueue.TryEnqueue(() => MessageInputBox.Focus(FocusState.Programmatic));
        }
    }

    private void OnOverlayCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width <= 0 || e.NewSize.Height <= 0)
        {
            return;
        }

        ApplySavedOrDefaultPosition();
    }

    private void ApplySavedOrDefaultPosition()
    {
        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width <= ButtonSize || height <= ButtonSize)
        {
            return;
        }

        _state.EnsureLoaded();
        var maxLeft = Math.Max(EdgeMargin, width - ButtonSize - EdgeMargin);
        var maxTop = Math.Max(EdgeMargin, height - ButtonSize - EdgeMargin);

        double left;
        double top;
        if (_state.ButtonXRatio is { } xr && _state.ButtonYRatio is { } yr)
        {
            left = EdgeMargin + (maxLeft - EdgeMargin) * Math.Clamp(xr, 0d, 1d);
            top = EdgeMargin + (maxTop - EdgeMargin) * Math.Clamp(yr, 0d, 1d);
        }
        else
        {
            // Initial default: bottom-right corner
            left = maxLeft;
            top = maxTop;
        }

        SetButtonPosition(left, top);
        _hasLaidOut = true;
    }

    private void SetButtonPosition(double left, double top)
    {
        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width > ButtonSize && height > ButtonSize)
        {
            var maxLeft = Math.Max(EdgeMargin, width - ButtonSize - EdgeMargin);
            var maxTop = Math.Max(EdgeMargin, height - ButtonSize - EdgeMargin);
            left = Math.Clamp(left, EdgeMargin, maxLeft);
            top = Math.Clamp(top, EdgeMargin, maxTop);
        }

        _currentLeft = left;
        _currentTop = top;
        Canvas.SetLeft(FloatingButtonContainer, left);
        Canvas.SetTop(FloatingButtonContainer, top);

        if (_viewModel.IsOpen)
        {
            UpdateChatPanelPosition();
        }
    }

    private void UpdateChatPanelPosition()
    {
        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Open to the left of the button's right edge if near right side, otherwise align left edge
        var panelLeft = _currentLeft + ButtonSize - PanelWidth;
        if (panelLeft < EdgeMargin)
        {
            panelLeft = _currentLeft;
        }

        panelLeft = Math.Clamp(panelLeft, EdgeMargin, Math.Max(EdgeMargin, width - PanelWidth - EdgeMargin));

        // Prefer opening above the button; fall back to below if insufficient space above
        var panelTop = _currentTop - PanelHeight - PanelGap;
        if (panelTop < EdgeMargin)
        {
            var belowTop = _currentTop + ButtonSize + PanelGap;
            panelTop = belowTop + PanelHeight <= height - EdgeMargin
                ? belowTop
                : Math.Max(EdgeMargin, height - PanelHeight - EdgeMargin);
        }

        Canvas.SetLeft(ChatPanel, panelLeft);
        Canvas.SetTop(ChatPanel, panelTop);
    }

    private void OnFabPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        FloatingButtonContainer.Translation = new Vector3(0, 0, 28);
        if (Resources.TryGetValue("FabHoverInStoryboard", out var sbObj) && sbObj is Storyboard sb)
        {
            sb.Begin();
        }
    }

    private void OnFabPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging)
        {
            FloatingButtonContainer.Translation = new Vector3(0, 0, 16);
        }

        if (Resources.TryGetValue("FabHoverOutStoryboard", out var sbObj) && sbObj is Storyboard sb)
        {
            sb.Begin();
        }
    }

    private void OnFabPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse
            && !e.GetCurrentPoint(FloatingButtonContainer).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPointerDown = true;
        _isDragging = false;
        _dragStartPointer = e.GetCurrentPoint(OverlayCanvas).Position;
        _dragStartLeft = _hasLaidOut ? _currentLeft : Canvas.GetLeft(FloatingButtonContainer);
        _dragStartTop = _hasLaidOut ? _currentTop : Canvas.GetTop(FloatingButtonContainer);
        if (double.IsNaN(_dragStartLeft)) _dragStartLeft = 0;
        if (double.IsNaN(_dragStartTop)) _dragStartTop = 0;

        FloatingButtonContainer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnFabPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown)
        {
            return;
        }

        var current = e.GetCurrentPoint(OverlayCanvas).Position;
        var dx = current.X - _dragStartPointer.X;
        var dy = current.Y - _dragStartPointer.Y;

        if (!_isDragging && (Math.Abs(dx) > DragThresholdPx || Math.Abs(dy) > DragThresholdPx))
        {
            _isDragging = true;
            FloatingButtonContainer.Translation = new Vector3(0, 0, 28);
        }

        if (_isDragging)
        {
            SetButtonPosition(_dragStartLeft + dx, _dragStartTop + dy);
            e.Handled = true;
        }
    }

    private void OnFabPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPointerDown)
        {
            return;
        }

        var wasDragging = _isDragging;
        _isPointerDown = false;
        _isDragging = false;
        FloatingButtonContainer.ReleasePointerCapture(e.Pointer);
        FloatingButtonContainer.Translation = new Vector3(0, 0, 16);

        if (wasDragging)
        {
            PersistCurrentPosition();
        }
        else
        {
            _viewModel.ToggleOpenCommand.Execute(null);
        }

        e.Handled = true;
    }

    private void OnFabPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isPointerDown = false;
        _isDragging = false;
        FloatingButtonContainer.Translation = new Vector3(0, 0, 16);
    }

    private void PersistCurrentPosition()
    {
        var width = OverlayCanvas.ActualWidth;
        var height = OverlayCanvas.ActualHeight;
        if (width <= ButtonSize || height <= ButtonSize)
        {
            return;
        }

        var maxLeft = Math.Max(EdgeMargin, width - ButtonSize - EdgeMargin);
        var maxTop = Math.Max(EdgeMargin, height - ButtonSize - EdgeMargin);
        var xRange = maxLeft - EdgeMargin;
        var yRange = maxTop - EdgeMargin;
        var xRatio = xRange > 0 ? (_currentLeft - EdgeMargin) / xRange : 1d;
        var yRatio = yRange > 0 ? (_currentTop - EdgeMargin) / yRange : 1d;
        _state.SavePosition(xRatio, yRatio);
    }

    private void OnSendClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SendMessageCommand.CanExecute(null))
        {
            _viewModel.SendMessageCommand.Execute(null);
        }
    }

    private void OnClearChatClick(object sender, RoutedEventArgs e) =>
        _viewModel.ClearChatCommand.Execute(null);

    private void OnCloseChatClick(object sender, RoutedEventArgs e) =>
        _viewModel.CloseCommand.Execute(null);

    private void OnMessageInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && _viewModel.SendMessageCommand.CanExecute(null))
        {
            e.Handled = true;
            _viewModel.SendMessageCommand.Execute(null);
        }
    }
}
