using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PartFinder.Models;
using PartFinder.Services;
using PartFinder.ViewModels;
using System.Collections.Specialized;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace PartFinder.Views.Pages;

public sealed partial class PartsPage : Page
{
    private const double DataCellTextWidth = 160;
    private const double DataCellHorizontalPadding = 10;
    private const double FilterColumnWidth = DataCellTextWidth + (DataCellHorizontalPadding * 2);
    private const double ActionColumnWidth = 72;
    private const double ActionPanelWidth = 320;
    private readonly List<ActionResultDisplayItem> _actionResultRows = new();
    private bool _isHeaderEditMode;
    private readonly Dictionary<string, string> _pendingHeaderEdits = new(StringComparer.OrdinalIgnoreCase);

    public PartsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<PartsViewModel>();
        Loaded += OnLoaded;
    }

    private PartsViewModel ViewModel => (PartsViewModel)DataContext;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.DynamicColumns.CollectionChanged += OnColumnsChanged;
        ViewModel.FilteredRecords.CollectionChanged += OnFilteredRowsChanged;
        await ViewModel.InitializeAsync();
        BuildGridColumns();
        BuildSelectedRowActionsPanel(null);
        AttachScrollHandler();
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildGridColumns();

    private void OnPartsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        BuildSelectedRowActionsPanel(PartsListView.SelectedItem as PartRecord);
    }

    private void OnFilteredRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (PartsListView.SelectedItem is PartRecord selected && !ViewModel.FilteredRecords.Contains(selected))
        {
            PartsListView.SelectedItem = null;
        }
        BuildSelectedRowActionsPanel(PartsListView.SelectedItem as PartRecord);
    }

    private void BuildGridColumns()
    {
        ColumnFiltersPanel.Children.Clear();

        foreach (var col in ViewModel.DynamicColumns)
        {
            if (_isHeaderEditMode)
            {
                var headerEditor = new TextBox
                {
                    Width = FilterColumnWidth,
                    Text = _pendingHeaderEdits.TryGetValue(col.Key, out var pending) ? pending : col.Header,
                    Tag = col.Key,
                    Style = (Style)Application.Current.Resources["StandardTextBoxStyle"],
                };
                headerEditor.TextChanged += OnHeaderEditTextChanged;
                ColumnFiltersPanel.Children.Add(headerEditor);
            }
            else
            {
                var filterBox = new TextBox
                {
                    Width = FilterColumnWidth,
                    PlaceholderText = col.Header,
                    Tag = col.Key,
                    Style = (Style)Application.Current.Resources["GridFilterTextBoxStyle"],
                };
                filterBox.TextChanged += OnColumnFilterTextChanged;
                ColumnFiltersPanel.Children.Add(filterBox);
            }
        }

        var actionSpacer = new Border
        {
            Width = ActionColumnWidth,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
            Background = (Brush)Application.Current.Resources["GridHeaderBackgroundBrush"],
        };
        if (_isHeaderEditMode)
        {
            var headerEditActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 4,
            };
            var saveHeadersButton = new Button
            {
                Width = 32,
                Height = 32,
                Style = (Style)Application.Current.Resources["GhostButtonStyle"],
                Content = new SymbolIcon(Symbol.Accept),
            };
            ToolTipService.SetToolTip(saveHeadersButton, "Save header names");
            saveHeadersButton.Click += OnSaveHeadersInlineClick;

            var cancelHeadersButton = new Button
            {
                Width = 32,
                Height = 32,
                Style = (Style)Application.Current.Resources["GhostButtonStyle"],
                Content = new SymbolIcon(Symbol.Cancel),
            };
            ToolTipService.SetToolTip(cancelHeadersButton, "Cancel header edit");
            cancelHeadersButton.Click += OnCancelHeadersInlineClick;

            headerEditActions.Children.Add(saveHeadersButton);
            headerEditActions.Children.Add(cancelHeadersButton);
            actionSpacer.Child = headerEditActions;
        }
        else
        {
            var hoverHost = new Grid();
            hoverHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hoverHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            hoverHost.HorizontalAlignment = HorizontalAlignment.Center;
            hoverHost.VerticalAlignment = VerticalAlignment.Center;
            hoverHost.ColumnSpacing = 4;

            var editHeadersButton = new Button
            {
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["GhostButtonStyle"],
                Content = new SymbolIcon(Symbol.Edit),
            };
            ToolTipService.SetToolTip(editHeadersButton, "Rename column headers");
            editHeadersButton.Click += OnStartInlineHeaderEditClick;
            Grid.SetColumn(editHeadersButton, 0);
            hoverHost.Children.Add(editHeadersButton);

            var addHeaderButton = new Button
            {
                Width = 32,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["GhostButtonStyle"],
                Content = "+",
                Visibility = Visibility.Collapsed,
            };
            ToolTipService.SetToolTip(addHeaderButton, "Add new header");
            addHeaderButton.Click += OnAddHeaderClick;
            Grid.SetColumn(addHeaderButton, 1);
            hoverHost.Children.Add(addHeaderButton);

            actionSpacer.PointerEntered += (_, _) => addHeaderButton.Visibility = Visibility.Visible;
            actionSpacer.PointerExited += (_, _) => addHeaderButton.Visibility = Visibility.Collapsed;
            actionSpacer.Child = hoverHost;
        }
        ColumnFiltersPanel.Children.Add(actionSpacer);
    }

    private void OnColumnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string fieldKey)
        {
            return;
        }

        ViewModel.SetColumnFilter(fieldKey, box.Text);
    }

    private void OnHeaderEditTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string fieldKey)
        {
            return;
        }

        _pendingHeaderEdits[fieldKey] = box.Text;
    }

    private void OnStartInlineHeaderEditClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.DynamicColumns.Count == 0)
        {
            return;
        }

        foreach (var col in ViewModel.DynamicColumns)
        {
            _pendingHeaderEdits[col.Key] = col.Header;
        }

        _isHeaderEditMode = true;
        BuildGridColumns();
    }

    private async void OnSaveHeadersInlineClick(object sender, RoutedEventArgs e)
    {
        if (_pendingHeaderEdits.Count == 0)
        {
            _isHeaderEditMode = false;
            BuildGridColumns();
            return;
        }

        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in ViewModel.DynamicColumns)
        {
            var raw = _pendingHeaderEdits.TryGetValue(col.Key, out var v) ? v : col.Header;
            updates[col.Key] = string.IsNullOrWhiteSpace(raw) ? col.Header : raw.Trim();
        }

        await ViewModel.UpdateColumnHeadersAsync(updates).ConfigureAwait(true);
        _pendingHeaderEdits.Clear();
        _isHeaderEditMode = false;
        BuildGridColumns();
    }

    private async void OnAddHeaderClick(object sender, RoutedEventArgs e)
    {
        if (XamlRoot is null)
        {
            return;
        }

        var input = new TextBox
        {
            PlaceholderText = "New header name",
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Add header",
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

        _ = await ViewModel.AddColumnToSelectedTemplateAsync(input.Text).ConfigureAwait(true);
    }

    private void OnCancelHeadersInlineClick(object sender, RoutedEventArgs e)
    {
        _pendingHeaderEdits.Clear();
        _isHeaderEditMode = false;
        BuildGridColumns();
    }

    private void BindRowCells(Grid rowGrid, PartRecord row, int rowIndex)
    {
        rowGrid.Children.Clear();
        rowGrid.ColumnDefinitions.Clear();
        rowGrid.MinHeight = 48;
        rowGrid.Background = (Brush)Application.Current.Resources[
            rowIndex % 2 == 0 ? "CardBackgroundBrush" : "GridRowAltBackgroundBrush"];

        for (var i = 0; i < ViewModel.DynamicColumns.Count; i++)
        {
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var col = ViewModel.DynamicColumns[i];
            var text = row.Values.TryGetValue(col.Key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

            var textBlock = new TextBlock
            {
                Text = text,
                Width = DataCellTextWidth,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            };

            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],
                Padding = new Thickness(DataCellHorizontalPadding, 6, DataCellHorizontalPadding, 6),
                Child = textBlock,
                ContextFlyout = BuildCellMenu(col.Key, text, row),
            };

            Grid.SetColumn(border, i);
            rowGrid.Children.Add(border);
        }

        // Explicit row action affordance so users don't need to discover right-click.
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ActionColumnWidth, GridUnitType.Pixel) });
        var actionsBtn = new Button
        {
            Content = "...",
            Width = 48,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 2, 8, 2),
            Style = (Style)Application.Current.Resources["GhostButtonStyle"],
            ContextFlyout = BuildRowMenu(row),
        };
        actionsBtn.Click += (_, _) =>
        {
            if (actionsBtn.ContextFlyout is FlyoutBase fb)
            {
                fb.ShowAt(actionsBtn);
            }
        };
        Grid.SetColumn(actionsBtn, ViewModel.DynamicColumns.Count);
        rowGrid.Children.Add(actionsBtn);
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemContainer.ContentTemplateRoot is Grid rowGrid
            && args.Item is PartRecord row)
        {
            BindRowCells(rowGrid, row, args.ItemIndex);
        }

        if (args.ItemIndex + 8 >= sender.Items.Count && ViewModel.LoadMoreCommand.CanExecute(null))
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }
    }

    private MenuFlyout BuildCellMenu(string fieldKey, string cellText, PartRecord row)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();

            var copy = new MenuFlyoutItem { Text = "Copy cell value" };
            copy.Click += (_, _) => CopyToClipboard(cellText);
            flyout.Items.Add(copy);

            var actions = ViewModel.GetContextActionsForField(fieldKey);
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
                    await ShowContextActionResultsAsync(captured, row).ConfigureAwait(true);
                flyout.Items.Add(item);
            }
        };
        return flyout;
    }

    private MenuFlyout BuildRowMenu(PartRecord row)
    {
        var flyout = new MenuFlyout();
        flyout.Opening += (_, _) =>
        {
            flyout.Items.Clear();

            var rowMap = PartsViewModel.BuildSourceRowMap(row);
            foreach (var kv in rowMap.Where(kv => !string.IsNullOrWhiteSpace(kv.Value)))
            {
                var copy = new MenuFlyoutItem { Text = $"Copy {GetHeaderForKey(kv.Key)}" };
                var text = kv.Value;
                copy.Click += (_, _) => CopyToClipboard(text);
                flyout.Items.Add(copy);
            }

            var actionItems = new Dictionary<string, TemplateContextAction>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in ViewModel.DynamicColumns)
            {
                foreach (var action in ViewModel.GetContextActionsForField(col.Key))
                {
                    if (!actionItems.ContainsKey(action.MenuLabel))
                    {
                        actionItems[action.MenuLabel] = action;
                    }
                }
            }

            if (actionItems.Count == 0)
            {
                return;
            }

            flyout.Items.Add(new MenuFlyoutSeparator());
            foreach (var action in actionItems.Values.OrderBy(a => a.MenuLabel, StringComparer.OrdinalIgnoreCase))
            {
                var item = new MenuFlyoutItem { Text = action.MenuLabel };
                var captured = action;
                item.Click += async (_, _) =>
                    await ShowContextActionResultsAsync(captured, row).ConfigureAwait(true);
                flyout.Items.Add(item);
            }
        };
        return flyout;
    }

    private string GetHeaderForKey(string key)
    {
        return ViewModel.DynamicColumns.FirstOrDefault(c => c.Key == key)?.Header ?? key;
    }

    private async Task ShowContextActionResultsAsync(TemplateContextAction action, PartRecord row)
    {
        if (XamlRoot is null || ActionResultsPanel is null || ActionRowsListView is null)
        {
            return;
        }

        var sourceMap = PartsViewModel.BuildSourceRowMap(row);
        var (targetDef, rows) = await ViewModel.RunContextActionAsync(action, sourceMap).ConfigureAwait(true);
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
                    new[] { "No rows matched your rules for this line." }));
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

    private void OnActionOverlayTapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
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

    private static void CopyToClipboard(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void BuildSelectedRowActionsPanel(PartRecord? row)
    {
        RowActionsPanel.Children.Clear();
        if (row is null)
        {
            RowActionsHintText.Text = "Select a row to see available actions.";
            ClearSelectedRowButton.Visibility = Visibility.Collapsed;
            return;
        }

        ClearSelectedRowButton.Visibility = Visibility.Visible;

        var actions = new Dictionary<string, TemplateContextAction>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in ViewModel.DynamicColumns)
        {
            foreach (var action in ViewModel.GetContextActionsForField(col.Key))
            {
                if (!actions.ContainsKey(action.MenuLabel))
                {
                    actions[action.MenuLabel] = action;
                }
            }
        }

        if (actions.Count == 0)
        {
            RowActionsHintText.Text = "No configured actions for this row/template.";
            return;
        }

        RowActionsHintText.Text = "Use these quick actions for the selected row.";
        foreach (var action in actions.Values.OrderBy(a => a.MenuLabel, StringComparer.OrdinalIgnoreCase))
        {
            var btn = new Button
            {
                Content = action.MenuLabel,
                Padding = new Thickness(10, 4, 10, 4),
            };
            var captured = action;
            btn.Click += async (_, _) => await ShowContextActionResultsAsync(captured, row).ConfigureAwait(true);
            RowActionsPanel.Children.Add(btn);
        }
    }

    private void OnClearSelectedRowClick(object sender, RoutedEventArgs e)
    {
        PartsListView.SelectedItem = null;
        BuildSelectedRowActionsPanel(null);
    }

    private void AttachScrollHandler()
    {
        var scrollViewer = FindDescendant<ScrollViewer>(PartsListView);
        if (scrollViewer is null)
        {
            return;
        }

        scrollViewer.ViewChanged -= OnPartsScrollChanged;
        scrollViewer.ViewChanged += OnPartsScrollChanged;
    }

    private void OnPartsScrollChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv || !ViewModel.HasMoreRows || ViewModel.IsLoading)
        {
            return;
        }

        var nearEnd = sv.ScrollableHeight - sv.VerticalOffset < 280;
        if (nearEnd && ViewModel.LoadMoreCommand.CanExecute(null))
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T typed)
            {
                return typed;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
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
