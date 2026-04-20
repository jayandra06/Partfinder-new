using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
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
    private readonly List<ActionResultDisplayItem> _actionResultRows = new();

    public MasterDataPage()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<MasterDataViewModel>();
        _excelTemplateService = App.Services.GetRequiredService<IExcelTemplateService>();
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
        await vm.LoadAsync().ConfigureAwait(true);
        RebuildSpreadsheet();
    }

    private void OnMasterDataPageUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnMasterDataPageUnloaded;
        if (_viewModel is not null)
        {
            _viewModel.Rows.CollectionChanged -= OnGridStructureChanged;
            _viewModel.Columns.CollectionChanged -= OnGridStructureChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MasterDataViewModel.IsEditMode))
        {
            RebuildSpreadsheet();
        }
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
        var colCount = _viewModel.Columns.Count;
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
                    Background = (Brush)Application.Current.Resources[
                        r % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"],
                    BorderBrush = borderBrush,
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = cellContent,
                };
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
        box.ContextFlyout = BuildTextCellMenu(cellVm, rowVm);
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
        outer.ContextFlyout = BuildLinkCellMenu(cellVm, rowVm);
        outer.Child = rowPanel;
        return outer;
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
