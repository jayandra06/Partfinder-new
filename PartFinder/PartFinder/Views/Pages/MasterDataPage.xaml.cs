using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Core;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PartFinder.Views.Pages;

public sealed partial class MasterDataPage : Page
{
    private const double GridCellTextWidth = 160d;
    private const double GridCellHorizontalPadding = 10d;
    private const double ActionPanelWidth = 320;
    private MasterDataViewModel? _viewModel;
    private readonly IExcelTemplateService _excelTemplateService;
    private readonly ActivityLogger _activity;
    private readonly ExplorerNavigationCoordinator _explorerNav;
    private readonly List<ActionResultDisplayItem> _actionResultRows = new();
    private bool _headerHoverTipsAttached;
    private Button? _headerEditButton;
    private Button? _headerExportButton;
    private Button? _headerImportButton;
    private AutoSuggestBox? _explorerTemplateSuggestBox;
    private Popup? _headerActionTipPopup;
    private Border? _headerActionTipHost;
    private TextBlock? _headerActionTipText;
    private DispatcherQueueTimer? _headerTipCloseTimer;
    public MasterDataPage()
    {
        InitializeComponent();
        ResolveHeaderHoverUi();
        var vm = App.Services.GetRequiredService<MasterDataViewModel>();
        _excelTemplateService = App.Services.GetRequiredService<IExcelTemplateService>();
        _activity = App.Services.GetRequiredService<ActivityLogger>();
        _explorerNav = App.Services.GetRequiredService<ExplorerNavigationCoordinator>();
        DataContext = vm;
        Loaded += OnMasterDataPageLoaded;
        Unloaded += OnMasterDataPageUnloaded;
    }

    private async void OnMasterDataPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnMasterDataPageLoaded;
        if (DataContext is not MasterDataViewModel vm)
        {
            return;
        }

        _viewModel = vm;
        vm.Rows.CollectionChanged += OnGridStructureChanged;
        vm.Columns.CollectionChanged += OnGridStructureChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.GridFilterChanged += OnGridFilterChanged;
        _explorerNav.OpenTemplateRequested += OnExplorerOpenTemplateRequested;
        SetupRowMatchFilterCombo();
        RootGrid.KeyDown += OnRootGridKeyDown;
        if (GridSearchBox is not null)
        {
            GridSearchBox.KeyDown += OnRootGridKeyDown;
        }

        await vm.LoadAsync().ConfigureAwait(true);
        RebuildSpreadsheet();
        AttachHeaderHoverTips();
    }

    private void OnMasterDataPageUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnMasterDataPageUnloaded;
        DetachHeaderHoverTips();
        RootGrid.KeyDown -= OnRootGridKeyDown;
        if (GridSearchBox is not null)
        {
            GridSearchBox.KeyDown -= OnRootGridKeyDown;
        }

        _explorerNav.OpenTemplateRequested -= OnExplorerOpenTemplateRequested;
        if (_viewModel is not null)
        {
            _viewModel.Rows.CollectionChanged -= OnGridStructureChanged;
            _viewModel.Columns.CollectionChanged -= OnGridStructureChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.GridFilterChanged -= OnGridFilterChanged;
            _viewModel = null;
        }
    }

    private void OnExplorerOpenTemplateRequested(string templateId) =>
        _viewModel?.OpenTemplateById(templateId);

    private void OnGridFilterChanged() => RebuildSpreadsheet();

    private void SetupRowMatchFilterCombo()
    {
        if (RowMatchFilterCombo is null)
        {
            return;
        }

        RowMatchFilterCombo.Items.Clear();
        RowMatchFilterCombo.Items.Add("All rows");
        RowMatchFilterCombo.Items.Add("Matched only");
        RowMatchFilterCombo.Items.Add("Unmatched only");
        RowMatchFilterCombo.SelectedIndex = 0;
        RowMatchFilterCombo.SelectionChanged += (_, _) =>
        {
            if (_viewModel is null || RowMatchFilterCombo.SelectedIndex < 0)
            {
                return;
            }

            _viewModel.RowMatchFilter = (ExplorerRowMatchFilter)RowMatchFilterCombo.SelectedIndex;
        };
    }

    private void OnRootGridKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var ctrlDown = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (e.Key == Windows.System.VirtualKey.F && ctrlDown)
        {
            GridSearchBox?.Focus(FocusState.Programmatic);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Escape && !_viewModel.IsDrawerPinned)
        {
            _viewModel.CloseDetailsPanelCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter && _viewModel.SelectedGridRow is not null)
        {
            var cell = _viewModel.SelectedGridCell ?? _viewModel.SelectedGridRow.Cells.FirstOrDefault();
            _viewModel.SelectGridCell(_viewModel.SelectedGridRow, cell);
            e.Handled = true;
            return;
        }

        var rowDelta = e.Key switch
        {
            Windows.System.VirtualKey.Down => 1,
            Windows.System.VirtualKey.Up => -1,
            _ => 0,
        };
        var colDelta = e.Key switch
        {
            Windows.System.VirtualKey.Right => 1,
            Windows.System.VirtualKey.Left => -1,
            _ => 0,
        };

        if (rowDelta != 0 || colDelta != 0)
        {
            if (_viewModel.TryMoveSelection(rowDelta, colDelta))
            {
                RebuildSpreadsheet();
                e.Handled = true;
            }
        }
    }

    private void OnColumnVisibilityClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || ColumnVisibilityButton is null)
        {
            return;
        }

        var flyout = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom };
        foreach (var col in _viewModel.ColumnVisibility)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = col.Label,
                IsChecked = col.IsVisible,
            };
            var captured = col;
            item.Click += (_, _) => captured.IsVisible = item.IsChecked;
            flyout.Items.Add(item);
        }

        flyout.ShowAt(ColumnVisibilityButton);
    }

    // Resolves header controls by name so code-behind does not rely on generated field symbols.
    private void ResolveHeaderHoverUi()
    {
        _headerEditButton = FindName("HeaderEditButton") as Button;
        _headerExportButton = FindName("HeaderExportButton") as Button;
        _headerImportButton = FindName("HeaderImportButton") as Button;
        _explorerTemplateSuggestBox = FindName("ExplorerTemplateSuggestBox") as AutoSuggestBox;
        _headerActionTipPopup = FindName("HeaderActionTipPopup") as Popup;
        _headerActionTipHost = FindName("HeaderActionTipHost") as Border;
        _headerActionTipText = FindName("HeaderActionTipText") as TextBlock;
    }

    private void AttachHeaderHoverTips()
    {
        if (_headerHoverTipsAttached)
        {
            return;
        }

        if (_headerEditButton is null
            || _headerExportButton is null
            || _headerImportButton is null
            || _headerActionTipPopup is null
            || _headerActionTipHost is null
            || _headerActionTipText is null)
        {
            return;
        }

        _headerHoverTipsAttached = true;
        _headerEditButton.PointerEntered += OnHeaderToolbarButtonPointerEntered;
        _headerEditButton.PointerExited += OnHeaderToolbarButtonPointerExited;
        _headerExportButton.PointerEntered += OnHeaderToolbarButtonPointerEntered;
        _headerExportButton.PointerExited += OnHeaderToolbarButtonPointerExited;
        _headerImportButton.PointerEntered += OnHeaderToolbarButtonPointerEntered;
        _headerImportButton.PointerExited += OnHeaderToolbarButtonPointerExited;
        _headerActionTipHost.PointerEntered += OnHeaderActionTipHostPointerEntered;
        _headerActionTipHost.PointerExited += OnHeaderActionTipHostPointerExited;
    }

    private void DetachHeaderHoverTips()
    {
        if (!_headerHoverTipsAttached)
        {
            return;
        }

        _headerHoverTipsAttached = false;
        if (_headerEditButton is not null)
        {
            _headerEditButton.PointerEntered -= OnHeaderToolbarButtonPointerEntered;
            _headerEditButton.PointerExited -= OnHeaderToolbarButtonPointerExited;
        }

        if (_headerExportButton is not null)
        {
            _headerExportButton.PointerEntered -= OnHeaderToolbarButtonPointerEntered;
            _headerExportButton.PointerExited -= OnHeaderToolbarButtonPointerExited;
        }

        if (_headerImportButton is not null)
        {
            _headerImportButton.PointerEntered -= OnHeaderToolbarButtonPointerEntered;
            _headerImportButton.PointerExited -= OnHeaderToolbarButtonPointerExited;
        }

        if (_headerActionTipHost is not null)
        {
            _headerActionTipHost.PointerEntered -= OnHeaderActionTipHostPointerEntered;
            _headerActionTipHost.PointerExited -= OnHeaderActionTipHostPointerExited;
        }

        CancelHeaderTipClose();
        if (_headerActionTipPopup is not null)
        {
            _headerActionTipPopup.IsOpen = false;
        }
    }

    private void OnHeaderToolbarButtonPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Button b || XamlRoot is null || _headerActionTipPopup is null || _headerActionTipText is null)
        {
            return;
        }

        CancelHeaderTipClose();

        var text = ReferenceEquals(b, _headerEditButton)
            ? "Edit"
            : ReferenceEquals(b, _headerExportButton)
                ? "Export Excel"
                : "Import Excel";

        _headerActionTipText.Text = text;
        _headerActionTipPopup.PlacementTarget = b;
        _headerActionTipPopup.DesiredPlacement = PopupPlacementMode.Top;
        _headerActionTipPopup.VerticalOffset = -6;
        _headerActionTipPopup.HorizontalOffset = 0;
        _headerActionTipPopup.XamlRoot = XamlRoot;
        _headerActionTipPopup.IsOpen = true;
    }

    private void OnHeaderToolbarButtonPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ScheduleHeaderTipClose();
    }

    private void OnHeaderActionTipHostPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        CancelHeaderTipClose();
    }

    private void OnHeaderActionTipHostPointerExited(object sender, PointerRoutedEventArgs e)
    {
        ScheduleHeaderTipClose();
    }

    private void CancelHeaderTipClose()
    {
        _headerTipCloseTimer?.Stop();
    }

    private void ScheduleHeaderTipClose()
    {
        var queue = DispatcherQueue.GetForCurrentThread();
        _headerTipCloseTimer ??= queue.CreateTimer();
        _headerTipCloseTimer.Stop();
        _headerTipCloseTimer.Interval = TimeSpan.FromMilliseconds(120);
        _headerTipCloseTimer.IsRepeating = false;
        _headerTipCloseTimer.Tick -= OnHeaderTipCloseTimerTick;
        _headerTipCloseTimer.Tick += OnHeaderTipCloseTimerTick;
        _headerTipCloseTimer.Start();
    }

    private void OnHeaderTipCloseTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Tick -= OnHeaderTipCloseTimerTick;
        if (_headerActionTipPopup is not null)
        {
            _headerActionTipPopup.IsOpen = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MasterDataViewModel.IsEditMode))
        {
            RebuildSpreadsheet();
        }

        if (e.PropertyName is nameof(MasterDataViewModel.SelectedGridRow)
            or nameof(MasterDataViewModel.SelectedGridCell)
            or nameof(MasterDataViewModel.LinkedRelationSections)
            or nameof(MasterDataViewModel.ShowLinkedInfoPanel))
        {
            UpdateLinkedPanelEmptyHint();
        }

        if (e.PropertyName is nameof(MasterDataViewModel.SelectedGridRow)
            or nameof(MasterDataViewModel.SelectedGridCell)
            or nameof(MasterDataViewModel.IsEditMode)
            or nameof(MasterDataViewModel.FilteredRowCountText))
        {
            RebuildSpreadsheet();
        }

        if (e.PropertyName == nameof(MasterDataViewModel.ToastMessage)
            && !string.IsNullOrEmpty(_viewModel?.ToastMessage))
        {
            _ = ClearToastAfterDelayAsync();
        }
    }

    private async Task ClearToastAfterDelayAsync()
    {
        await Task.Delay(2200).ConfigureAwait(true);
        if (_viewModel is not null)
        {
            _viewModel.ToastMessage = string.Empty;
        }
    }

    private void OnGridCellTapped(object sender, TappedRoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Border { Tag: GridCellTag tag })
        {
            return;
        }

        _viewModel.SelectGridCell(tag.Row, tag.Cell);
        RebuildSpreadsheet();
        UpdateLinkedPanelEmptyHint();
    }

    private void UpdateLinkedPanelEmptyHint()
    {
        if (LinkedPanelEmptyHint is null || _viewModel is null)
        {
            return;
        }

        LinkedPanelEmptyHint.Visibility =
            _viewModel.ShowLinkedInfoPanel && _viewModel.LinkedRelationSections.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OnExplorerTemplateSuggestTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput || _viewModel is null)
        {
            return;
        }

        _viewModel.TemplatePickerText = sender.Text ?? string.Empty;
    }

    private void OnExplorerTemplateSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (_viewModel is null || args.SelectedItem is not PartTemplateDefinition template)
        {
            return;
        }

        _viewModel.SelectTemplateFromPicker(template);
    }

    private void OnGridStructureChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildSpreadsheet();
    }

    private async void OnToggleEditModeClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || XamlRoot is null)
        {
            return;
        }

        if (_viewModel.IsEditMode)
        {
            var dlg = new ContentDialog
            {
                Title = "Stop editing?",
                Content = "If you changed values, click Save first to persist them. Stop editing now?",
                PrimaryButtonText = "Stop editing",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            if (await dlg.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
        }

        if (_viewModel.ToggleEditModeCommand.CanExecute(null))
        {
            _viewModel.ToggleEditModeCommand.Execute(null);
        }
    }

    private async void OnSaveGridClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || XamlRoot is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            Title = "Save changes?",
            Content = "This writes current grid values to the selected template data set.",
            PrimaryButtonText = "Save now",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary
            && _viewModel.SaveGridCommand.CanExecute(null))
        {
            _viewModel.SaveGridCommand.Execute(null);
            var templateName = _viewModel.SelectedDataTemplate?.Name ?? "Unknown";
            _activity.LogStockChange("Data Saved", $"Explorer data saved for template '{templateName}' — {_viewModel.Rows.Count} rows");
        }
    }

    private async void OnCancelEditClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (_viewModel.SelectedDataTemplate is null)
        {
            _viewModel.ToggleEditModeCommand.Execute(null);
            return;
        }

        await _viewModel.LoadForTemplateAsync(_viewModel.SelectedDataTemplate.Id).ConfigureAwait(true);
        if (_viewModel.IsEditMode)
        {
            _viewModel.ToggleEditModeCommand.Execute(null);
        }
    }

    private async void OnExportTemplateClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedDataTemplate is null || App.MainAppWindow is null || XamlRoot is null)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = $"{_viewModel.SelectedDataTemplate.Name}-template",
        };
        picker.FileTypeChoices.Add("Excel Workbook", new List<string> { ".xlsx" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await _excelTemplateService.ExportTemplateAsync(_viewModel.SelectedDataTemplate, file.Path);

        var dlg = new ContentDialog
        {
            Title = "Template exported",
            Content = $"Excel template saved to:\n{file.Path}",
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        _ = await dlg.ShowAsync();
    }

    private async void OnImportExcelClicked(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.SelectedDataTemplate is null || App.MainAppWindow is null || XamlRoot is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".xlsx");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var parsed = await _excelTemplateService.ParseImportFileAsync(_viewModel.SelectedDataTemplate, file.Path);
        if (parsed.Rows.Count == 0)
        {
            var noneDlg = new ContentDialog
            {
                Title = "No data to import",
                Content = BuildImportSummaryContent(parsed, 0, imported: 0),
                CloseButtonText = "Close",
                XamlRoot = XamlRoot,
            };
            _ = await noneDlg.ShowAsync();
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Import Excel data?",
            Content = $"Import {parsed.Rows.Count} row(s) into template \"{_viewModel.SelectedDataTemplate.Name}\"?",
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var imported = await _viewModel.ImportRowsAsync(parsed.Rows);
        var dlg = new ContentDialog
        {
            Title = "Import completed",
            Content = BuildImportSummaryContent(parsed, parsed.Rows.Count, imported),
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        _activity.LogStockChange("Excel Import", $"Imported {imported} rows into template '{_viewModel.SelectedDataTemplate.Name}'");
        _ = await dlg.ShowAsync();
    }

    private static UIElement BuildImportSummaryContent(ExcelImportParseResult parsed, int detectedRows, int imported)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = $"Detected rows: {detectedRows}" });
        panel.Children.Add(new TextBlock { Text = $"Imported rows: {imported}" });
        panel.Children.Add(new TextBlock { Text = $"Empty rows skipped: {parsed.EmptyRowsSkipped}" });
        if (parsed.Warnings.Count > 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Warnings:",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0),
            });
            foreach (var warning in parsed.Warnings)
            {
                panel.Children.Add(new TextBlock { Text = "- " + warning, TextWrapping = TextWrapping.WrapWholeWords });
            }
        }

        return panel;
    }

    private void RebuildSpreadsheet()
    {
        if (_viewModel is null || SpreadsheetHost is null)
        {
            return;
        }

        if (_viewModel.Columns.Count == 0)
        {
            SpreadsheetHost.Content = null;
            return;
        }

        var visibleRows = _viewModel.GetVisibleRows();

        const double minColWidth = GridCellTextWidth + (GridCellHorizontalPadding * 2);
        var visibleColumns = _viewModel.GetVisibleColumns();
        var colCount = visibleColumns.Count;
        var rowCount = visibleRows.Count;

        var scroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        var grid = new Grid
        {
            MinWidth = colCount * minColWidth,
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
        };

        for (var c = 0; c < colCount; c++)
        {
            grid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star),
                    MinWidth = minColWidth,
                });
        }

        for (var r = 0; r <= rowCount; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var headerBg = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"];
        var borderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"];
        var headerForeground = (Brush)Application.Current.Resources["TextSecondaryBrush"];

        for (var c = 0; c < colCount; c++)
        {
            var field = visibleColumns[c];
            var headerLabel = field.Type == TemplateFieldType.RecordLink
                ? $"{field.Label} (link)"
                : field.Label;
            var headerBorder = new Border
            {
                Background = headerBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 10, 10, 10),
            };
            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c);

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var title = new TextBlock
            {
                Text = headerLabel,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = headerForeground,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(title, 0);
            headerGrid.Children.Add(title);

            if (_viewModel.IsEditMode)
            {
                var addColumnBtn = new Button
                {
                    Content = "+",
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)Application.Current.Resources["OutlinedButtonStyle"],
                    Visibility = Visibility.Visible,
                    Opacity = 0,
                    IsHitTestVisible = false,
                    Tag = c + 1,
                };
                ToolTipService.SetToolTip(addColumnBtn, "Insert column after this header");
                addColumnBtn.Click += OnInsertColumnFromHeaderClick;
                Grid.SetColumn(addColumnBtn, 1);
                headerGrid.Children.Add(addColumnBtn);

                headerBorder.PointerEntered += (_, _) => AffordanceAnimationHelper.Fade(addColumnBtn, show: true, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
                headerBorder.PointerExited += (_, _) => AffordanceAnimationHelper.Fade(addColumnBtn, show: false, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
            }

            headerBorder.Child = headerGrid;

            grid.Children.Add(headerBorder);
        }

        if (_viewModel.IsEditMode && colCount > 0)
        {
            var addFirstHeader = new Button
            {
                Content = "+",
                Width = 24,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Style = (Style)Application.Current.Resources["OutlinedButtonStyle"],
                Tag = 0,
            };
            ToolTipService.SetToolTip(addFirstHeader, "Insert column before the first header");
            addFirstHeader.Click += OnInsertColumnFromHeaderClick;
            Grid.SetRow(addFirstHeader, 0);
            Grid.SetColumn(addFirstHeader, 0);
            grid.Children.Add(addFirstHeader);
        }

        for (var r = 0; r < rowCount; r++)
        {
            var rowVm = visibleRows[r];
            for (var c = 0; c < colCount; c++)
            {
                var field = visibleColumns[c];
                var cellVm = rowVm.Cells.FirstOrDefault(x => x.FieldKey == field.Key);
                if (cellVm is null)
                {
                    continue;
                }

                UIElement cellContent;
                if (cellVm.IsRecordLink)
                {
                    cellContent = WrapCellWithMatchBadge(BuildLinkCell(cellVm, rowVm), rowVm, cellVm);
                }
                else
                {
                    cellContent = WrapCellWithMatchBadge(BuildTextCell(cellVm, rowVm), rowVm, cellVm);
                }

                var isSelectedRow = ReferenceEquals(rowVm, _viewModel.SelectedGridRow);
                var isSelectedCell = ReferenceEquals(cellVm, _viewModel.SelectedGridCell);
                var isHighlighted = isSelectedCell || isSelectedRow;
                var cellBorder = new Border
                {
                    Background = (Brush)Application.Current.Resources[
                        isHighlighted ? "NavItemSelectedBrush" : r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = isSelectedCell
                        ? (Brush)Application.Current.Resources["AccentPrimaryBrush"]
                        : isSelectedRow
                            ? (Brush)Application.Current.Resources["BorderDefaultBrush"]
                            : borderBrush,
                    BorderThickness = isSelectedCell
                        ? new Thickness(2)
                        : new Thickness(0, 0, 1, 1),
                    Child = cellContent,
                    Tag = new GridCellTag(rowVm, cellVm),
                };
                cellBorder.Tapped += OnGridCellTapped;

                Grid.SetRow(cellBorder, r + 1);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        if (_viewModel.IsEditMode)
        {
            // Canva-like affordance: insert points between rows (and at the end), pinned to last column.
            for (var insertAfterRow = 0; insertAfterRow <= rowCount; insertAfterRow++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var insertionIndex = insertAfterRow;
                var addRowButton = new Button
                {
                    Content = "+",
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 8, 2),
                    Style = (Style)Application.Current.Resources["OutlinedButtonStyle"],
                    Visibility = Visibility.Visible,
                    Opacity = 0,
                    IsHitTestVisible = false,
                };
                addRowButton.Click += (_, _) => _viewModel.InsertRowAt(insertionIndex);

                var addRowHost = new Border
                {
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                    Child = addRowButton,
                };

                // Header is row 0; data rows are 1..rowCount. Insert strip after each data row.
                Grid.SetRow(addRowHost, rowCount + 1 + insertAfterRow);
                Grid.SetColumn(addRowHost, colCount - 1);
                grid.Children.Add(addRowHost);

                addRowHost.PointerEntered += (_, _) => AffordanceAnimationHelper.Fade(addRowButton, show: true, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
                addRowHost.PointerExited += (_, _) => AffordanceAnimationHelper.Fade(addRowButton, show: false, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
            }
        }

        scroll.Content = grid;
        SpreadsheetHost.Content = scroll;
    }

    private async void OnInsertColumnFromHeaderClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || XamlRoot is null || sender is not Button { Tag: int insertAt })
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
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        _ = await _viewModel.InsertColumnAtAsync(insertAt, input.Text).ConfigureAwait(true);
    }

    private UIElement WrapCellWithMatchBadge(UIElement content, MasterDataRowViewModel rowVm, MasterDataCellViewModel cellVm)
    {
        if (_viewModel is null || !_viewModel.ShouldShowMatchBadge(rowVm, cellVm))
        {
            return content;
        }

        var matched = _viewModel.GetRowMatchBadge(rowVm.RowId) == ExplorerCellMatchBadge.Matched;
        var host = new Grid { MinHeight = 36 };
        host.Children.Add(content);

        var badge = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 4, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = (Brush)Application.Current.Resources[
                matched ? "SuccessBrush" : "TextTertiaryBrush"],
        };
        ToolTipService.SetToolTip(
            badge,
            matched ? "Linked row matched" : "No linked row for this match key");
        host.Children.Add(badge);
        return host;
    }

    private TextBox BuildTextCell(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var box = new TextBox
        {
            MinHeight = 36,
            Width = GridCellTextWidth,
            Padding = new Thickness(GridCellHorizontalPadding, 6, GridCellHorizontalPadding, 6),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        box.SetBinding(
            TextBox.TextProperty,
            new Binding
            {
                Path = new PropertyPath(nameof(MasterDataCellViewModel.Text)),
                Source = cellVm,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
        box.IsReadOnly = _viewModel is null || !_viewModel.IsEditMode;
        box.ContextFlyout = BuildCellContextMenu(cellVm, rowVm, isRecordLink: false);
        return box;
    }

    private UIElement BuildLinkCell(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var box = new TextBox
        {
            MinHeight = 36,
            Width = GridCellTextWidth,
            Padding = new Thickness(GridCellHorizontalPadding, 6, GridCellHorizontalPadding, 6),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0),
        };
        box.SetBinding(
            TextBox.TextProperty,
            new Binding
            {
                Path = new PropertyPath(nameof(MasterDataCellViewModel.Text)),
                Source = cellVm,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            });
        box.IsReadOnly = _viewModel is null || !_viewModel.IsEditMode;
        var originalText = string.Empty;
        box.GotFocus += (_, _) => originalText = cellVm.Text;
        box.LostFocus += (_, _) =>
        {
            // If the user edits text manually, treat this as an unlinked manual value.
            if (!string.Equals(originalText, cellVm.Text, StringComparison.Ordinal))
            {
                cellVm.LinkedRowId = null;
            }
        };

        var pickBtn = new Button
        {
            Content = "Pick…",
            Style = (Style)Application.Current.Resources["GhostButtonStyle"],
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 4, 4),
            MinWidth = 56,
            IsEnabled = _viewModel is not null && _viewModel.IsEditMode,
        };
        pickBtn.Click += async (_, _) => await ShowPickLinkDialogAsync(cellVm).ConfigureAwait(true);

        var rowPanel = new Grid();
        rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(box, 0);
        Grid.SetColumn(pickBtn, 1);
        rowPanel.Children.Add(box);
        rowPanel.Children.Add(pickBtn);

        var outer = new Border { MinHeight = 36 };
        outer.ContextFlyout = BuildCellContextMenu(cellVm, rowVm, isRecordLink: true);
        outer.Child = rowPanel;
        return outer;
    }

    private MenuFlyout BuildCellContextMenu(
        MasterDataCellViewModel cellVm,
        MasterDataRowViewModel rowVm,
        bool isRecordLink)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();

            var copy = new MenuFlyoutItem { Text = "Copy" };
            copy.Click += (_, _) => CopyToClipboard(cellVm.EffectiveCopyValue);
            flyout.Items.Add(copy);

            var copyRow = new MenuFlyoutItem { Text = "Copy row" };
            copyRow.Click += (_, _) => CopyRowToClipboard(rowVm);
            flyout.Items.Add(copyRow);

            if (_viewModel is not null && _viewModel.IsEditMode && !isRecordLink)
            {
                var edit = new MenuFlyoutItem { Text = "Edit" };
                edit.Click += (_, _) =>
                {
                    _viewModel.SelectGridCell(rowVm, cellVm);
                    RebuildSpreadsheet();
                };
                flyout.Items.Add(edit);
            }

            var details = new MenuFlyoutItem { Text = "View details" };
            details.Click += (_, _) =>
            {
                _viewModel?.SelectGridCell(rowVm, cellVm);
                RebuildSpreadsheet();
                UpdateLinkedPanelEmptyHint();
            };
            flyout.Items.Add(details);

            if (isRecordLink)
            {
                flyout.Items.Add(new MenuFlyoutSeparator());
                AppendLinkCellMenuItems(flyout, cellVm, rowVm);
            }
            else
            {
                AppendConfiguredContextActions(flyout, cellVm, rowVm);
            }
        };
        return flyout;
    }

    private void AppendConfiguredContextActions(
        MenuFlyout flyout,
        MasterDataCellViewModel cellVm,
        MasterDataRowViewModel rowVm)
    {
        if (_viewModel is null)
        {
            return;
        }

        var actions = _viewModel.GetContextActionsForField(cellVm.FieldKey);
        if (actions.Count == 0)
        {
            return;
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        foreach (var action in actions)
        {
            var item = new MenuFlyoutItem { Text = action.MenuLabel };
            var captured = action;
            item.Click += async (_, _) =>
                await ShowContextActionResultsAsync(captured, rowVm).ConfigureAwait(true);
            flyout.Items.Add(item);
        }
    }

    private async Task ShowContextActionResultsAsync(
        TemplateContextAction action,
        MasterDataRowViewModel rowVm)
    {
        if (_viewModel is null || XamlRoot is null || ActionRowsListView is null)
        {
            return;
        }

        var sourceMap = MasterDataViewModel.BuildSourceRowMap(rowVm);
        var (targetDef, rows) = await _viewModel.RunContextActionAsync(action, sourceMap).ConfigureAwait(true);
        if (targetDef is null)
        {
            return;
        }

        ActionTitleText.Text = action.MenuLabel;
        ActionCountText.Text = $"{rows.Count} row(s) in {targetDef.Name}";
        _actionResultRows.Clear();

        if (rows.Count == 0)
        {
            _actionResultRows.Add(
                new ActionResultDisplayItem(
                    "No matches",
                    new[] { "No rows matched your rules for this line. Check values and Templates -> cell actions." }));
        }
        else
        {
            var fieldOrder = ResolveDisplayFields(targetDef, action.DisplayFieldKeys);
            var rowNumber = 1;
            foreach (var resultRow in rows)
            {
                var lines = new List<string>(fieldOrder.Count);
                foreach (var f in fieldOrder)
                {
                    var val = resultRow.Values.TryGetValue(f.Key, out var v) ? v : string.Empty;
                    lines.Add($"{f.Label}: {val}");
                }

                _actionResultRows.Add(new ActionResultDisplayItem($"Row {rowNumber}", lines));
                rowNumber++;
            }
        }

        ActionRowsListView.ItemsSource = _actionResultRows;
        OpenActionPanel();
    }

    private void OnCloseActionPanelClick(object sender, RoutedEventArgs e)
    {
        CloseActionPanel();
    }

    private void OnActionOverlayTapped(object sender, TappedRoutedEventArgs e)
    {
        CloseActionPanel();
    }

    private void OpenActionPanel()
    {
        if (ActionResultsPanel is null || ActionResultsTransform is null || ActionResultsOverlay is null)
        {
            return;
        }

        ActionResultsPanel.Visibility = Visibility.Visible;
        if (LinkedInfoPanel is not null)
        {
            LinkedInfoPanel.Visibility = Visibility.Collapsed;
        }
        ActionResultsOverlay.Visibility = Visibility.Visible;

        var animation = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            From = ActionPanelWidth,
            To = 0,
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, ActionResultsTransform);
        Storyboard.SetTargetProperty(animation, "X");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void CloseActionPanel()
    {
        if (ActionResultsPanel is null || ActionResultsTransform is null || ActionResultsOverlay is null)
        {
            return;
        }

        var animation = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            From = 0,
            To = ActionPanelWidth,
            EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 5 },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(animation, ActionResultsTransform);
        Storyboard.SetTargetProperty(animation, "X");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        storyboard.Completed += (_, _) =>
        {
            ActionResultsPanel.Visibility = Visibility.Collapsed;
            if (LinkedInfoPanel is not null && _viewModel?.ShowLinkedInfoPanel == true)
            {
                LinkedInfoPanel.Visibility = Visibility.Visible;
            }
            ActionResultsOverlay.Visibility = Visibility.Collapsed;
        };
        storyboard.Begin();
    }

    private static List<TemplateFieldDefinition> ResolveDisplayFields(
        PartTemplateDefinition targetDef,
        IReadOnlyList<string>? displayKeys)
    {
        var ordered = targetDef.Fields.OrderBy(f => f.DisplayOrder).ToList();
        if (displayKeys is null || displayKeys.Count == 0)
        {
            return ordered;
        }

        var list = new List<TemplateFieldDefinition>();
        foreach (var key in displayKeys)
        {
            var f = ordered.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (f is not null)
            {
                list.Add(f);
            }
        }

        return list.Count > 0 ? list : ordered;
    }

    private void AppendLinkCellMenuItems(MenuFlyout flyout, MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var copyId = new MenuFlyoutItem { Text = "Copy linked row id" };
        copyId.Click += (_, _) => CopyToClipboard(cellVm.LinkedRowId);
        copyId.IsEnabled = !string.IsNullOrWhiteSpace(cellVm.LinkedRowId);
        flyout.Items.Add(copyId);

        var pick = new MenuFlyoutItem { Text = "Choose linked row…" };
        pick.Click += async (_, _) => await ShowPickLinkDialogAsync(cellVm).ConfigureAwait(true);
        flyout.Items.Add(pick);

        var open = new MenuFlyoutItem { Text = "View linked record…" };
        open.Click += async (_, _) => await ShowViewLinkedRecordDialogAsync(cellVm).ConfigureAwait(true);
        open.IsEnabled = !string.IsNullOrWhiteSpace(cellVm.LinkedRowId);
        flyout.Items.Add(open);

        var clear = new MenuFlyoutItem { Text = "Clear link" };
        clear.Click += (_, _) => _viewModel?.ClearLinkedCell(cellVm);
        clear.IsEnabled = !string.IsNullOrWhiteSpace(cellVm.LinkedRowId);
        flyout.Items.Add(clear);
    }

    private void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        _viewModel?.ShowToast("Copied to clipboard");
    }

    private void CopyRowToClipboard(MasterDataRowViewModel rowVm)
    {
        if (_viewModel is null)
        {
            return;
        }

        var parts = new List<string>();
        for (var i = 0; i < _viewModel.Columns.Count && i < rowVm.Cells.Count; i++)
        {
            var label = _viewModel.Columns[i].Label;
            var value = rowVm.Cells[i].Text;
            parts.Add($"{label}\t{value}");
        }

        CopyToClipboard(string.Join(Environment.NewLine, parts));
    }

    private sealed record GridCellTag(MasterDataRowViewModel Row, MasterDataCellViewModel Cell);

    private async Task ShowPickLinkDialogAsync(MasterDataCellViewModel cellVm)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(cellVm.LinkedTemplateId) || XamlRoot is null)
        {
            return;
        }

        var template = await _viewModel.GetTemplateDefinitionAsync(cellVm.LinkedTemplateId!).ConfigureAwait(true);
        if (template is null)
        {
            return;
        }

        var rows = await _viewModel.GetTemplateRowsForPickerAsync(cellVm.LinkedTemplateId!).ConfigureAwait(true);
        var displayKey = !string.IsNullOrWhiteSpace(cellVm.LinkedDisplayFieldKey)
            ? cellVm.LinkedDisplayFieldKey!
            : template.Fields.OrderBy(f => f.DisplayOrder).FirstOrDefault()?.Key ?? string.Empty;

        var options = rows
            .Select(
                r => new LinkPickItem(
                    r.Id,
                    FormatRowDisplay(r, displayKey)))
            .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var combo = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
            PlaceholderText = options.Count == 0 ? "No rows in that template yet" : "Select a row",
            ItemsSource = options,
            DisplayMemberPath = nameof(LinkPickItem.Display),
        };

        if (!string.IsNullOrWhiteSpace(cellVm.LinkedRowId))
        {
            var match = options.FirstOrDefault(o => string.Equals(o.Id, cellVm.LinkedRowId, StringComparison.Ordinal));
            combo.SelectedItem = match;
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(
            new TextBlock
            {
                Text = $"Template: {template.Name}",
                TextWrapping = TextWrapping.WrapWholeWords,
            });
        panel.Children.Add(combo);

        var dlg = new ContentDialog
        {
            Title = "Link to row",
            Content = panel,
            PrimaryButtonText = "Apply",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (combo.SelectedItem is not LinkPickItem picked)
        {
            return;
        }

        _viewModel.SetLinkedCell(cellVm, picked.Id, picked.Display);
    }

    private async Task ShowViewLinkedRecordDialogAsync(MasterDataCellViewModel cellVm)
    {
        if (_viewModel is null
            || string.IsNullOrWhiteSpace(cellVm.LinkedTemplateId)
            || string.IsNullOrWhiteSpace(cellVm.LinkedRowId)
            || XamlRoot is null)
        {
            return;
        }

        var template = await _viewModel.GetTemplateDefinitionAsync(cellVm.LinkedTemplateId!).ConfigureAwait(true);
        if (template is null)
        {
            return;
        }

        var rows = await _viewModel
            .GetRowsByIdsForTemplateAsync(cellVm.LinkedTemplateId!, new[] { cellVm.LinkedRowId! })
            .ConfigureAwait(true);

        var row = rows.FirstOrDefault();
        var stack = new StackPanel { Spacing = 10 };
        if (row is null)
        {
            stack.Children.Add(
                new TextBlock
                {
                    Text = "That row no longer exists. Clear the link or pick another row.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                });
        }
        else
        {
            foreach (var f in template.Fields.OrderBy(x => x.DisplayOrder))
            {
                var val = row.Values.TryGetValue(f.Key, out var v) ? v : string.Empty;
                stack.Children.Add(
                    new TextBlock
                    {
                        Text = $"{f.Label}: {val}",
                        TextWrapping = TextWrapping.WrapWholeWords,
                    });
            }
        }

        var scroll = new ScrollViewer
        {
            MaxHeight = 420,
            Content = stack,
        };

        var dlg = new ContentDialog
        {
            Title = $"Linked — {template.Name}",
            Content = scroll,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        _ = await dlg.ShowAsync();
    }

    private static string FormatRowDisplay(MasterDataRowRecord row, string displayKey)
    {
        if (!string.IsNullOrEmpty(displayKey)
            && row.Values.TryGetValue(displayKey, out var v)
            && !string.IsNullOrWhiteSpace(v))
        {
            return v;
        }

        return row.Id;
    }

    private sealed class LinkPickItem
    {
        public LinkPickItem(string id, string display)
        {
            Id = id;
            Display = display;
        }

        public string Id { get; }
        public string Display { get; }
    }

    private sealed class ActionResultDisplayItem
    {
        public ActionResultDisplayItem(string title, IReadOnlyList<string> lines)
        {
            Title = title;
            Lines = lines;
        }

        public string Title { get; }
        public IReadOnlyList<string> Lines { get; }
    }
}
