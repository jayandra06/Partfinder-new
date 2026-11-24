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
using Windows.Storage;
using Windows.Storage.Pickers;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Navigation;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page, INavigationParameterSink
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
    private bool _bulkUploadRunning;
    private bool _acceptBulkProgress;
    private int _bulkUploaded;
    private int _bulkUploadTotal;
    private bool _initialLoadComplete;
    private string? _pendingWorkspaceTemplateId;
    private bool _pendingWorkspaceViewOnly;

    public TemplatesPage()
    {
        System.Diagnostics.Debug.WriteLine("[TemplatesPage] Constructor started.");
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TemplatesViewModel>();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        AllTemplatesCarouselControl.TemplateDoubleTapped += OnTemplateDoubleTapped;
        AllTemplatesCarouselControl.TemplateEditRequested += OnTemplateEditRequested;
        FavouritesSubPageControl.TemplateEditRequested += OnTemplateEditRequested;
        TemplateComposerControl.Closed += OnTemplateComposerClosed;
        TemplateComposerControl.Saved += OnTemplateComposerSaved;

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

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!TryParseWorkspaceNavigation(e.Parameter, out var templateId, out var viewOnly))
        {
            return;
        }

        if (_initialLoadComplete)
        {
            _ = OpenTemplateWorkspaceAsync(templateId, viewOnly);
        }
        else
        {
            _pendingWorkspaceTemplateId = templateId;
            _pendingWorkspaceViewOnly = viewOnly;
        }
    }

    private static bool TryParseWorkspaceNavigation(object? parameter, out string templateId, out bool viewOnly)
    {
        templateId = string.Empty;
        viewOnly = false;
        switch (parameter)
        {
            case TemplateWorkspaceNavigationArgs args when !string.IsNullOrWhiteSpace(args.TemplateId):
                templateId = args.TemplateId;
                viewOnly = args.ViewOnly;
                return true;
            case string id when !string.IsNullOrWhiteSpace(id):
                templateId = id;
                return true;
            default:
                return false;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[TemplatesPage] OnLoaded triggered.");
        var vm = (TemplatesViewModel)DataContext;
        var appState = App.Services.GetRequiredService<IAppStateStore>();

        var pendingOpenId = _pendingWorkspaceTemplateId ?? appState.TemplatesPendingOpenTemplateId;
        var pendingViewOnly = _pendingWorkspaceViewOnly || appState.TemplatesPendingOpenViewOnly;
        _pendingWorkspaceTemplateId = null;
        _pendingWorkspaceViewOnly = false;
        appState.TemplatesPendingOpenTemplateId = null;
        appState.TemplatesPendingOpenViewOnly = false;

        if (!string.IsNullOrWhiteSpace(pendingOpenId))
        {
            await OpenTemplateWorkspaceAsync(pendingOpenId, pendingViewOnly).ConfigureAwait(true);
        }

        // IMPORTANT: Load favourite store FIRST before loading templates
        await vm.LoadFavouriteStoreAsync().ConfigureAwait(true);

        // Then load templates (carousel); composer can open before this finishes
        await vm.LoadAsync().ConfigureAwait(true);
        vm.Templates.CollectionChanged += (_, _) =>
        {
            if (AllTemplatesPanel.Visibility == Visibility.Visible)
            {
                RefreshTemplatesListSource();
            }
        };
        RefreshTemplatesListSource();

        var pending = appState.TemplatesPendingAction;
        if (string.Equals(pending, "Create", StringComparison.Ordinal))
        {
            appState.TemplatesPendingAction = null;
            OnCreateNewTemplateClick(this, new RoutedEventArgs());
        }

        _initialLoadComplete = true;

        // Hook template save to log activity
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(TemplatesViewModel.IsCreatingTemplate) && vm.IsCreatingTemplate)
            {
                HideTemplateBrowsingPanels();
            }

            if (args.PropertyName == nameof(TemplatesViewModel.IsCreatingTemplate) && !vm.IsCreatingTemplate)
            {
                AllTemplatesPanel.Visibility = Visibility.Visible;
                RefreshTemplatesListSource();
            }

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

        // Update favourite stars once layout is ready
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            UpdateFavoriteStars();
        });
    }

    private async void OnCreateNewTemplateClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm || XamlRoot is null)
        {
            return;
        }

        CloseTemplateComposer();
        var nameBox = new TextBox { PlaceholderText = "Template name" };
        var dialog = new ContentDialog
        {
            Title = "Create template",
            Content = nameBox,
            PrimaryButtonText = "Create",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var error = await vm.CreateDefaultTableTemplateAsync(nameBox.Text);
        if (error is null)
        {
            if (!string.IsNullOrWhiteSpace(vm.LastCreatedTemplateId))
            {
                await ShowTemplateComposerAsync(vm.LastCreatedTemplateId, isCreating: true);
            }

            return;
        }

        var errorDialog = new ContentDialog
        {
            Title = "Could not create template",
            Content = error,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await errorDialog.ShowAsync();
    }

    public void ReceiveNavigationParameter(object? parameter)
    {
        if (!TryParseWorkspaceNavigation(parameter, out var templateId, out var viewOnly))
        {
            return;
        }

        var appState = App.Services.GetRequiredService<IAppStateStore>();
        appState.TemplatesPendingOpenTemplateId = null;
        appState.TemplatesPendingOpenViewOnly = false;
        if (_initialLoadComplete)
        {
            _ = OpenTemplateWorkspaceAsync(templateId, viewOnly);
        }
        else
        {
            _pendingWorkspaceTemplateId = templateId;
            _pendingWorkspaceViewOnly = viewOnly;
        }
    }

    private async Task OpenTemplateWorkspaceAsync(string templateId, bool viewOnly = false) =>
        await ShowTemplateComposerAsync(templateId, isCreating: false, viewOnly: viewOnly);

    private void OnTemplateDoubleTapped(object? sender, string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        App.Services.GetRequiredService<INavigationService>().Navigate(AppPage.TemplateData, templateId);
    }

    private async void OnTemplateEditRequested(object? sender, string templateId) =>
        await OpenTemplateWorkspaceAsync(templateId, viewOnly: false);

    private async Task ShowTemplateComposerAsync(string templateId, bool isCreating, bool viewOnly = false)
    {
        TemplatesBrowseHeader.Visibility = Visibility.Collapsed;
        AllTemplatesPanel.Visibility = Visibility.Collapsed;
        FavouritesSubPageControl.Visibility = Visibility.Collapsed;
        AllTemplatesCarouselControl.Visibility = Visibility.Collapsed;
        EmptyTemplatesHint.Visibility = Visibility.Collapsed;
        TemplateComposerControl.Visibility = Visibility.Visible;
        await TemplateComposerControl.OpenAsync(templateId, isCreating, viewOnly);
    }

    private void OnTemplateComposerClosed(object? sender, EventArgs e)
    {
        CloseTemplateComposer();
        if (DataContext is TemplatesViewModel vm)
        {
            _ = ReloadTemplatesAfterComposerAsync(vm);
        }
    }

    private async void OnTemplateComposerSaved(object? sender, EventArgs e)
    {
        if (DataContext is TemplatesViewModel vm)
        {
            await vm.LoadAsync().ConfigureAwait(true);
        }
    }

    private void CloseTemplateComposer()
    {
        TemplateComposerControl.Visibility = Visibility.Collapsed;
        TemplatesBrowseHeader.Visibility = Visibility.Visible;
        RefreshTemplatesListSource();
    }

    private async Task ReloadTemplatesAfterComposerAsync(TemplatesViewModel vm)
    {
        await vm.LoadAsync();
        RefreshTemplatesListSource();
    }

    private async void OnBulkUploadClick(object sender, RoutedEventArgs e)
    {
        if (_bulkUploadRunning || DataContext is not TemplatesViewModel vm || XamlRoot is null || App.MainAppWindow is null)
        {
            return;
        }

        _bulkUploadRunning = true;
        BulkUploadButton.IsEnabled = false;
        _acceptBulkProgress = false;
        _bulkUploaded = 0;
        _bulkUploadTotal = 0;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".xlsx");
        picker.FileTypeFilter.Add(".xlsm");
        picker.FileTypeFilter.Add(".csv");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));

        string? tempPath = null;
        try
        {
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            SetImportStatus("Reading file…", file.Name, isActive: true);
            var path = await ResolvePickerFilePathAsync(file).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
            {
                SetImportStatus("Import failed.", "Could not access the selected file.", isActive: false);
                await ShowBulkImportDialogAsync("Could not access the selected file.", XamlRoot).ConfigureAwait(true);
                return;
            }

            tempPath = path != file.Path ? path : null;
            _acceptBulkProgress = true;
            var progress = new Progress<BulkImportProgress>(ApplyBulkUploadProgress);
            var result = await vm.ImportBulkTemplatesFromFileAsync(path, progress).ConfigureAwait(true);
            _acceptBulkProgress = false;

            var uploaded = result.TotalCount > 0 ? result.UploadedCount : _bulkUploaded;
            var total = result.TotalCount > 0 ? result.TotalCount : _bulkUploadTotal;
            var percent = total <= 0
                ? 0
                : (int)Math.Clamp(Math.Round(uploaded * 100.0 / total, MidpointRounding.AwayFromZero), 0, 100);

            var summaryParts = new List<string>();
            if (total > 0)
            {
                summaryParts.Add($"{uploaded}/{total} is uploaded ({percent}%).");
            }

            if (result.CreatedCount > 0)
            {
                summaryParts.Add($"Created {result.CreatedCount} template(s).");
            }

            if (result.ImportedRowCount > 0)
            {
                summaryParts.Add($"Imported {result.ImportedRowCount} data row(s).");
            }

            if (result.SkippedCount > 0)
            {
                summaryParts.Add($"Matched {result.SkippedCount} existing template name(s) (data may still have been added).");
            }

            var summary = summaryParts.Count > 0
                ? string.Join(" ", summaryParts)
                : "No templates or rows were imported.";

            if (result.Errors.Count > 0)
            {
                var detail = string.Join("\n", result.Errors.Take(8));
                if (result.Errors.Count > 8)
                {
                    detail += $"\n…and {result.Errors.Count - 8} more.";
                }

                SetImportStatus(
                    "Import finished with notes.",
                    summary,
                    isActive: false,
                    completed: total > 0 ? uploaded : null,
                    total: total > 0 ? total : null);
                await ShowBulkImportDialogAsync($"{summary}\n\n{detail}", XamlRoot).ConfigureAwait(true);
            }
            else
            {
                SetImportStatus(
                    "Import complete.",
                    summary,
                    isActive: false,
                    completed: total > 0 ? uploaded : null,
                    total: total > 0 ? total : null);
                await ShowBulkImportDialogAsync(summary, XamlRoot).ConfigureAwait(true);
            }

            if (result.HasWork)
            {
                RefreshTemplatesListSource();
            }
        }
        catch (Exception ex)
        {
            SetImportStatus("Import failed.", ex.Message, isActive: false, completed: _bulkUploadTotal > 0 ? _bulkUploaded : null, total: _bulkUploadTotal > 0 ? _bulkUploadTotal : null);
            await ShowBulkImportDialogAsync($"Import failed:\n{ex.Message}", XamlRoot).ConfigureAwait(true);
        }
        finally
        {
            _acceptBulkProgress = false;
            SetImportStatus(null);
            BulkUploadButton.ClearValue(Control.IsEnabledProperty);
            _bulkUploadRunning = false;
            if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort temp cleanup.
                }
            }
        }
    }

    private void ApplyBulkUploadProgress(BulkImportProgress progress)
    {
        void Apply()
        {
            if (!_acceptBulkProgress)
            {
                return;
            }

            _bulkUploaded = progress.Completed;
            _bulkUploadTotal = progress.Total;
            var headline = progress.Total > 0 ? $"Uploading… {progress.Percent}%" : "Uploading…";
            SetImportStatus(headline, progress.Detail, isActive: true, completed: progress.Completed, total: progress.Total);
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Apply();
            return;
        }

        DispatcherQueue.TryEnqueue(Apply);
    }

    private void HideTemplateBrowsingPanels()
    {
        FavouritesSubPageControl.Visibility = Visibility.Collapsed;
        AllTemplatesPanel.Visibility = Visibility.Collapsed;
    }

    private void RefreshTemplatesListSource()
    {
        if (TemplateComposerControl.Visibility == Visibility.Visible)
        {
            return;
        }

        if (DataContext is not TemplatesViewModel vm)
        {
            return;
        }

        var list = vm.Templates
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (list.Count == 0)
        {
            AllTemplatesCarouselControl.Visibility = Visibility.Collapsed;
            EmptyTemplatesHint.Text = "No analytics templates yet. Use Create New Template or Bulk Upload.";
            EmptyTemplatesHint.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyTemplatesHint.Visibility = Visibility.Collapsed;
            AllTemplatesCarouselControl.Visibility = Visibility.Visible;
            AllTemplatesCarouselControl.Load(vm);
        }
    }

    private void OnTemplatesListItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not PartTemplateDefinition template)
        {
            return;
        }

        App.Services.GetRequiredService<INavigationService>().Navigate(AppPage.TemplateData, template.Id);
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

        if (ZoomLevelText is not null)
            ZoomLevelText.Text = $"{(int)Math.Round(_canvasZoom * 100)}%";

        ClampCanvasTranslation();
    }

    private void OnTemplateCanvasDoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        ResetCanvasView();
        e.Handled = true;
    }

    private void OnPageKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Space)
        {
            _isSpacePanMode = true;
            e.Handled = true;
            return;
        }
        if (e.Key == Windows.System.VirtualKey.Delete
            && DataContext is TemplatesViewModel vm
            && vm.IsCreatingTemplate)
        {
            var focused = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
            var draft = FindAncestorDataContext<ColumnLabelDraft>(focused);
            if (draft is not null && vm.ColumnLabels.Count > 1)
            {
                vm.ColumnLabels.Remove(draft);
                RefreshCanvasUiState(vm);
                e.Handled = true;
            }
        }
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
        if (ZoomLevelText is not null)
            ZoomLevelText.Text = "100%";
    }

    private void CenterCanvasContent()
    {
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (TemplateCanvasScrollViewer is null || TemplateCanvasItemsControl is null || TemplateCanvasTranslateTransform is null)
                return;
            var viewportHeight = TemplateCanvasScrollViewer.ActualHeight;
            var contentHeight = TemplateCanvasItemsControl.ActualHeight * _canvasZoom;
            if (viewportHeight <= 0 || contentHeight <= 0)
                return;
            TemplateCanvasTranslateTransform.X = 24;
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
            return;
        var viewportHeight = TemplateCanvasScrollViewer.ActualHeight;
        var contentHeight = TemplateCanvasItemsControl.ActualHeight * _canvasZoom;
        if (viewportHeight <= 0 || contentHeight <= 0)
            return;
        const double verticalPadding = 24;
        var minY = -(contentHeight - verticalPadding);
        var maxY = viewportHeight - verticalPadding;
        if (contentHeight <= viewportHeight)
        {
            minY = 0;
            maxY = viewportHeight - contentHeight;
        }
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
        if (DataContext is not TemplatesViewModel vm) return;
        var draft = (sender as FrameworkElement)?.DataContext as ColumnLabelDraft;
        if (draft is null) return;
        var index = vm.ColumnLabels.IndexOf(draft);
        if (index < 0) return;
        vm.ColumnLabels.Insert(index, new ColumnLabelDraft());
        RefreshAllCanvasCellBorders();
        SyncCanvasBaseToCurrentTranslation();
        RefreshCanvasUiState(vm);
    }

    private void OnCanvasInsertRightClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm) return;
        var draft = (sender as FrameworkElement)?.DataContext as ColumnLabelDraft;
        if (draft is null) return;
        var index = vm.ColumnLabels.IndexOf(draft);
        if (index < 0) return;
        vm.ColumnLabels.Insert(index + 1, new ColumnLabelDraft());
        RefreshAllCanvasCellBorders();
        SyncCanvasBaseToCurrentTranslation();
        RefreshCanvasUiState(vm);
    }

    private void OnCanvasDeleteColumnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm) return;
        if (vm.ColumnLabels.Count <= 1) return;
        var draft = (sender as FrameworkElement)?.DataContext as ColumnLabelDraft;
        if (draft is null) return;
        vm.ColumnLabels.Remove(draft);
        RefreshCanvasUiState(vm);
    }

    private void OnAddFieldRowClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not TemplatesViewModel vm) return;
        vm.ColumnLabels.Add(new ColumnLabelDraft());
        RefreshCanvasUiState(vm);
    }

    private void RefreshCanvasUiState(TemplatesViewModel vm)
    {
        var count = vm.ColumnLabels.Count;
        if (FieldCountText is not null)
            FieldCountText.Text = count == 1 ? "1 field" : $"{count} fields";
        if (CanvasEmptyState is not null)
            CanvasEmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        WireCanvasInputChrome();
    }

    private bool ShouldUseLightCanvasChrome() =>
        !App.Services.GetRequiredService<IThemeService>().IsDark;

    private void WireCanvasInputChrome()
    {
        if (!ShouldUseLightCanvasChrome())
        {
            return;
        }

        foreach (var box in FindVisualChildren<TextBox>(TemplateCanvasScrollViewer))
        {
            UiResourceHelper.WirePersistentLightTextBox(box);
        }

        foreach (var combo in FindVisualChildren<ComboBox>(TemplateCanvasScrollViewer))
        {
            UiResourceHelper.WirePersistentLightComboBox(combo);
        }
    }

    private void OnCanvasTextBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && ShouldUseLightCanvasChrome())
        {
            UiResourceHelper.WirePersistentLightTextBox(box);
        }
    }

    private void OnCanvasComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox combo && ShouldUseLightCanvasChrome())
        {
            UiResourceHelper.WirePersistentLightComboBox(combo);
        }
    }

    private void OnZoomResetClick(object sender, RoutedEventArgs e)
    {
        ResetCanvasView();
        CenterCanvasContent();
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
            ApplyCanvasCellEdgeStyling(border);
        if (DataContext is TemplatesViewModel vm)
            RefreshCanvasUiState(vm);
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

            // If the All Templates panel is open, rebuild it so star states stay in sync
            if (AllTemplatesPanel.Visibility == Visibility.Visible)
            {
                RefreshTemplatesListSource();
            }
        }
    }

    private void UpdateFavoriteStars()
    {
        // The AllTemplatesCarousel manages its own star state via its card buttons.
        // Reload the carousel so all cards reflect the latest favourite state.
        if (DataContext is TemplatesViewModel vm && AllTemplatesCarouselControl.Visibility == Visibility.Visible)
        {
            AllTemplatesCarouselControl.Load(vm);
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
                var isFavorite = vm.IsFavouriteFor(template.Id);
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
                        icon.Glyph = isFavorite ? "\uE735" : "\uE734";
                        icon.Foreground = isFavorite
                            ? new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 31, 122, 224))
                            : Application.Current.Resources["TextSecondaryBrush"] as SolidColorBrush;
                    }

                    DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
                    {
                        UpdateFavoriteStars();
                    });
                }
                catch
                {
                    // Silently ignore favorite toggle errors
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

    private static async Task ShowBulkImportDialogAsync(string message, XamlRoot xamlRoot)
    {
        var dialog = new ContentDialog
        {
            Title = "Bulk upload",
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.WrapWholeWords },
            CloseButtonText = "OK",
            XamlRoot = xamlRoot,
        };
        _ = await dialog.ShowAsync();
    }

    private static async Task<string?> ResolvePickerFilePathAsync(StorageFile file)
    {
        if (!string.IsNullOrWhiteSpace(file.Path) && File.Exists(file.Path))
        {
            return file.Path;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "PartFinderBulkImport");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_{file.Name}");
        using (var source = await file.OpenStreamForReadAsync().ConfigureAwait(true))
        await using (var dest = File.Create(tempPath))
        {
            await source.CopyToAsync(dest).ConfigureAwait(true);
        }

        return tempPath;
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
        SetImportStatus(
            finalHeadline,
            $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.",
            isActive: false,
            completed: status.ProcessedRows,
            total: status.TotalRows);
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
                $"Import {status.Status}... {FormatUploadPercent(status.ProcessedRows, status.TotalRows)}%",
                $"Processed {status.ProcessedRows}/{status.TotalRows}, failed {status.FailedRows}.",
                isActive: true,
                completed: status.ProcessedRows,
                total: status.TotalRows);

            if (string.Equals(status.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status.Status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        return null;
    }

    private void SetImportStatus(
        string? headline,
        string? detail = null,
        bool isActive = false,
        int? completed = null,
        int? total = null)
    {
        if (ImportStatusPanel is null || ImportProgressRing is null || ImportStatusText is null || ImportStatusDetailText is null
            || ImportProgressBar is null || ImportPercentText is null || ImportCountText is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            ImportStatusPanel.Visibility = Visibility.Collapsed;
            ImportProgressRing.IsActive = false;
            ImportProgressRing.Visibility = Visibility.Collapsed;
            ImportProgressBar.Visibility = Visibility.Collapsed;
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Value = 0;
            ImportPercentText.Visibility = Visibility.Collapsed;
            ImportPercentText.Text = string.Empty;
            ImportCountText.Visibility = Visibility.Collapsed;
            ImportCountText.Text = string.Empty;
            ImportStatusText.Text = string.Empty;
            ImportStatusDetailText.Text = string.Empty;
            return;
        }

        var hasCount = completed is int && total is int overall && overall > 0;
        var percent = hasCount ? FormatUploadPercent(completed!.Value, total!.Value) : 0;

        ImportStatusPanel.Visibility = Visibility.Visible;
        ImportStatusText.Text = headline;
        ImportStatusDetailText.Text = detail ?? string.Empty;

        if (hasCount)
        {
            ImportProgressRing.IsActive = false;
            ImportProgressRing.Visibility = Visibility.Collapsed;
            ImportProgressBar.Visibility = Visibility.Visible;
            ImportProgressBar.IsIndeterminate = false;
            ImportProgressBar.Value = percent;
            ImportPercentText.Visibility = Visibility.Visible;
            ImportPercentText.Text = $"{percent}%";
            ImportCountText.Visibility = Visibility.Visible;
            ImportCountText.Text = $"{completed}/{total} is uploaded";
            return;
        }

        ImportProgressBar.Visibility = Visibility.Collapsed;
        ImportProgressBar.Value = 0;
        ImportPercentText.Visibility = Visibility.Collapsed;
        ImportCountText.Visibility = Visibility.Collapsed;
        ImportProgressRing.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        ImportProgressRing.IsActive = isActive;
    }

    private static int FormatUploadPercent(int completed, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return (int)Math.Clamp(Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero), 0, 100);
    }
}
