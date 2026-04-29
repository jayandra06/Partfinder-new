using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using System.ComponentModel;
using WinRT.Interop;
using Windows.Storage.Pickers;
using System.Linq;
using Microsoft.UI.Dispatching;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    private readonly BackendApiClient _apiClient = App.Services.GetRequiredService<BackendApiClient>();
    private readonly ActivityLogger _activity = App.Services.GetRequiredService<ActivityLogger>();
    private TemplatesViewModel? _boundVm;
    private double _canvasZoom = 1.0;
    private bool _isCanvasPanning;
    private bool _isSpacePanMode;
    private Windows.Foundation.Point _canvasPanLastPoint;
    private double _canvasBaseTranslateX;
    private double _canvasBaseTranslateY;

    public TemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TemplatesViewModel>();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;

        // Ensure drag-pan still works when child controls mark events handled.
        TemplateCanvasScrollViewer.AddHandler(
            UIElement.PointerPressedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnTemplateCanvasPointerPressed),
            true);
        TemplateCanvasScrollViewer.AddHandler(
            UIElement.PointerMovedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnTemplateCanvasPointerMoved),
            true);
        TemplateCanvasScrollViewer.AddHandler(
            UIElement.PointerReleasedEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnTemplateCanvasPointerReleased),
            true);
        TemplateCanvasScrollViewer.AddHandler(
            UIElement.PointerCanceledEvent,
            new Microsoft.UI.Xaml.Input.PointerEventHandler(OnTemplateCanvasPointerCanceled),
            true);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var vm = (TemplatesViewModel)DataContext;

        // IMPORTANT: Load favourite store FIRST before loading templates
        await vm.LoadFavouriteStoreAsync().ConfigureAwait(true);

        // Then load templates
        await vm.LoadAsync().ConfigureAwait(true);
        await FavouritesSubPageControl.ShowAsync(vm).ConfigureAwait(true);

        // Hook template save to log activity
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TemplatesViewModel.IsCreatingTemplate) &&
                !vm.IsCreatingTemplate &&
                vm.SelectedTemplate is not null)
            {
                _activity.LogTemplateChange("Template Saved", $"Template '{vm.SelectedTemplate.Name}' saved with {vm.SelectedTemplate.Fields.Count} fields");
            }

            // When FavouritesStoreVersion changes, update all star icons
            if (args.PropertyName == nameof(TemplatesViewModel.FavouritesStoreVersion))
            {
                // Delay to ensure UI is ready
                DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                {
                    UpdateFavoriteStars();
                });
            }
        };

        // Force initial update of favorite stars after everything is loaded
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            UpdateFavoriteStars();
        });

        // Also add a delayed update to ensure everything is properly loaded
        _ = Task.Delay(500).ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                System.Diagnostics.Debug.WriteLine("Delayed favorite stars update");
                UpdateFavoriteStars();
            });
        });
    }

    private void OnCreateNewTemplateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm)
        {
            return;
        }
        vm.StartNewCustomTemplateCommand.Execute(null);
        vm.ColumnLabels.Clear();
        vm.ColumnLabels.Add(new ColumnLabelDraft());
        RefreshAllCanvasCellBorders();
        CenterCanvasContent();
    }

    private async void OnShowFavouritesInlineClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is TemplatesViewModel vm)
        {
            await FavouritesSubPageControl.ShowAsync(vm).ConfigureAwait(true);
        }
    }

    private void OnTemplateCanvasPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(TemplateCanvasScrollViewer);
        var delta = point.Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        // Scroll up => zoom in, scroll down => zoom out
        var step = delta > 0 ? 0.08 : -0.08;
        SetCanvasZoom(_canvasZoom + step);
        e.Handled = true;
    }

    private void OnTemplateCanvasPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // Never start panning from interactive controls; this keeps input and button clicks reliable.
        if (!_isSpacePanMode && IsFromInteractiveEditor(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var point = e.GetCurrentPoint(TemplateCanvasScrollViewer);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isCanvasPanning = true;
        _canvasPanLastPoint = point.Position;
        TemplateCanvasScrollViewer.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnTemplateCanvasPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isCanvasPanning)
        {
            return;
        }

        var point = e.GetCurrentPoint(TemplateCanvasScrollViewer);
        var dx = point.Position.X - _canvasPanLastPoint.X;
        var dy = point.Position.Y - _canvasPanLastPoint.Y;
        _canvasPanLastPoint = point.Position;

        if (TemplateCanvasTranslateTransform is not null)
        {
            TemplateCanvasTranslateTransform.X += dx;
            TemplateCanvasTranslateTransform.Y += dy;
            ClampCanvasTranslation();
        }
        e.Handled = true;
    }

    private void OnTemplateCanvasPointerReleased(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isCanvasPanning)
        {
            return;
        }

        _isCanvasPanning = false;
        TemplateCanvasScrollViewer.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnTemplateCanvasPointerCanceled(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (!_isCanvasPanning)
        {
            return;
        }

        _isCanvasPanning = false;
        TemplateCanvasScrollViewer.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private static bool IsFromInteractiveEditor(DependencyObject? source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is TextBox || current is PasswordBox || current is ComboBox || current is Button || current is DatePicker)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void SetCanvasZoom(double zoom)
    {
        var clamped = Math.Clamp(zoom, 0.6, 1.8);
        _canvasZoom = Math.Round(clamped, 2);

        if (TemplateCanvasScaleTransform is not null)
        {
            TemplateCanvasScaleTransform.ScaleX = _canvasZoom;
            TemplateCanvasScaleTransform.ScaleY = _canvasZoom;
        }

        ClampCanvasTranslation();
    }

    private void OnTemplateCanvasDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ResetCanvasView();
        e.Handled = true;
    }

    private void OnPageKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space)
        {
            return;
        }

        _isSpacePanMode = true;
        e.Handled = true;
    }

    private void OnPageKeyUp(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Space)
        {
            return;
        }

        _isSpacePanMode = false;
        e.Handled = true;
    }

    private void ResetCanvasView()
    {
        _canvasZoom = 1.0;
        if (TemplateCanvasScaleTransform is not null)
        {
            TemplateCanvasScaleTransform.ScaleX = 1.0;
            TemplateCanvasScaleTransform.ScaleY = 1.0;
        }

        if (TemplateCanvasTranslateTransform is not null)
        {
            TemplateCanvasTranslateTransform.X = 0;
            TemplateCanvasTranslateTransform.Y = 0;
        }
        _canvasBaseTranslateX = 0;
        _canvasBaseTranslateY = 0;
    }

    private void CenterCanvasContent()
    {
        // Run after layout pass so ActualWidth/ActualHeight are valid.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (TemplateCanvasScrollViewer is null || TemplateCanvasItemsControl is null || TemplateCanvasTranslateTransform is null)
            {
                return;
            }

            var viewportWidth = TemplateCanvasScrollViewer.ActualWidth;
            var viewportHeight = TemplateCanvasScrollViewer.ActualHeight;
            var contentWidth = TemplateCanvasItemsControl.ActualWidth * _canvasZoom;
            var contentHeight = TemplateCanvasItemsControl.ActualHeight * _canvasZoom;
            if (viewportWidth <= 0 || viewportHeight <= 0 || contentWidth <= 0 || contentHeight <= 0)
            {
                return;
            }

            TemplateCanvasTranslateTransform.X = (viewportWidth - contentWidth) / 2.0;
            TemplateCanvasTranslateTransform.Y = (viewportHeight - contentHeight) / 2.0;
            _canvasBaseTranslateX = TemplateCanvasTranslateTransform.X;
            _canvasBaseTranslateY = TemplateCanvasTranslateTransform.Y;
        });
    }

    private void SyncCanvasBaseToCurrentTranslation()
    {
        if (TemplateCanvasTranslateTransform is null)
        {
            return;
        }

        _canvasBaseTranslateX = TemplateCanvasTranslateTransform.X;
        _canvasBaseTranslateY = TemplateCanvasTranslateTransform.Y;
    }

    private void ClampCanvasTranslation()
    {
        if (TemplateCanvasTranslateTransform is null || TemplateCanvasItemsControl is null || TemplateCanvasScrollViewer is null)
        {
            return;
        }

        var viewportWidth = TemplateCanvasScrollViewer.ActualWidth;
        var viewportHeight = TemplateCanvasScrollViewer.ActualHeight;
        var contentWidth = TemplateCanvasItemsControl.ActualWidth * _canvasZoom;
        var contentHeight = TemplateCanvasItemsControl.ActualHeight * _canvasZoom;

        if (viewportWidth <= 0 || viewportHeight <= 0 || contentWidth <= 0 || contentHeight <= 0)
        {
            return;
        }

        const double padding = 80;
        var xRange = Math.Max(0, (contentWidth - viewportWidth) / 2) + padding;
        var yRange = Math.Max(0, (contentHeight - viewportHeight) / 2) + padding;

        var minX = _canvasBaseTranslateX - xRange;
        var maxX = _canvasBaseTranslateX + xRange;
        var minY = _canvasBaseTranslateY - yRange;
        var maxY = _canvasBaseTranslateY + yRange;

        TemplateCanvasTranslateTransform.X = Math.Clamp(TemplateCanvasTranslateTransform.X, minX, maxX);
        TemplateCanvasTranslateTransform.Y = Math.Clamp(TemplateCanvasTranslateTransform.Y, minY, maxY);
    }

    private static void SetEdgeButtonOpacity(Border edgeHost, string edgeTag, double opacity, bool enableHitTest)
    {
        if (edgeHost.Child is not Button edgeButton || !Equals(edgeButton.Tag, edgeTag))
        {
            return;
        }
        edgeButton.Opacity = opacity;
        edgeButton.IsHitTestVisible = enableHitTest;
    }

    private void OnCanvasLeftEdgePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border host)
        {
            SetEdgeButtonOpacity(host, "edge-left", 1, true);
        }
    }

    private void OnCanvasLeftEdgePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border host)
        {
            SetEdgeButtonOpacity(host, "edge-left", 0, false);
        }
    }

    private void OnCanvasRightEdgePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border host)
        {
            SetEdgeButtonOpacity(host, "edge-right", 1, true);
        }
    }

    private void OnCanvasRightEdgePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is Border host)
        {
            SetEdgeButtonOpacity(host, "edge-right", 0, false);
        }
    }

    private void OnCanvasInsertLeftClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft } || DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        var index = vm.ColumnLabels.IndexOf(draft);
        if (index < 0)
        {
            return;
        }

        vm.ColumnLabels.Insert(index, new ColumnLabelDraft());
        RefreshAllCanvasCellBorders();
        SyncCanvasBaseToCurrentTranslation();
    }

    private void OnCanvasInsertRightClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft } || DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        vm.InsertColumnAfterCommand.Execute(draft);
        RefreshAllCanvasCellBorders();
        SyncCanvasBaseToCurrentTranslation();
    }

    private void OnAddDropdownOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColumnLabelDraft draft })
        {
            draft.TryAddPendingDropdownOption();
        }
    }

    private void OnDropdownOptionInputKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: ColumnLabelDraft draft })
        {
            draft.TryAddPendingDropdownOption();
            e.Handled = true;
        }
    }

    private void OnRemoveDropdownOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not string option)
        {
            return;
        }

        var draft = FindAncestorDataContext<ColumnLabelDraft>(fe);
        draft?.RemoveDropdownOption(option);
    }

    private void OnCanvasCellBorderLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is Border border)
        {
            ApplyCanvasCellEdgeStyling(border);
        }
    }

    private void RefreshAllCanvasCellBorders()
    {
        foreach (var border in FindVisualChildren<Border>(this))
        {
            if (Equals(border.Tag, "canvas-cell-shell"))
            {
                ApplyCanvasCellEdgeStyling(border);
            }
        }
    }

    private void ApplyCanvasCellEdgeStyling(Border border)
    {
        if (DataContext is not TemplatesViewModel vm || border.DataContext is not ColumnLabelDraft draft)
        {
            return;
        }

        var index = vm.ColumnLabels.IndexOf(draft);
        if (index < 0)
        {
            return;
        }

        var lastIndex = vm.ColumnLabels.Count - 1;
        border.Margin = new Thickness(index == 0 ? 0 : -1, 0, 0, 0);

        if (index == 0 && index == lastIndex)
        {
            border.CornerRadius = new CornerRadius(6);
            return;
        }

        if (index == 0)
        {
            border.CornerRadius = new CornerRadius(6, 0, 0, 6);
            return;
        }

        if (index == lastIndex)
        {
            border.CornerRadius = new CornerRadius(0, 6, 6, 0);
            return;
        }

        border.CornerRadius = new CornerRadius(0);
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T t)
            {
                yield return t;
            }

            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static T? FindAncestorDataContext<T>(DependencyObject? element) where T : class
    {
        var current = element;
        while (current is not null)
        {
            if (current is FrameworkElement fe && fe.DataContext is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private async void OnFavouritesBackRequested(object? sender, EventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        await FavouritesSubPageControl.PlayExitAsync().ConfigureAwait(true);
        vm.HideFavouritesCommand.Execute(null);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_boundVm is not null)
        {
            _boundVm.PropertyChanged -= OnTemplatesVmPropertyChanged;
            _boundVm = null;
        }

        if (DataContext is TemplatesViewModel newVm)
        {
            _boundVm = newVm;
            newVm.PropertyChanged += OnTemplatesVmPropertyChanged;
        }
    }

    private void OnTemplatesVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplatesViewModel.SelectedTemplate) ||
            e.PropertyName == nameof(TemplatesViewModel.ShowTemplatePreviewPanel))
        {
            BuildTemplateChipRow();
        }
        
        if (e.PropertyName == nameof(TemplatesViewModel.FavouritesStoreVersion))
        {
            UpdateFavoriteStars();
        }
    }

    private void UpdateFavoriteStars()
    {
        if (DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        // Find the ListView and update all star buttons
        var listView = FindName("TemplatesList") as ListView;
        if (listView?.Items != null)
        {
            // Force refresh of the ListView items
            foreach (var item in listView.Items)
            {
                if (item is PartTemplateDefinition template)
                {
                    var container = listView.ContainerFromItem(item) as ListViewItem;
                    if (container != null)
                    {
                        UpdateStarForContainer(container, template);
                    }
                }
            }
        }
    }

    private static T? FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
            {
                return result;
            }
            
            var childResult = FindChildOfType<T>(child);
            if (childResult != null)
            {
                return childResult;
            }
        }
        return null;
    }

    private void OnListViewContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue)
            return;

        if (args.Item is PartTemplateDefinition template && args.ItemContainer is ListViewItem container)
        {
            // Delay the star update to ensure favorites are loaded
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                UpdateStarForContainer(container, template);
            });
        }
    }

    private void UpdateStarForContainer(ListViewItem container, PartTemplateDefinition template)
    {
        if (DataContext is not TemplatesViewModel vm)
            return;

        // Find the star button and update it
        var button = FindChildOfType<Button>(container);
        if (button != null)
        {
            var icon = FindChildOfType<FontIcon>(button);
            if (icon != null)
            {
                // Ensure favorites are loaded before checking
                var isFavorite = vm.IsFavouriteFor(template.Id);
                
                // Debug log to check if favorites are working
                System.Diagnostics.Debug.WriteLine($"Template {template.Name} (ID: {template.Id}) - IsFavorite: {isFavorite}");
                
                icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                
                if (isFavorite)
                {
                    icon.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 31, 122, 224)); // #1F7AE0
                }
                else
                {
                    icon.Foreground = Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush;
                }
            }
        }
    }

    private async void OnStarButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is PartTemplateDefinition template)
        {
            if (DataContext is TemplatesViewModel vm)
            {
                try
                {
                    // Call the public method
                    await vm.ToggleFavouritePublicAsync(template.Id);
                    
                    // Force immediate UI update
                    var isFavorite = vm.IsFavouriteFor(template.Id);
                    
                    // Update the star icon immediately after the method completes
                    var icon = FindChildOfType<FontIcon>(button);
                    if (icon != null)
                    {
                        icon.Glyph = isFavorite ? "\uE735" : "\uE734"; // Filled vs outline star
                        
                        // Use theme colors
                        if (isFavorite)
                        {
                            icon.Foreground = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 31, 122, 224)); // #1F7AE0
                        }
                        else
                        {
                            icon.Foreground = Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Star immediately updated for {template.Name}: {(isFavorite ? "Filled" : "Empty")}");
                    }

                    // Also update all other stars to ensure consistency (delayed)
                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        UpdateFavoriteStars();
                    });
                }
                catch (Exception ex)
                {
                    // Log error but don't crash
                    System.Diagnostics.Debug.WriteLine($"Error toggling favorite: {ex.Message}");
                }
            }
        }
    }

    private void OnRemoveColumnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft })
        {
            return;
        }

        if (DataContext is TemplatesViewModel vm)
        {
            vm.RemoveColumnCommand.Execute(draft);
        }
    }

    private void OnInsertDraftColumnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ColumnLabelDraft draft })
        {
            return;
        }

        if (DataContext is TemplatesViewModel vm)
        {
            vm.InsertColumnAfterCommand.Execute(draft);
        }
    }

    private void OnAffordancePointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: true, shownOpacity: 1, hiddenOpacity: 0.35);
        }
    }

    private void OnAffordancePointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: false, shownOpacity: 1, hiddenOpacity: 0.35);
        }
    }

    private void OnFavouritesButtonPointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: true, shownOpacity: 1.0, hiddenOpacity: 0.75);
        }
    }

    private void OnFavouritesButtonPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is UIElement element)
        {
            AffordanceAnimationHelper.Fade(element, show: false, shownOpacity: 1.0, hiddenOpacity: 0.75);
        }
    }

    private async void OnAddContextActionClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm
            || vm.SelectedTemplate is null
            || XamlRoot is null)
        {
            return;
        }

        var sourceTemplate = vm.SelectedTemplate;
        var rulesList = new List<RuleRowUi>();

        var sourceColCb = new ComboBox
        {
            DisplayMemberPath = "Label",
            SelectedValuePath = "Key",
            ItemsSource = sourceTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var menuLabelTb = new TextBox
        {
            PlaceholderText = "e.g. View suppliers",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var targetTemplateCb = new ComboBox
        {
            DisplayMemberPath = "Name",
            SelectedValuePath = "Id",
            ItemsSource = vm.Templates.ToList(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        PartTemplateDefinition? currentTarget = null;

        var rulesPanel = new StackPanel { Spacing = 8 };

        void RebindTargetFieldCombos()
        {
            var fields = currentTarget?.Fields.OrderBy(f => f.DisplayOrder).ToList()
                         ?? new List<TemplateFieldDefinition>();
            foreach (var row in rulesList)
            {
                row.TargetField.ItemsSource = fields;
                row.TargetField.SelectedItem = null;
            }
        }

        targetTemplateCb.SelectionChanged += (_, _) =>
        {
            currentTarget = targetTemplateCb.SelectedItem as PartTemplateDefinition;
            RebindTargetFieldCombos();
        };

        void AddRuleRow()
        {
            var targetField = new ComboBox
            {
                DisplayMemberPath = "Label",
                SelectedValuePath = "Key",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var sourceField = new ComboBox
            {
                DisplayMemberPath = "Label",
                SelectedValuePath = "Key",
                ItemsSource = sourceTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList(),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var literalTb = new TextBox
            {
                PlaceholderText = "Optional fixed value (overrides source column)",
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            var grid = new Grid
            {
                ColumnSpacing = 8,
                RowSpacing = 6,
                Margin = new Thickness(0, 4, 0, 0),
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition());
            grid.RowDefinitions.Add(new RowDefinition());

            var h1 = new TextBlock { Text = "Target field (other template)" };
            var h2 = new TextBlock { Text = "Source field (this row)" };
            var h3 = new TextBlock { Text = "Or literal" };
            Grid.SetColumn(h1, 0);
            Grid.SetRow(h1, 0);
            Grid.SetColumn(h2, 1);
            Grid.SetRow(h2, 0);
            Grid.SetColumn(h3, 2);
            Grid.SetRow(h3, 0);
            Grid.SetColumn(targetField, 0);
            Grid.SetRow(targetField, 1);
            Grid.SetColumn(sourceField, 1);
            Grid.SetRow(sourceField, 1);
            Grid.SetColumn(literalTb, 2);
            Grid.SetRow(literalTb, 1);
            grid.Children.Add(h1);
            grid.Children.Add(h2);
            grid.Children.Add(h3);
            grid.Children.Add(targetField);
            grid.Children.Add(sourceField);
            grid.Children.Add(literalTb);
            rulesPanel.Children.Add(grid);

            var rowUi = new RuleRowUi(targetField, sourceField, literalTb);
            rulesList.Add(rowUi);
            if (currentTarget is not null)
            {
                targetField.ItemsSource = currentTarget.Fields.OrderBy(f => f.DisplayOrder).ToList();
            }
        }

        var addRuleBtn = new Button
        {
            Content = "Add AND rule",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        addRuleBtn.Click += (_, _) => AddRuleRow();

        var displayTb = new TextBox
        {
            PlaceholderText = "Optional: comma-separated field keys to show in the popup (empty = all columns)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(new TextBlock { Text = "Column (menu appears on right-click here)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(sourceColCb);
        root.Children.Add(new TextBlock { Text = "Menu label", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(menuLabelTb);
        root.Children.Add(new TextBlock { Text = "Target template", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(targetTemplateCb);
        root.Children.Add(new TextBlock { Text = "Match rules (all must match)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(rulesPanel);
        root.Children.Add(addRuleBtn);
        root.Children.Add(new TextBlock { Text = "Display columns (optional)", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        root.Children.Add(displayTb);

        var other = vm.Templates.FirstOrDefault(t => t.Id != sourceTemplate.Id);
        if (other is not null)
        {
            targetTemplateCb.SelectedItem = other;
        }

        AddRuleRow();

        var dlg = new ContentDialog
        {
            Title = "New cell action",
            Content = new ScrollViewer
            {
                MaxHeight = 520,
                Content = root,
            },
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (sourceColCb.SelectedValue is not string sourceKey || string.IsNullOrWhiteSpace(sourceKey))
        {
            await ShowSimpleDialogAsync("Choose a column for this action.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var menuLabel = menuLabelTb.Text.Trim();
        if (menuLabel.Length == 0)
        {
            await ShowSimpleDialogAsync("Enter a menu label.", XamlRoot).ConfigureAwait(true);
            return;
        }

        if (targetTemplateCb.SelectedValue is not string targetId || string.IsNullOrWhiteSpace(targetId))
        {
            await ShowSimpleDialogAsync("Choose a target template.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var rules = new List<ContextActionMatchRule>();
        foreach (var row in rulesList)
        {
            if (row.TargetField.SelectedItem is not TemplateFieldDefinition tf)
            {
                continue;
            }

            var lit = row.Literal.Text.Trim();
            if (lit.Length > 0)
            {
                rules.Add(
                    new ContextActionMatchRule
                    {
                        TargetFieldKey = tf.Key,
                        LiteralValue = lit,
                        SourceFieldKey = null,
                    });
                continue;
            }

            if (row.SourceField.SelectedItem is not TemplateFieldDefinition sf)
            {
                continue;
            }

            rules.Add(
                new ContextActionMatchRule
                {
                    TargetFieldKey = tf.Key,
                    SourceFieldKey = sf.Key,
                    LiteralValue = null,
                });
        }

        if (rules.Count == 0)
        {
            await ShowSimpleDialogAsync("Add at least one complete rule (target field + source field, or target field + literal).", XamlRoot)
                .ConfigureAwait(true);
            return;
        }

        IReadOnlyList<string>? displayKeys = null;
        var rawDisplay = displayTb.Text.Trim();
        if (rawDisplay.Length > 0)
        {
            displayKeys = rawDisplay
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => s.Length > 0)
                .ToList();
        }

        var action = new TemplateContextAction
        {
            Id = Guid.NewGuid().ToString("N"),
            SourceTemplateId = sourceTemplate.Id,
            SourceFieldKey = sourceKey,
            MenuLabel = menuLabel,
            TargetTemplateId = targetId,
            MatchRules = rules,
            DisplayFieldKeys = displayKeys,
        };

        try
        {
            await vm.SaveNewContextActionAsync(action).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            await ShowSimpleDialogAsync(ex.Message, XamlRoot).ConfigureAwait(true);
        }
    }

    private static async Task ShowSimpleDialogAsync(string message, XamlRoot xamlRoot)
    {
        var err = new ContentDialog
        {
            Title = "Cell action",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
            XamlRoot = xamlRoot,
        };
        _ = await err.ShowAsync();
    }

    private sealed class RuleRowUi
    {
        public RuleRowUi(ComboBox targetField, ComboBox sourceField, TextBox literalTb)
        {
            TargetField = targetField;
            SourceField = sourceField;
            Literal = literalTb;
        }

        public ComboBox TargetField { get; }
        public ComboBox SourceField { get; }
        public TextBox Literal { get; }
    }

    private void BuildTemplateChipRow()
    {
        TemplateChipRowPanel.Children.Clear();
        if (DataContext is not TemplatesViewModel vm || vm.SelectedTemplate is null || !vm.ShowTemplatePreviewPanel)
        {
            return;
        }

        var fields = vm.SelectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        AddInsertButton(vm, 0);
        for (var i = 0; i < fields.Count; i++)
        {
            var field = fields[i];

            // Type badge
            var typeBadge = new Border
            {
                Background = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"],
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = field.Type.ToString().ToUpperInvariant(),
                    FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["AccentPrimaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center,
                }
            };

            // Close button
            var closeBtn = new Button
            {
                Content = "\uE711",
                FontFamily = (FontFamily)Application.Current.Resources["SymbolThemeFontFamily"],
                Padding = new Thickness(4),
                Width = 22,
                Height = 22,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0)),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 9,
                Tag = i,
                Opacity = 0.6,
            };
            closeBtn.Click += OnTemplateChipRemoveClick;

            // Inner row: label + badge + close
            var innerRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };
            innerRow.Children.Add(new TextBlock
            {
                Text = field.Label,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            innerRow.Children.Add(typeBadge);
            innerRow.Children.Add(closeBtn);

            // Chip card
            var card = new Border
            {
                Height = 36,
                Margin = new Thickness(0),
                Padding = new Thickness(10, 0, 4, 0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
                Background = (Brush)Application.Current.Resources["ElevatedCardBackgroundBrush"],
                VerticalAlignment = VerticalAlignment.Center,
                Child = innerRow,
            };

            TemplateChipRowPanel.Children.Add(card);
            AddInsertButton(vm, i + 1);
        }
    }

    private void AddInsertButton(TemplatesViewModel vm, int insertIndex)
    {
        var btn = new Button
        {
            Content = "+",
            Width = 24,
            Height = 24,
            Margin = new Thickness(3, 0, 3, 0),
            Tag = insertIndex,
            Opacity = 0.3,
            Padding = new Thickness(0),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.PointerEntered += (_, _) => AffordanceAnimationHelper.Fade(btn, show: true, shownOpacity: 1, hiddenOpacity: 0.35);
        btn.PointerExited += (_, _) => AffordanceAnimationHelper.Fade(btn, show: false, shownOpacity: 1, hiddenOpacity: 0.35);
        btn.Click += async (_, _) =>
        {
            if (XamlRoot is null)
            {
                return;
            }

            var input = new TextBox { PlaceholderText = "Column name" };
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Add column",
                Content = input,
                PrimaryButtonText = "Add",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
            };
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary || btn.Tag is not int idx)
            {
                return;
            }

            await vm.InsertColumnIntoSelectedTemplateAsync(idx, input.Text).ConfigureAwait(true);
            BuildTemplateChipRow();
        };
        TemplateChipRowPanel.Children.Add(btn);
    }

    private async void OnTemplateChipRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int index } || DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        await vm.RemoveColumnFromSelectedTemplateAsync(index).ConfigureAwait(true);
        BuildTemplateChipRow();
    }

    private async void OnImportCsvClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm || vm.SelectedTemplate is null || XamlRoot is null || App.MainAppWindow is null)
        {
            return;
        }
        SetImportStatus(null);

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var headers = await ReadCsvHeadersAsync(file.Path).ConfigureAwait(true);
        if (headers.Count == 0)
        {
            await ShowSimpleDialogAsync("Could not detect CSV headers.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var headerMap = await ShowHeaderMappingDialogAsync(vm.SelectedTemplate, headers).ConfigureAwait(true);
        if (headerMap is null)
        {
            return;
        }

        var (ok, error, jobId) = await _apiClient
            .StartTemplateImportAsync(vm.SelectedTemplate.Id, file.Path, headerMap)
            .ConfigureAwait(true);
        if (!ok || string.IsNullOrWhiteSpace(jobId))
        {
            SetImportStatus("Import failed to start.", error ?? "Unknown startup error.", isActive: false);
            await ShowSimpleDialogAsync(error ?? "Failed to start import.", XamlRoot).ConfigureAwait(true);
            return;
        }

        SetImportStatus("Import started...", $"Job: {jobId}", isActive: true);
        var status = await PollImportStatusAsync(vm.SelectedTemplate.Id).ConfigureAwait(true);
        if (status is null)
        {
            SetImportStatus("Import status unavailable.", "The server did not return a final status in time.", isActive: false);
            await ShowSimpleDialogAsync("Import started but status could not be read.", XamlRoot).ConfigureAwait(true);
            return;
        }

        var summary = $"Status: {status.Status}\nProcessed: {status.ProcessedRows}/{status.TotalRows}\nFailed: {status.FailedRows}";
        if (status.Errors.Count > 0)
        {
            summary += $"\nErrors:\n- {string.Join("\n- ", status.Errors)}";
        }

        var finalHeadline = string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase)
            ? "Import completed."
            : "Import finished with issues.";
        SetImportStatus(finalHeadline, $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.", isActive: false);
        await ShowSimpleDialogAsync(summary, XamlRoot).ConfigureAwait(true);
    }

    private static async Task<List<string>> ReadCsvHeadersAsync(string path)
    {
        string? firstLine;
        using (var reader = new StreamReader(path))
        {
            firstLine = await reader.ReadLineAsync().ConfigureAwait(true);
        }

        firstLine ??= string.Empty;
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return [];
        }

        return firstLine
            .Split(',', StringSplitOptions.TrimEntries)
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .ToList();
    }

    private async Task<Dictionary<string, string>?> ShowHeaderMappingDialogAsync(
        PartTemplateDefinition selectedTemplate,
        IReadOnlyList<string> headers)
    {
        if (XamlRoot is null)
        {
            return null;
        }

        var templateFields = selectedTemplate.Fields.OrderBy(f => f.DisplayOrder).ToList();
        var rows = new List<(string Header, ComboBox Combo)>();
        var panel = new StackPanel { Spacing = 8 };
        foreach (var header in headers)
        {
            var combo = new ComboBox
            {
                Width = 260,
                ItemsSource = templateFields,
                DisplayMemberPath = "Label",
                SelectedValuePath = "Label",
                PlaceholderText = "Skip this header",
            };

            var auto = templateFields.FirstOrDefault(
                f => string.Equals(f.Label, header, StringComparison.OrdinalIgnoreCase));
            if (auto is not null)
            {
                combo.SelectedItem = auto;
            }

            rows.Add((header, combo));
            panel.Children.Add(
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Width = 240, Text = header, VerticalAlignment = VerticalAlignment.Center },
                        combo,
                    },
                });
        }

        var dialog = new ContentDialog
        {
            Title = "Map CSV headers",
            Content = new ScrollViewer { MaxHeight = 420, Content = panel },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return null;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Combo.SelectedItem is TemplateFieldDefinition field)
            {
                map[row.Header] = field.Label;
            }
        }

        return map;
    }

    private async Task<ImportStatusDto?> PollImportStatusAsync(string templateId)
    {
        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(1000).ConfigureAwait(true);
            var (ok, _, status) = await _apiClient.GetTemplateImportStatusAsync(templateId).ConfigureAwait(true);
            if (!ok || status is null)
            {
                continue;
            }

            SetImportStatus(
                $"Import {status.Status}...",
                $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.",
                isActive: true);

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        return null;
    }

    private void SetImportStatus(string? headline, string? detail = null, bool isActive = false)
    {
        if (ImportStatusPanel is null || ImportProgressRing is null || ImportStatusText is null || ImportStatusDetailText is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            ImportStatusPanel.Visibility = Visibility.Collapsed;
            ImportProgressRing.IsActive = false;
            ImportStatusText.Text = string.Empty;
            ImportStatusDetailText.Text = string.Empty;
            return;
        }

        ImportStatusPanel.Visibility = Visibility.Visible;
        ImportProgressRing.IsActive = isActive;
        ImportStatusText.Text = headline;
        ImportStatusDetailText.Text = detail ?? string.Empty;
    }
}
