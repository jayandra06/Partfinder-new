using System.Collections.Specialized;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using PartFinder.Helpers;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PartFinder.Views.Pages;

public sealed partial class MasterDataPage : Page
{
    private const double GridCellTextWidth = 160d;
    private const double GridCellHorizontalPadding = 10d;
    private const double ActionPanelWidth = 360;
    private const int MasterDataPageSize = 50;
    private int _gridPageIndex;
    private bool _rebuildSpreadsheetScheduled;
    private bool _connectorUpdateQueued;
    private MasterDataViewModel? _viewModel;
    private readonly IExcelTemplateService _excelTemplateService;
    private readonly ActivityLogger _activity;
    private readonly CatalogBrowseSessionStore _catalogSession;
    private ScrollViewer? _spreadsheetScroll;
    private Grid? _sheetGrid;
    private string? _displayedTemplateId;
    private bool _suspendBrowsePersist;
    private MasterDataNavigationArgs? _navigationArgs;
    private bool _surfaceInitialized;
    private MasterDataRowViewModel? _activeLinkRow;
    private string? _activeLinkFieldKey;
    private string _activeLinkLabel = string.Empty;
    private HyperlinkButton? _linkAnchor;
    private Border? _highlightedCell;
    private Brush? _highlightedCellBackground;
    private Brush? _highlightedCellBorderBrush;
    private readonly HashSet<string> _openColumnSearchKeys = new(StringComparer.OrdinalIgnoreCase);
    private Thickness _highlightedCellBorderThickness;

    public MasterDataPage()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MasterDataViewModel>();
        _excelTemplateService = App.Services.GetRequiredService<IExcelTemplateService>();
        _activity = App.Services.GetRequiredService<ActivityLogger>();
        _catalogSession = App.Services.GetRequiredService<CatalogBrowseSessionStore>();
        DataContext = vm;
        Loaded += OnMasterDataPageLoaded;
        Unloaded += OnMasterDataPageUnloaded;
        DataWorkspace.SizeChanged += (_, _) => ScheduleLinkConnectorUpdate();
        LinkedViewPanel.SizeChanged += (_, _) => ScheduleLinkConnectorUpdate();
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
        vm.ColumnFiltersChanged += OnColumnFiltersChanged;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        var theme = App.Services.GetService<IThemeService>();
        if (theme is not null)
        {
            theme.ThemeChanged += OnAppThemeChanged;
        }

        _navigationArgs ??= new MasterDataNavigationArgs { Mode = MasterDataSurfaceMode.ReadOnlyCatalog };
        if (!_surfaceInitialized)
        {
            await vm.InitializeSurfaceAsync(_navigationArgs).ConfigureAwait(true);
            _surfaceInitialized = true;
        }

        if (_navigationArgs.Mode == MasterDataSurfaceMode.LinkedBrowse)
        {
            ConfigureEmbeddedBrowseChrome();
        }

        UpdateCatalogNavChrome();
        _displayedTemplateId ??= _viewModel.SelectedDataTemplate?.Id;
        RestorePageIndexFromSession();
        RebuildSpreadsheet();
    }

    private CatalogGridBrowseState? CurrentBrowseState()
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(_displayedTemplateId))
        {
            return null;
        }

        return _catalogSession.GetTemplateBrowseState(_viewModel.SurfaceMode, _displayedTemplateId);
    }

    private void RestorePageIndexFromSession()
    {
        var state = CurrentBrowseState();
        if (state is null)
        {
            return;
        }

        _gridPageIndex = Math.Max(0, state.PageIndex);
    }

    private void PersistGridBrowseState(bool includeScrollOffsets = true)
    {
        if (_suspendBrowsePersist)
        {
            return;
        }

        WriteBrowseState(includeScrollOffsets);
    }

    private void WriteBrowseState(bool includeScrollOffsets)
    {
        var state = CurrentBrowseState();
        if (state is null)
        {
            return;
        }

        state.PageIndex = _gridPageIndex;
        if (includeScrollOffsets && _spreadsheetScroll is not null)
        {
            state.ScrollHorizontalOffset = _spreadsheetScroll.HorizontalOffset;
            state.ScrollVerticalOffset = _spreadsheetScroll.VerticalOffset;
        }
    }

    private void RestoreScrollFromSession(ScrollViewer scroll)
    {
        var state = CurrentBrowseState();
        if (state is null)
        {
            return;
        }

        var horizontal = Math.Max(0, state.ScrollHorizontalOffset);
        var vertical = Math.Max(0, state.ScrollVerticalOffset);
        if (horizontal <= 0 && vertical <= 0)
        {
            _suspendBrowsePersist = false;
            return;
        }

        void Apply()
        {
            _suspendBrowsePersist = true;
            scroll.ChangeView(horizontal, vertical, null, disableAnimation: true);
            _ = scroll.DispatcherQueue.TryEnqueue(
                Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                () => _suspendBrowsePersist = false);
        }

        if (scroll.ExtentHeight > 1 || scroll.ExtentWidth > 1)
        {
            Apply();
            return;
        }

        void OnSized(object sender, SizeChangedEventArgs args)
        {
            if (scroll.ExtentHeight <= 1 && scroll.ExtentWidth <= 1 && args.NewSize.Height <= 1)
            {
                return;
            }

            scroll.SizeChanged -= OnSized;
            Apply();
        }

        scroll.SizeChanged += OnSized;
    }

    private void OnSpreadsheetScrollLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scroll)
        {
            scroll.Loaded -= OnSpreadsheetScrollLoaded;
            RestoreScrollFromSession(scroll);
        }
    }

    private void UpdateCatalogNavChrome()
    {
        if (_viewModel is null)
        {
            return;
        }

        var show = _viewModel.ShowTemplatePickerInChrome;
        CatalogNavColumn.Width = show ? new GridLength(220) : new GridLength(0);
        CatalogSidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _navigationArgs = e.Parameter as MasterDataNavigationArgs
                          ?? new MasterDataNavigationArgs { Mode = MasterDataSurfaceMode.ReadOnlyCatalog };
        _surfaceInitialized = false;
    }

    public void ConfigureEmbeddedBrowseChrome()
    {
        RootGrid.Padding = new Thickness(0);
        if (StandalonePageTitlePanel is not null)
        {
            StandalonePageTitlePanel.Visibility = Visibility.Collapsed;
        }

        if (PageTitleHeaderGrid is not null)
        {
            PageTitleHeaderGrid.Margin = new Thickness(0, 0, 0, 4);
        }
    }

    public void ApplyBrowseTemplate(string? templateId)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        _viewModel.SurfaceMode = MasterDataSurfaceMode.LinkedBrowse;
        var existing = _viewModel.DataTemplates.FirstOrDefault(t => t.Id == templateId);
        if (existing is not null)
        {
            _viewModel.SelectedDataTemplate = existing;
            return;
        }

        _ = _viewModel.LoadForTemplateAsync(templateId);
    }

    private void OnMasterDataPageUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnMasterDataPageUnloaded;
        _suspendBrowsePersist = false;
        PersistGridBrowseState();
        if (_viewModel is not null)
        {
            _viewModel.Rows.CollectionChanged -= OnGridStructureChanged;
            _viewModel.Columns.CollectionChanged -= OnGridStructureChanged;
            _viewModel.ColumnFiltersChanged -= OnColumnFiltersChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }

        var theme = App.Services.GetService<IThemeService>();
        if (theme is not null)
        {
            theme.ThemeChanged -= OnAppThemeChanged;
        }
    }

    private void OnAppThemeChanged(object? sender, EventArgs e) => RebuildSpreadsheet();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MasterDataViewModel.ShowTemplatePickerInChrome))
        {
            UpdateCatalogNavChrome();
        }

        if (e.PropertyName is nameof(MasterDataViewModel.IsEditMode)
            or nameof(MasterDataViewModel.SurfaceMode)
            or nameof(MasterDataViewModel.EnableWorksheetHyperlinks))
        {
            if (_viewModel is { IsEditMode: false } && NewColumnNameBox is not null)
            {
                NewColumnNameBox.Text = string.Empty;
            }

            if (e.PropertyName == nameof(MasterDataViewModel.SurfaceMode))
            {
                RestorePageIndexFromSession();
            }

            UpdateCatalogNavChrome();
            RebuildSpreadsheet();
            return;
        }

        if (e.PropertyName == nameof(MasterDataViewModel.SelectedDataTemplate))
        {
            // Keep the page the user was on, even if a scroll restore is still settling.
            WriteBrowseState(includeScrollOffsets: true);
            _suspendBrowsePersist = true;
            _displayedTemplateId = _viewModel?.SelectedDataTemplate?.Id;
            RestorePageIndexFromSession();
            if (_viewModel is { IsLoading: false })
            {
                RebuildSpreadsheet();
            }
        }

        if (e.PropertyName == nameof(MasterDataViewModel.IsLoading)
            && _viewModel is { IsLoading: false })
        {
            if (!string.Equals(
                    _displayedTemplateId,
                    _viewModel.SelectedDataTemplate?.Id,
                    StringComparison.Ordinal))
            {
                // Cached catalogs finish loading before the selection handler
                // switches the saved page. Wait for that handler to rebuild.
                return;
            }

            RestorePageIndexFromSession();
            RebuildSpreadsheet();
        }
    }

    private void OnColumnFiltersChanged(object? sender, EventArgs e)
    {
        _gridPageIndex = 0;
        ScheduleRebuildSpreadsheet();
    }

    private void OnGridStructureChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel is { IsLoading: true })
        {
            return;
        }

        if (e is not null && ReferenceEquals(sender, _viewModel?.Columns))
        {
            _openColumnSearchKeys.Clear();
        }

        if (e.Action == NotifyCollectionChangedAction.Add
            && _viewModel is not null
            && e.NewItems is { Count: > 0 }
            && e.NewItems[e.NewItems.Count - 1] is MasterDataRowViewModel addedRow)
        {
            var visibleIndex = 0;
            foreach (var row in _viewModel.Rows)
            {
                if (!_viewModel.IsRowVisible(row))
                {
                    continue;
                }

                if (ReferenceEquals(row, addedRow))
                {
                    _gridPageIndex = visibleIndex / MasterDataPageSize;
                    break;
                }

                visibleIndex++;
            }
        }

        ScheduleRebuildSpreadsheet();
    }

    private void OnMasterDataPrevPageClick(object sender, RoutedEventArgs e)
    {
        if (_gridPageIndex <= 0)
        {
            return;
        }

        _gridPageIndex--;
        _suspendBrowsePersist = false;
        WriteBrowseState(includeScrollOffsets: true);
        RebuildSpreadsheet();
    }

    private void OnMasterDataNextPageClick(object sender, RoutedEventArgs e)
    {
        _gridPageIndex++;
        _suspendBrowsePersist = false;
        WriteBrowseState(includeScrollOffsets: true);
        RebuildSpreadsheet();
    }

    private void ScheduleRebuildSpreadsheet()
    {
        if (_rebuildSpreadsheetScheduled)
        {
            return;
        }

        _rebuildSpreadsheetScheduled = true;
        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _rebuildSpreadsheetScheduled = false;
            RebuildSpreadsheet();
        });
    }

    private void OnBackFromEditClick(object sender, RoutedEventArgs e)
    {
        var nav = App.Services.GetRequiredService<INavigationService>();
        if (nav.CanGoBack)
        {
            nav.GoBack();
            return;
        }

        nav.Navigate(AppPage.Templates);
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

        if (await dlg.ShowAsync() != ContentDialogResult.Primary
            || !_viewModel.SaveGridCommand.CanExecute(null))
        {
            return;
        }

        if (_viewModel.SaveGridCommand is IAsyncRelayCommand saveAsync)
        {
            await saveAsync.ExecuteAsync(null).ConfigureAwait(true);
        }
        else
        {
            _viewModel.SaveGridCommand.Execute(null);
        }

        var templateName = _viewModel.SelectedDataTemplate?.Name ?? "Unknown";
        _activity.LogStockChange("Data Saved", $"Master Data saved for template '{templateName}' — {_viewModel.Rows.Count} rows");

        if (_viewModel.SurfaceMode == MasterDataSurfaceMode.TemplateEdit
            && !string.IsNullOrWhiteSpace(_viewModel.SelectedDataTemplate?.Id))
        {
            var nav = App.Services.GetRequiredService<INavigationService>();
            nav.Navigate(AppPage.TemplateData, _viewModel.SelectedDataTemplate.Id);
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

        var rowData = _viewModel.Rows
            .Select(row => (IReadOnlyDictionary<string, string>)row.Cells.ToDictionary(
                cell => cell.FieldKey,
                cell => cell.Text ?? string.Empty,
                StringComparer.Ordinal))
            .ToList();
        await _excelTemplateService.ExportTemplateWithDataAsync(_viewModel.SelectedDataTemplate, file.Path, rowData);

        var dlg = new ContentDialog
        {
            Title = "Data exported",
            Content = $"Excel file saved with {rowData.Count} row(s):\n{file.Path}",
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
        ClearLinkHighlight();
        _linkAnchor = null;

        if (_viewModel is null || SpreadsheetHost is null)
        {
            return;
        }

        if (_viewModel.Columns.Count == 0)
        {
            SpreadsheetHost.Content = null;
            _sheetGrid = null;
            UpdatePager(0);
            return;
        }

        var visibleRows = _viewModel.Rows.Where(_viewModel.IsRowVisible).ToList();
        UpdatePager(visibleRows.Count);
        var pageStart = _gridPageIndex * MasterDataPageSize;
        var pageRows = visibleRows.Skip(pageStart).Take(MasterDataPageSize).ToList();
        var colCount = _viewModel.Columns.Count;
        var showDeleteCol = _viewModel.ShowSpreadsheetEditChrome && _viewModel.IsEditMode && _viewModel.CanDeleteRows;
        var gridColCount = colCount + (showDeleteCol ? 1 : 0);
        var rowCount = pageRows.Count;

        var sheetRoot = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        sheetRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        sheetRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        const double columnMinWidth = 180d;
        var viewportWidth = SpreadsheetHost.ActualWidth;
        if (viewportWidth <= 1 && SourceTableHost is not null)
        {
            viewportWidth = SourceTableHost.ActualWidth;
        }

        var deleteColumnWidth = showDeleteCol ? 52d : 0d;
        var dataColumnWidth = columnMinWidth;
        if (viewportWidth > 1 && colCount > 0 && viewportWidth > (colCount * columnMinWidth) + deleteColumnWidth)
        {
            dataColumnWidth = (viewportWidth - deleteColumnWidth) / colCount;
        }

        var tableWidth = (colCount * dataColumnWidth) + deleteColumnWidth;

        var headerGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = tableWidth,
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
        };
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        headerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var dataGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = tableWidth,
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
        };

        var headerScroll = new ScrollViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
        };

        var scroll = new ScrollViewer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalScrollMode = ScrollMode.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };

        scroll.SizeChanged += (_, _) => ScheduleLinkConnectorUpdate();
        scroll.ViewChanged += (_, e) =>
        {
            headerScroll.ChangeView(scroll.HorizontalOffset, null, null, disableAnimation: true);
            if (_suspendBrowsePersist)
            {
                if (!e.IsIntermediate)
                {
                    _suspendBrowsePersist = false;
                }

                ScheduleLinkConnectorUpdate();
                return;
            }

            if (!e.IsIntermediate)
            {
                PersistGridBrowseState();
            }

            ScheduleLinkConnectorUpdate();
        };

        static void AddColumnDefinitions(Grid target, int columns, bool showDelete, double columnWidth)
        {
            for (var c = 0; c < columns; c++)
            {
                var isDelete = showDelete && c == columns - 1;
                target.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(isDelete ? 52 : columnWidth),
                });
            }
        }

        AddColumnDefinitions(headerGrid, gridColCount, showDeleteCol, dataColumnWidth);
        AddColumnDefinitions(dataGrid, gridColCount, showDeleteCol, dataColumnWidth);

        for (var r = 0; r < rowCount; r++)
        {
            dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        var headerBg = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"];
        var borderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"];

        for (var c = 0; c < colCount; c++)
        {
            var field = _viewModel.Columns[c];
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

            var headerHost = new Grid
            {
                MinHeight = 32,
                ColumnSpacing = 4,
            };
            headerHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (_viewModel.CanRemoveColumns)
            {
                headerHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }

            var titleBlock = new TextBlock
            {
                Text = headerLabel,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(titleBlock, 0);
            headerHost.Children.Add(titleBlock);

            var fieldKey = field.Key;
            var filterOpen = _openColumnSearchKeys.Contains(fieldKey);
            _viewModel.TryGetColumnFilter(fieldKey, out var activeFilter);
            var searchBtn = new Button
            {
                Width = 28,
                Height = 28,
                MinWidth = 28,
                MinHeight = 28,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = filterOpen
                    ? (Brush)Application.Current.Resources["RowSelectedBackgroundBrush"]
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            ToolTipService.SetToolTip(searchBtn, filterOpen ? "Hide column search" : "Search this column");
            searchBtn.Content = new FontIcon
            {
                Glyph = "\uE721",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            };
            searchBtn.Click += (_, _) =>
            {
                if (_openColumnSearchKeys.Contains(fieldKey))
                {
                    _openColumnSearchKeys.Remove(fieldKey);
                }
                else
                {
                    _openColumnSearchKeys.Add(fieldKey);
                }

                RebuildSpreadsheet();
            };
            Grid.SetColumn(searchBtn, 1);
            headerHost.Children.Add(searchBtn);

            if (_viewModel.CanRemoveColumns)
            {
                var columnIndex = c;
                var columnLabel = field.Label;
                var removeBtn = new Button
                {
                    Width = 18,
                    Height = 18,
                    MinWidth = 18,
                    MinHeight = 18,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x22, 0xE0, 0x52, 0x52)),
                    BorderBrush = (Brush)Application.Current.Resources["DangerBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                };
                ToolTipService.SetToolTip(removeBtn, $"Remove column \"{columnLabel}\"");
                removeBtn.Content = new FontIcon
                {
                    Glyph = "\uE711",
                    FontSize = 8,
                    Foreground = (Brush)Application.Current.Resources["DangerBrush"],
                };
                Grid.SetColumn(removeBtn, 2);
                removeBtn.Click += (_, _) =>
                {
                    if (_viewModel is null || !_viewModel.CanRemoveColumns)
                    {
                        return;
                    }

                    _ = ConfirmAndRemoveColumnAsync(columnIndex, columnLabel);
                };
                headerHost.Children.Add(removeBtn);
            }

            headerBorder.Child = headerHost;

            headerGrid.Children.Add(headerBorder);

            var filterBorder = new Border
            {
                Background = headerBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(6, 4, 6, 6),
                Visibility = filterOpen ? Visibility.Visible : Visibility.Collapsed,
            };
            var filterBox = new TextBox
            {
                PlaceholderText = "Search column…",
                FontSize = 12,
                Text = activeFilter ?? string.Empty,
            };
            filterBox.TextChanged += (_, _) =>
            {
                if (_viewModel is null)
                {
                    return;
                }

                _viewModel.SetColumnFilter(fieldKey, filterBox.Text);
            };
            filterBorder.Child = filterBox;
            Grid.SetRow(filterBorder, 1);
            Grid.SetColumn(filterBorder, c);
            headerGrid.Children.Add(filterBorder);
        }

        if (showDeleteCol)
        {
            var deleteHeader = new Border
            {
                Background = headerBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(4, 10, 4, 10),
                Child = new TextBlock
                {
                    Text = "×",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current.Resources["DangerBrush"],
                },
            };
            Grid.SetRow(deleteHeader, 0);
            Grid.SetRowSpan(deleteHeader, 2);
            Grid.SetColumn(deleteHeader, colCount);
            headerGrid.Children.Add(deleteHeader);
        }

        for (var r = 0; r < rowCount; r++)
        {
            var rowVm = pageRows[r];
            var rowVisible = _viewModel.IsRowVisible(rowVm);
            for (var c = 0; c < colCount; c++)
            {
                var cellVm = rowVm.Cells[c];
                UIElement cellContent;
                if (cellVm.IsRecordLink)
                {
                    cellContent = BuildLinkCell(cellVm, rowVm);
                }
                else
                {
                    cellContent = BuildTextCell(cellVm, rowVm);
                }

                var cellBorder = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources[
                        r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Tag = new LinkSourceCellTag(rowVm, cellVm.FieldKey),
                    Visibility = rowVisible ? Visibility.Visible : Visibility.Collapsed,
                    Child = cellContent,
                };
                Grid.SetRow(cellBorder, r);
                Grid.SetColumn(cellBorder, c);
                dataGrid.Children.Add(cellBorder);
            }

            if (showDeleteCol)
            {
                var capturedRow = rowVm;
                var deleteBtn = new Button
                {
                    Width = 32,
                    Height = 32,
                    MinWidth = 32,
                    MinHeight = 32,
                    Padding = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x22, 0xE0, 0x52, 0x52)),
                    BorderBrush = (Brush)Application.Current.Resources["DangerBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Content = new FontIcon
                    {
                        Glyph = "\uE711",
                        FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["DangerBrush"],
                    },
                };
                ToolTipService.SetToolTip(deleteBtn, "Delete row");
                deleteBtn.Click += (_, _) => _ = ConfirmAndDeleteRowAsync(capturedRow);

                var deleteCell = new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Background = (Brush)Application.Current.Resources[
                        r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = deleteBtn,
                };
                Grid.SetRow(deleteCell, r);
                Grid.SetColumn(deleteCell, colCount);
                dataGrid.Children.Add(deleteCell);
            }
        }

        if (_viewModel.IsEditMode)
        {
            // Canva-like affordance: insert points between rows (and at the end), pinned to last column.
            for (var insertAfterRow = 0; insertAfterRow <= rowCount; insertAfterRow++)
            {
                dataGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
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
                addRowButton.Click += (_, _) => InsertRowFromPage(insertionIndex);

                var addRowHost = new Border
                {
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
                    Child = addRowButton,
                };

                Grid.SetRow(addRowHost, rowCount + insertAfterRow);
                Grid.SetColumn(addRowHost, gridColCount - 1);
                dataGrid.Children.Add(addRowHost);

                addRowHost.PointerEntered += (_, _) => AffordanceAnimationHelper.Fade(addRowButton, show: true, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
                addRowHost.PointerExited += (_, _) => AffordanceAnimationHelper.Fade(addRowButton, show: false, shownOpacity: 1, hiddenOpacity: 0, disableHitTestingWhenHidden: true);
            }
        }

        headerScroll.Content = headerGrid;
        scroll.Content = dataGrid;
        Grid.SetRow(headerScroll, 0);
        Grid.SetRow(scroll, 1);
        sheetRoot.Children.Add(headerScroll);
        sheetRoot.Children.Add(scroll);
        SpreadsheetHost.Content = sheetRoot;
        _spreadsheetScroll = scroll;
        _sheetGrid = dataGrid;
        RefreshColumnManagementCombos();
        if (scroll.IsLoaded)
        {
            RestoreScrollFromSession(scroll);
        }
        else
        {
            scroll.Loaded += OnSpreadsheetScrollLoaded;
        }

        if (LinkedViewPanel is { Visibility: Visibility.Visible })
        {
            HighlightLinkAnchor();
        }
    }

    private sealed class ColumnInsertOption
    {
        public required string Label { get; init; }
        public int InsertIndex { get; init; }
    }

    private void RefreshColumnManagementCombos()
    {
        if (_viewModel is null || InsertAfterColumnCombo is null)
        {
            return;
        }

        if (!_viewModel.CanManageColumns)
        {
            return;
        }

        InsertAfterColumnCombo.Items.Clear();
        InsertAfterColumnCombo.Items.Add(new ColumnInsertOption { Label = "At end of table", InsertIndex = _viewModel.Columns.Count });
        for (var i = 0; i < _viewModel.Columns.Count; i++)
        {
            var col = _viewModel.Columns[i];
            InsertAfterColumnCombo.Items.Add(new ColumnInsertOption
            {
                Label = $"After \"{col.Label}\"",
                InsertIndex = i + 1,
            });
        }

        InsertAfterColumnCombo.SelectedIndex = 0;
    }

    private void InsertRowFromPage(int pageLocalIndex)
    {
        if (_viewModel is null)
        {
            return;
        }

        var visible = _viewModel.Rows.Where(_viewModel.IsRowVisible).ToList();
        var absolute = _gridPageIndex * MasterDataPageSize + pageLocalIndex;
        int rowsIndex;
        if (absolute >= visible.Count || visible.Count == 0)
        {
            rowsIndex = _viewModel.Rows.Count;
        }
        else
        {
            var index = _viewModel.Rows.IndexOf(visible[absolute]);
            rowsIndex = index < 0 ? _viewModel.Rows.Count : index;
        }

        _viewModel.InsertRowAt(rowsIndex);
    }

    private void UpdatePager(int visibleCount)
    {
        if (MasterDataPageStatusText is null || MasterDataPrevPageButton is null || MasterDataNextPageButton is null)
        {
            return;
        }

        var pageCount = Math.Max(1, (visibleCount + MasterDataPageSize - 1) / MasterDataPageSize);
        if (visibleCount > 0 && _gridPageIndex >= pageCount)
        {
            _gridPageIndex = pageCount - 1;
        }

        if (_gridPageIndex < 0)
        {
            _gridPageIndex = 0;
        }

        if (visibleCount == 0)
        {
            MasterDataPageStatusText.Text = "No rows";
        }
        else
        {
            var start = _gridPageIndex * MasterDataPageSize + 1;
            var end = Math.Min(visibleCount, (_gridPageIndex + 1) * MasterDataPageSize);
            MasterDataPageStatusText.Text = $"Showing {start}–{end} of {visibleCount}";
        }

        MasterDataPrevPageButton.IsEnabled = _gridPageIndex > 0;
        MasterDataNextPageButton.IsEnabled = visibleCount > 0 && _gridPageIndex < pageCount - 1;
    }

    private async Task ConfirmAndRemoveColumnAsync(int columnIndex, string columnLabel)
    {
        if (_viewModel is null || XamlRoot is null || !_viewModel.CanRemoveColumns)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            Title = "Remove column?",
            Content = $"Do you confirm deleting the column \"{columnLabel}\"? This removes it from the template for everyone.",
            PrimaryButtonText = "Delete column",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var ok = await _viewModel.RemoveColumnAtAsync(columnIndex).ConfigureAwait(true);
        if (!ok && string.IsNullOrWhiteSpace(_viewModel.StatusMessage))
        {
            _viewModel.StatusMessage = "Could not remove that column.";
        }

        RebuildSpreadsheet();
    }

    private string? GetNewColumnNameFromBar()
    {
        var name = NewColumnNameBox?.Text?.Trim();
        return string.IsNullOrEmpty(name) ? null : name;
    }

    private async void OnAddColumnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || !_viewModel.CanManageColumns)
        {
            return;
        }

        var name = GetNewColumnNameFromBar();
        if (name is null)
        {
            _viewModel.StatusMessage = "Enter a column name, then click Add column.";
            NewColumnNameBox?.Focus(FocusState.Programmatic);
            return;
        }

        var insertAt = _viewModel.Columns.Count;
        if (InsertAfterColumnCombo?.SelectedItem is ColumnInsertOption opt)
        {
            insertAt = opt.InsertIndex;
        }

        if (await _viewModel.InsertColumnAtAsync(insertAt, name).ConfigureAwait(true))
        {
            if (NewColumnNameBox is not null)
            {
                NewColumnNameBox.Text = string.Empty;
            }

            _viewModel.IsAddColumnPanelVisible = false;
        }
    }

    private void OnShowAddColumnPanelClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.IsAddColumnPanelVisible = true;
        NewColumnNameBox?.Focus(FocusState.Programmatic);
    }

    private void OnHideAddColumnPanelClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.IsAddColumnPanelVisible = false;
        if (NewColumnNameBox is not null)
        {
            NewColumnNameBox.Text = string.Empty;
        }
    }

    private async Task ConfirmAndDeleteRowAsync(MasterDataRowViewModel rowVm)
    {
        if (_viewModel is null || XamlRoot is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            Title = "Delete row?",
            Content = "Remove this row from the table? Saved rows are deleted from the database.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (await _viewModel.DeleteRowAsync(rowVm).ConfigureAwait(true))
        {
            RebuildSpreadsheet();
        }
    }

    private void OnNewColumnNameKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter && _viewModel?.CanManageColumns == true)
        {
            e.Handled = true;
            OnAddColumnClick(sender, e);
        }
    }

    private UIElement BuildTextCell(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var isViewMode = _viewModel is not null && !_viewModel.IsEditMode;
        var hasRelationLinks = _viewModel?.HasWorksheetRelationsForFieldKey(cellVm.FieldKey) == true;
        var display = string.IsNullOrWhiteSpace(cellVm.Text) ? "—" : cellVm.Text;
        if (isViewMode
            && _viewModel?.EnableWorksheetHyperlinks == true
            && !string.IsNullOrWhiteSpace(cellVm.Text)
            && hasRelationLinks)
        {
            return BuildLinkedValueHyperlink(cellVm, rowVm, display);
        }

        if (isViewMode && _viewModel?.IsCatalogReadOnly == true)
        {
            return new TextBlock
            {
                Text = display,
                MinHeight = 36,
                Padding = new Thickness(GridCellHorizontalPadding, 8, GridCellHorizontalPadding, 8),
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var box = new TextBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
        if (_viewModel?.IsCatalogReadOnly != true)
        {
            box.ContextFlyout = BuildTextCellMenu(cellVm, rowVm);
        }

        return box;
    }

    private UIElement BuildLinkCell(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var isViewMode = _viewModel is not null && !_viewModel.IsEditMode;
        var display = string.IsNullOrWhiteSpace(cellVm.Text) ? cellVm.LinkedRowId ?? string.Empty : cellVm.Text;
        var allowRelationLinks = _viewModel?.EnableWorksheetHyperlinks == true;
        if (isViewMode && _viewModel?.IsCatalogReadOnly == true)
        {
            return new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(display) ? "—" : display,
                MinHeight = 36,
                Padding = new Thickness(GridCellHorizontalPadding, 8, GridCellHorizontalPadding, 8),
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        if (isViewMode
            && allowRelationLinks
            && (!string.IsNullOrWhiteSpace(cellVm.LinkedRowId)
                || _viewModel?.HasWorksheetRelationsForFieldKey(cellVm.FieldKey) == true))
        {
            var link = BuildLinkedValueHyperlink(
                cellVm,
                rowVm,
                string.IsNullOrWhiteSpace(display) ? "Open linked data" : display);
            var outerView = new Border { MinHeight = 36, Padding = new Thickness(GridCellHorizontalPadding, 6, GridCellHorizontalPadding, 6) };
            outerView.ContextFlyout = BuildLinkCellMenu(cellVm, rowVm);
            outerView.Child = link;
            return outerView;
        }

        var box = new TextBox
        {
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(GridCellHorizontalPadding, 6, GridCellHorizontalPadding, 6),
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
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
        outer.ContextFlyout = BuildLinkCellMenu(cellVm, rowVm);
        outer.Child = rowPanel;
        return outer;
    }

    private HyperlinkButton BuildLinkedValueHyperlink(
        MasterDataCellViewModel cellVm,
        MasterDataRowViewModel rowVm,
        string displayText)
    {
        var link = new HyperlinkButton
        {
            Content = displayText,
            FontSize = 12,
            Padding = new Thickness(0),
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["AccentPrimaryBrush"],
        };
        ToolTipService.SetToolTip(link, "Open rows from templates linked to this column");
        if (ReferenceEquals(rowVm, _activeLinkRow)
            && string.Equals(cellVm.FieldKey, _activeLinkFieldKey, StringComparison.OrdinalIgnoreCase))
        {
            _linkAnchor = link;
        }

        link.Click += async (_, _) =>
        {
            _activeLinkRow = rowVm;
            _activeLinkFieldKey = cellVm.FieldKey;
            _activeLinkLabel = displayText;
            _linkAnchor = link;
            HideLinkedColumnView();
            await ShowLinkedDataForCellAsync(cellVm, rowVm).ConfigureAwait(true);
        };
        return link;
    }

    private MenuFlyout BuildTextCellMenu(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();
            var copy = new MenuFlyoutItem { Text = "Copy cell value" };
            copy.Click += (_, _) => CopyToClipboard(cellVm.EffectiveCopyValue);
            flyout.Items.Add(copy);
        };
        return flyout;
    }

    private MenuFlyout BuildLinkCellMenu(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();
            PopulateLinkCellMenuItems(flyout, cellVm, rowVm);
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
        if (_viewModel is null || XamlRoot is null)
        {
            return;
        }

        var sourceMap = MasterDataViewModel.BuildSourceRowMap(rowVm);
        var (targetDef, rows) = await _viewModel.RunContextActionAsync(action, sourceMap).ConfigureAwait(true);
        if (targetDef is null)
        {
            return;
        }

        PopulateLinkedDataTabs(
        [
            new LinkedDataTabModel(
                action.MenuLabel,
                targetDef,
                null,
                rows,
                action.DisplayFieldKeys),
        ]);
        OpenActionPanel();
    }

    private async Task ShowLinkedDataForCellAsync(MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        if (_viewModel is null)
        {
            return;
        }

        var tabs = new List<LinkedDataTabModel>();
        var relationResults = await _viewModel
            .QueryWorksheetRelationsForCellAsync(cellVm.FieldKey, rowVm)
            .ConfigureAwait(true);
        foreach (var result in relationResults)
        {
            tabs.Add(new LinkedDataTabModel(
                result.TabHeader,
                result.LookupTemplate,
                result.Relation,
                result.Matches));
        }

        if (cellVm.IsRecordLink && !string.IsNullOrWhiteSpace(cellVm.LinkedRowId))
        {
            var directLink = await _viewModel.QueryDirectRecordLinkAsync(cellVm).ConfigureAwait(true);
            if (directLink is not null)
            {
                tabs.Insert(
                    0,
                    new LinkedDataTabModel(
                        directLink.TabHeader,
                        directLink.LookupTemplate,
                        directLink.Relation,
                        directLink.Matches));
            }
        }

        if (tabs.Count == 0)
        {
            ActionTitleText.Text = "Linked data";
            ActionCountText.Text =
                "No rows in the linked template have the same value in this column. "
                + "Add or update vendor rows with a matching value, or adjust keys in Link Templates.";
            LinkedDataTabView.TabItems.Clear();
            OpenActionPanel();
            return;
        }

        PopulateLinkedDataTabs(tabs);
        OpenActionPanel();
    }

    private void PopulateLinkedDataTabs(IReadOnlyList<LinkedDataTabModel> tabs)
    {
        if (LinkedDataTabView is null)
        {
            return;
        }

        LinkedDataTabView.TabItems.Clear();
        var totalMatches = tabs.Sum(t => t.Matches.Count);
        ActionTitleText.Text = "Linked data";
        ActionCountText.Text = $"{tabs.Count} linked template(s) · {totalMatches} matching row(s)";

        foreach (var tab in tabs)
        {
            var tabItem = new TabViewItem
            {
                Header = tab.Header,
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource { Symbol = Symbol.Link },
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                Content = BuildLinkedTabContent(tab),
            };
            LinkedDataTabView.TabItems.Add(tabItem);
        }

        if (LinkedDataTabView.TabItems.Count > 0)
        {
            LinkedDataTabView.SelectedIndex = 0;
        }
    }

    private UIElement BuildLinkedTabContent(LinkedDataTabModel tab)
    {
        var allFields = MasterDataViewModel.GetAllBrowsableFields(tab.LookupTemplate);
        var selectedKeys = new HashSet<string>(
            MasterDataViewModel.GetDefaultSelectedFieldKeysForBrowse(tab.LookupTemplate, tab.Relation),
            StringComparer.OrdinalIgnoreCase);
        var lockedFieldKey = GetLockedLookupFieldKeyForActiveLink(tab, allFields);
        if (!string.IsNullOrWhiteSpace(lockedFieldKey))
        {
            selectedKeys.Add(lockedFieldKey);
        }

        var root = new StackPanel { Spacing = 4, Padding = new Thickness(0, 4, 0, 8) };
        root.Children.Add(new TextBlock
        {
            Text = tab.LookupTemplate.Name,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            TextWrapping = TextWrapping.WrapWholeWords,
            Margin = new Thickness(0, 0, 0, 6),
        });
        root.Children.Add(new TextBlock
        {
            Text = "Columns to show",
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 4),
            Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"],
        });

        var viewButton = new Button
        {
            Style = (Style)Application.Current.Resources["PrimaryButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            MinHeight = 36,
            Padding = new Thickness(12, 6, 12, 6),
            Content = "View",
        };
        ToolTipService.SetToolTip(viewButton, "Show the selected columns beside this table");

        foreach (var field in allFields)
        {
            var capturedField = field;
            var isLocked =
                !string.IsNullOrWhiteSpace(lockedFieldKey)
                && string.Equals(field.Key, lockedFieldKey, StringComparison.OrdinalIgnoreCase);
            var check = new CheckBox
            {
                Content = field.Label,
                FontSize = 12,
                IsChecked = isLocked || selectedKeys.Contains(field.Key),
                IsEnabled = !isLocked,
            };
            if (isLocked)
            {
                ToolTipService.SetToolTip(
                    check,
                    "This column stays selected because you opened linked data from this field.");
            }

            check.Checked += (_, _) =>
            {
                selectedKeys.Add(capturedField.Key);
                viewButton.IsEnabled = selectedKeys.Count > 0;
            };
            check.Unchecked += (_, _) =>
            {
                if (isLocked)
                {
                    check.IsChecked = true;
                    return;
                }

                selectedKeys.Remove(capturedField.Key);
                viewButton.IsEnabled = selectedKeys.Count > 0;
            };
            root.Children.Add(check);
        }

        viewButton.IsEnabled = selectedKeys.Count > 0;
        viewButton.Click += (_, _) => ShowLinkedColumnView(tab, allFields, selectedKeys);
        root.Children.Add(viewButton);

        return new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = root,
        };
    }

    private string? GetLockedLookupFieldKeyForActiveLink(
        LinkedDataTabModel tab,
        IReadOnlyList<TemplateFieldDefinition> lookupFields)
    {
        if (_viewModel is null || string.IsNullOrWhiteSpace(_activeLinkFieldKey))
        {
            return null;
        }

        var sourceField = _viewModel.Columns.FirstOrDefault(c =>
            string.Equals(c.Key, _activeLinkFieldKey, StringComparison.OrdinalIgnoreCase));
        var sourceLabel = sourceField?.Label.Trim() ?? string.Empty;

        if (tab.Relation is not null && !string.IsNullOrEmpty(sourceLabel))
        {
            foreach (var pair in tab.Relation.MatchKeys)
            {
                var pairSource = pair.SourceColumn?.Trim() ?? string.Empty;
                if (!string.Equals(pairSource, sourceLabel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetLabel = string.IsNullOrWhiteSpace(pair.TargetColumn)
                    ? pairSource
                    : pair.TargetColumn.Trim();
                var targetField = lookupFields.FirstOrDefault(f =>
                    string.Equals(f.Label, targetLabel, StringComparison.OrdinalIgnoreCase));
                if (targetField is not null)
                {
                    return targetField.Key;
                }
            }

            if (string.Equals(tab.Relation.TriggerColumn?.Trim(), sourceLabel, StringComparison.OrdinalIgnoreCase))
            {
                var triggerField = lookupFields.FirstOrDefault(f =>
                    string.Equals(f.Label, sourceLabel, StringComparison.OrdinalIgnoreCase));
                if (triggerField is not null)
                {
                    return triggerField.Key;
                }
            }
        }

        if (!string.IsNullOrEmpty(sourceLabel))
        {
            var sameLabel = lookupFields.FirstOrDefault(f =>
                string.Equals(f.Label, sourceLabel, StringComparison.OrdinalIgnoreCase));
            if (sameLabel is not null)
            {
                return sameLabel.Key;
            }
        }

        return lookupFields.FirstOrDefault(f =>
            string.Equals(f.Key, _activeLinkFieldKey, StringComparison.OrdinalIgnoreCase))?.Key;
    }

    private void ShowLinkedColumnView(
        LinkedDataTabModel tab,
        IReadOnlyList<TemplateFieldDefinition> allFields,
        IReadOnlySet<string> selectedKeys)
    {
        if (LinkedViewColumn is null || LinkedViewPanel is null || LinkedViewScroll is null
            || SourceTableColumn is null || LinkedViewTitleText is null || LinkedViewSubtitleText is null)
        {
            return;
        }

        var fields = allFields.Where(f => selectedKeys.Contains(f.Key)).ToList();
        if (fields.Count == 0)
        {
            return;
        }

        LinkedViewTitleText.Text = tab.LookupTemplate.Name;
        var fromLabel = string.IsNullOrWhiteSpace(_activeLinkLabel) ? "this link" : _activeLinkLabel;
        LinkedViewSubtitleText.Text =
            $"From {fromLabel} · {fields.Count} column(s) · {tab.Matches.Count} matching row(s)";
        LinkedViewScroll.Content = BuildLinkedResultGrid(tab, fields);

        SourceTableColumn.Width = new GridLength(1, GridUnitType.Star);
        LinkedViewColumn.Width = new GridLength(1.2, GridUnitType.Star);
        LinkedViewPanel.Visibility = Visibility.Visible;
        LinkedViewPanel.Opacity = 1;
        HideConnectorGraphics();
        if (LinkConnectorCanvas is not null)
        {
            LinkConnectorCanvas.Visibility = Visibility.Collapsed;
        }

        HighlightLinkAnchor();
    }

    private static UIElement BuildLinkedResultGrid(
        LinkedDataTabModel tab,
        IReadOnlyList<TemplateFieldDefinition> fields)
    {
        var borderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"];
        var headerBg = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"];
        var primary = (Brush)Application.Current.Resources["TextPrimaryBrush"];
        var rowCount = tab.Matches.Count;

        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],
        };

        for (var c = 0; c < fields.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var r = 0; r < Math.Max(rowCount, 1); r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var c = 0; c < fields.Count; c++)
        {
            var header = new Border
            {
                Background = headerBg,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(10, 10, 10, 10),
                Child = new TextBlock
                {
                    Text = fields[c].Label,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = primary,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }

        if (rowCount == 0)
        {
            var empty = new TextBlock
            {
                Text = "No matching rows for this link.",
                FontSize = 12,
                Margin = new Thickness(10, 12, 10, 12),
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                TextWrapping = TextWrapping.WrapWholeWords,
            };
            Grid.SetRow(empty, 1);
            Grid.SetColumn(empty, 0);
            Grid.SetColumnSpan(empty, Math.Max(fields.Count, 1));
            grid.Children.Add(empty);
            return grid;
        }

        for (var r = 0; r < rowCount; r++)
        {
            var match = tab.Matches[r];
            for (var c = 0; c < fields.Count; c++)
            {
                var raw = match.Values.TryGetValue(fields[c].Key, out var value) ? value : null;
                var cell = new Border
                {
                    Background = (Brush)Application.Current.Resources[
                        r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(10, 8, 10, 8),
                    MinHeight = 36,
                    Child = new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(raw) ? "—" : raw,
                        FontSize = 12,
                        Foreground = primary,
                        TextWrapping = TextWrapping.Wrap,
                        IsTextSelectionEnabled = true,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        return grid;
    }

    private void OnCloseLinkedViewClick(object sender, RoutedEventArgs e)
    {
        HideLinkedColumnView();
    }

    private void HideLinkedColumnView()
    {
        ClearLinkHighlight();
        if (LinkedViewScroll is not null)
        {
            LinkedViewScroll.Content = null;
        }

        if (LinkedViewPanel is not null)
        {
            LinkedViewPanel.Visibility = Visibility.Collapsed;
            LinkedViewPanel.Opacity = 1;
        }

        if (LinkedViewColumn is not null)
        {
            LinkedViewColumn.Width = new GridLength(0);
        }

        if (SourceTableColumn is not null)
        {
            SourceTableColumn.Width = new GridLength(1, GridUnitType.Star);
        }

        HideConnectorGraphics();
        if (LinkConnectorCanvas is not null)
        {
            LinkConnectorCanvas.Visibility = Visibility.Collapsed;
        }
    }

    private void ClearActiveLink()
    {
        _activeLinkRow = null;
        _activeLinkFieldKey = null;
        _activeLinkLabel = string.Empty;
        _linkAnchor = null;
        HideLinkedColumnView();
    }

    private void HighlightLinkAnchor()
    {
        ClearLinkHighlight();
        if (_linkAnchor is null || _activeLinkRow is null || string.IsNullOrWhiteSpace(_activeLinkFieldKey))
        {
            return;
        }

        DependencyObject? node = _linkAnchor;
        while (node is not null)
        {
            if (node is Border border && border.Tag is LinkSourceCellTag tag
                && ReferenceEquals(tag.Row, _activeLinkRow)
                && string.Equals(tag.FieldKey, _activeLinkFieldKey, StringComparison.OrdinalIgnoreCase))
            {
                _highlightedCell = border;
                _highlightedCellBackground = border.Background;
                _highlightedCellBorderBrush = border.BorderBrush;
                _highlightedCellBorderThickness = border.BorderThickness;
                border.BorderBrush = (Brush)Application.Current.Resources["AccentPrimaryBrush"];
                border.BorderThickness = new Thickness(2);
                if (Application.Current.Resources["AccentPrimaryBrush"] is SolidColorBrush accent)
                {
                    var color = accent.Color;
                    border.Background = new SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(0x55, color.R, color.G, color.B));
                }

                return;
            }

            node = VisualTreeHelper.GetParent(node);
        }
    }

    private void ClearLinkHighlight()
    {
        if (_highlightedCell is null)
        {
            return;
        }

        _highlightedCell.Background = _highlightedCellBackground;
        _highlightedCell.BorderBrush = _highlightedCellBorderBrush;
        _highlightedCell.BorderThickness = _highlightedCellBorderThickness;
        _highlightedCell = null;
        _highlightedCellBackground = null;
        _highlightedCellBorderBrush = null;
    }

    private void ScheduleLinkConnectorUpdate()
    {
        if (_connectorUpdateQueued || LinkedViewPanel is not { Visibility: Visibility.Visible })
        {
            return;
        }

        _connectorUpdateQueued = true;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            _connectorUpdateQueued = false;
            HideConnectorGraphics();
            if (LinkConnectorCanvas is not null)
            {
                LinkConnectorCanvas.Visibility = Visibility.Collapsed;
            }

            HighlightLinkAnchor();
        });
    }

    private void UpdateLinkConnector()
    {
        HideConnectorGraphics();
        if (LinkConnectorCanvas is not null)
        {
            LinkConnectorCanvas.Visibility = Visibility.Collapsed;
        }

        return;

#pragma warning disable CS0162
        if (LinkConnectorCanvas is null || LinkConnectorPath is null || LinkConnectorGlow is null
            || LinkSourceNode is null || LinkSourceHalo is null
            || LinkTargetNode is null || LinkTargetHalo is null
            || LinkedViewPanel is not { Visibility: Visibility.Visible }
            || _linkAnchor is null
            || SpreadsheetHost is null)
        {
            HideConnectorGraphics();
            return;
        }

        LinkConnectorCanvas.Visibility = Visibility.Visible;
#pragma warning restore CS0162
        if (_linkAnchor.ActualWidth <= 0 || _linkAnchor.ActualHeight <= 0
            || LinkedViewPanel.ActualWidth <= 0 || LinkedViewPanel.ActualHeight <= 0)
        {
            HideConnectorGraphics();
            return;
        }

        if (!TryGetCanvasPoint(_linkAnchor, new Point(_linkAnchor.ActualWidth + 2, _linkAnchor.ActualHeight / 2), out var start)
            || !TryGetCanvasPoint(LinkedViewPanel, new Point(0, 0), out var panelOrigin)
            || !TryGetCanvasPoint(SpreadsheetHost, new Point(0, 0), out var hostOrigin))
        {
            HideConnectorGraphics();
            return;
        }

        var hostBottom = hostOrigin.Y + SpreadsheetHost.ActualHeight;
        if (start.Y < hostOrigin.Y - 4 || start.Y > hostBottom + 4)
        {
            HideConnectorGraphics();
            return;
        }

        var panelTop = panelOrigin.Y + 20;
        var panelBottom = panelOrigin.Y + LinkedViewPanel.ActualHeight - 20;
        if (panelBottom < panelTop)
        {
            panelBottom = panelTop;
        }

        var end = new Point(panelOrigin.X, Math.Clamp(start.Y, panelTop, panelBottom));
        if (end.X - start.X < 8)
        {
            HideConnectorGraphics();
            return;
        }

        try
        {
            // Each Path must own its own geometry. Sharing one instance throws ArgumentException.
            LinkConnectorGlow.Data = CreateConnectorGeometry(start, end);
            LinkConnectorPath.Data = CreateConnectorGeometry(start, end);
        }
        catch (ArgumentException)
        {
            HideConnectorGraphics();
            return;
        }

        PlaceNode(LinkSourceHalo, start);
        PlaceNode(LinkSourceNode, start);
        PlaceNode(LinkTargetHalo, end);
        PlaceNode(LinkTargetNode, end);
    }

    private static PathGeometry CreateConnectorGeometry(Point start, Point end)
    {
        var midX = start.X + ((end.X - start.X) * 0.55);
        var figure = new PathFigure
        {
            StartPoint = start,
            IsClosed = false,
            IsFilled = false,
        };
        figure.Segments.Add(new BezierSegment
        {
            Point1 = new Point(midX, start.Y),
            Point2 = new Point(midX, end.Y),
            Point3 = end,
        });
        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void HideConnectorGraphics()
    {
        if (LinkConnectorGlow is not null)
        {
            LinkConnectorGlow.Data = null;
        }

        if (LinkConnectorPath is not null)
        {
            LinkConnectorPath.Data = null;
        }

        if (LinkSourceHalo is not null)
        {
            LinkSourceHalo.Visibility = Visibility.Collapsed;
        }

        if (LinkSourceNode is not null)
        {
            LinkSourceNode.Visibility = Visibility.Collapsed;
        }

        if (LinkTargetHalo is not null)
        {
            LinkTargetHalo.Visibility = Visibility.Collapsed;
        }

        if (LinkTargetNode is not null)
        {
            LinkTargetNode.Visibility = Visibility.Collapsed;
        }
    }

    private bool TryGetCanvasPoint(UIElement element, Point localPoint, out Point canvasPoint)
    {
        canvasPoint = default;
        if (LinkConnectorCanvas is null || element.XamlRoot is null)
        {
            return false;
        }

        try
        {
            canvasPoint = element.TransformToVisual(LinkConnectorCanvas).TransformPoint(localPoint);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void PlaceNode(FrameworkElement node, Point center)
    {
        node.Visibility = Visibility.Visible;
        Canvas.SetLeft(node, center.X - (node.Width / 2));
        Canvas.SetTop(node, center.Y - (node.Height / 2));
    }

    private sealed class LinkedDataTabModel(
        string header,
        PartTemplateDefinition lookupTemplate,
        WorksheetRelationDto? relation,
        IReadOnlyList<MasterDataRowRecord> matches,
        IReadOnlyList<string>? displayFieldKeys = null)
    {
        public string Header { get; } = header;
        public PartTemplateDefinition LookupTemplate { get; } = lookupTemplate;
        public WorksheetRelationDto? Relation { get; } = relation;
        public IReadOnlyList<MasterDataRowRecord> Matches { get; } = matches;
        public IReadOnlyList<string>? DisplayFieldKeys { get; } = displayFieldKeys;
    }

    private void OnCloseActionPanelClick(object sender, RoutedEventArgs e)
    {
        CloseActionPanel();
    }

    private void OpenActionPanel()
    {
        if (ActionResultsPanel is null || ActionResultsColumn is null || ActionResultsTransform is null)
        {
            return;
        }

        ActionResultsColumn.Width = new GridLength(ActionPanelWidth);
        ActionResultsPanel.Visibility = Visibility.Visible;
        ActionResultsTransform.X = 48;
        ActionResultsPanel.Opacity = 0;

        var slide = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(280),
            From = 48,
            To = 0,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(slide, ActionResultsTransform);
        Storyboard.SetTargetProperty(slide, "X");

        var fade = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(280),
            From = 0,
            To = 1,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(fade, ActionResultsPanel);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(slide);
        storyboard.Children.Add(fade);
        storyboard.Begin();
    }

    private void CloseActionPanel()
    {
        ClearActiveLink();
        if (ActionResultsPanel is null || ActionResultsColumn is null || ActionResultsTransform is null)
        {
            return;
        }

        var slide = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(220),
            From = 0,
            To = 48,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(slide, ActionResultsTransform);
        Storyboard.SetTargetProperty(slide, "X");

        var fade = new DoubleAnimation
        {
            Duration = TimeSpan.FromMilliseconds(220),
            From = 1,
            To = 0,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(fade, ActionResultsPanel);
        Storyboard.SetTargetProperty(fade, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(slide);
        storyboard.Children.Add(fade);
        storyboard.Completed += (_, _) =>
        {
            ActionResultsPanel.Visibility = Visibility.Collapsed;
            ActionResultsColumn.Width = new GridLength(0);
            LinkedDataTabView?.TabItems.Clear();
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

    private void PopulateLinkCellMenuItems(MenuFlyout flyout, MasterDataCellViewModel cellVm, MasterDataRowViewModel rowVm)
    {
        var copyDisplay = new MenuFlyoutItem { Text = "Copy displayed label" };
        copyDisplay.Click += (_, _) => CopyToClipboard(string.IsNullOrEmpty(cellVm.Text) ? null : cellVm.Text);
        flyout.Items.Add(copyDisplay);

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

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

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

        PopulateLinkedDataTabs(
        [
            new LinkedDataTabModel(
                template.Name,
                template,
                null,
                row is null ? [] : [row]),
        ]);
        OpenActionPanel();
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

    private sealed class LinkSourceCellTag
    {
        public LinkSourceCellTag(MasterDataRowViewModel row, string fieldKey)
        {
            Row = row;
            FieldKey = fieldKey;
        }

        public MasterDataRowViewModel Row { get; }
        public string FieldKey { get; }
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

}
