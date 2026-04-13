using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using PartFinder.Models;
using PartFinder.ViewModels;
using System.Collections.Specialized;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace PartFinder.Views.Pages;

public sealed partial class PartsPage : Page
{
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
        await ViewModel.InitializeAsync();
        BuildGridColumns();
        AttachScrollHandler();
    }

    private void OnColumnsChanged(object? sender, NotifyCollectionChangedEventArgs e) => BuildGridColumns();

    private void BuildGridColumns()
    {
        ColumnsHeaderPanel.Children.Clear();
        ColumnFiltersPanel.Children.Clear();

        foreach (var col in ViewModel.DynamicColumns)
        {
            var headerText = new TextBlock
            {
                Text = col.Header,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 160,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var headerBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
                Padding = new Thickness(10, 8, 10, 8),
                Child = headerText,
            };
            ColumnsHeaderPanel.Children.Add(headerBorder);

            var filterBox = new TextBox
            {
                Width = 160,
                PlaceholderText = col.Header,
                Tag = col.Key,
            };
            filterBox.TextChanged += OnColumnFilterTextChanged;
            ColumnFiltersPanel.Children.Add(filterBox);
        }

        var actionsHeader = new Border
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
            Padding = new Thickness(10, 8, 10, 8),
            Child = new TextBlock
            {
                Text = "Actions",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 74,
                HorizontalTextAlignment = TextAlignment.Center,
            },
        };
        ColumnsHeaderPanel.Children.Add(actionsHeader);
    }

    private void OnColumnFilterTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not string fieldKey)
        {
            return;
        }

        ViewModel.SetColumnFilter(fieldKey, box.Text);
    }

    private void BindRowCells(Grid rowGrid, PartRecord row)
    {
        rowGrid.Children.Clear();
        rowGrid.ColumnDefinitions.Clear();

        for (var i = 0; i < ViewModel.DynamicColumns.Count; i++)
        {
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var col = ViewModel.DynamicColumns[i];
            var text = row.Values.TryGetValue(col.Key, out var value) ? value?.ToString() ?? string.Empty : string.Empty;

            var textBlock = new TextBlock
            {
                Text = text,
                Width = 160,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var border = new Border
            {
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
                Padding = new Thickness(10, 6, 10, 6),
                Child = textBlock,
                ContextFlyout = BuildCellMenu(col.Key, text, row),
            };

            Grid.SetColumn(border, i);
            rowGrid.Children.Add(border);
        }

        // Explicit row action affordance so users don't need to discover right-click.
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var actionsBtn = new Button
        {
            Content = "...",
            Width = 56,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 2, 8, 2),
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
            BindRowCells(rowGrid, row);
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
        if (XamlRoot is null)
        {
            return;
        }

        var sourceMap = PartsViewModel.BuildSourceRowMap(row);
        var (targetDef, rows) = await ViewModel.RunContextActionAsync(action, sourceMap).ConfigureAwait(true);
        if (targetDef is null)
        {
            return;
        }

        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(
            new TextBlock
            {
                Text = $"{rows.Count} row(s) in {targetDef.Name}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.WrapWholeWords,
            });

        if (rows.Count == 0)
        {
            stack.Children.Add(
                new TextBlock
                {
                    Text = "No rows matched your rules for this line.",
                    TextWrapping = TextWrapping.WrapWholeWords,
                    Foreground = (Brush)Application.Current.Resources["AppSubtleTextBrush"],
                });
        }
        else
        {
            var fieldOrder = ResolveDisplayFields(targetDef, action.DisplayFieldKeys);
            foreach (var resultRow in rows)
            {
                var card = new Border
                {
                    BorderBrush = (Brush)Application.Current.Resources["AppBorderBrush"],
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 8, 10, 8),
                    Margin = new Thickness(0, 0, 0, 8),
                };
                var inner = new StackPanel { Spacing = 4 };
                foreach (var f in fieldOrder)
                {
                    var val = resultRow.Values.TryGetValue(f.Key, out var v) ? v : string.Empty;
                    inner.Children.Add(
                        new TextBlock
                        {
                            Text = $"{f.Label}: {val}",
                            TextWrapping = TextWrapping.WrapWholeWords,
                        });
                }

                card.Child = inner;
                stack.Children.Add(card);
            }
        }

        var dlg = new ContentDialog
        {
            Title = action.MenuLabel,
            Content = new ScrollViewer { MaxHeight = 440, Content = stack },
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        _ = await dlg.ShowAsync();
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
}
