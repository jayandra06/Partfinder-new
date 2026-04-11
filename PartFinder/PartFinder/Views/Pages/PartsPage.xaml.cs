using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using PartFinder.Models;
using PartFinder.ViewModels;
using System.Collections.Specialized;
using System.Linq;

namespace PartFinder.Views.Pages;

public sealed partial class PartsPage : Page
{
    private readonly DataTemplate _cellTemplate = (DataTemplate)XamlReader.Load(
        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border BorderThickness='0,0,1,1' BorderBrush='{ThemeResource AppBorderBrush}' Padding='10,6'><TextBlock Text='{Binding}' Width='160' TextTrimming='CharacterEllipsis'/></Border></DataTemplate>");

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
        ColumnsHeader.ItemsSource = ViewModel.DynamicColumns.Select(c => c.Header).ToList();
        ColumnsHeader.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
            "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal'/></ItemsPanelTemplate>");
        ColumnsHeader.ItemTemplate = _cellTemplate;
    }

    private void OnRowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not ItemsControl itemsControl || itemsControl.DataContext is not PartRecord row)
        {
            return;
        }

        var values = ViewModel.DynamicColumns
            .Select(c => row.Values.TryGetValue(c.Key, out var value) ? value?.ToString() ?? string.Empty : string.Empty)
            .ToList();

        itemsControl.ItemsSource = values;
        itemsControl.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load(
            "<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Orientation='Horizontal'/></ItemsPanelTemplate>");
        itemsControl.ItemTemplate = _cellTemplate;
    }

    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.ItemIndex + 8 >= sender.Items.Count && ViewModel.LoadMoreCommand.CanExecute(null))
        {
            ViewModel.LoadMoreCommand.Execute(null);
        }
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
