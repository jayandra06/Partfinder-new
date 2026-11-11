using System.Collections.Specialized;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Models;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class LinkedTemplatesPage : Page
{
    private LinkedTemplatesViewModel? _vm;
    private bool _browseFrameReady;

    public LinkedTemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<LinkedTemplatesViewModel>();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PageEntranceStoryboard.Begin();

        if (DataContext is not LinkedTemplatesViewModel vm)
        {
            return;
        }

        _vm = vm;
        vm.PropertyChanged += OnBrowseVmPropertyChanged;
        vm.Templates.CollectionChanged += OnTemplatesCollectionChanged;
        await vm.InitializeAsync().ConfigureAwait(true);
        UpdateCatalogNavChrome();
        EnsureBrowseFrame();
        SyncBrowseTemplate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnBrowseVmPropertyChanged;
            _vm.Templates.CollectionChanged -= OnTemplatesCollectionChanged;
            _vm = null;
        }
    }

    private void OnTemplatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateCatalogNavChrome();

    private void UpdateCatalogNavChrome()
    {
        var show = _vm is { Templates.Count: > 0 };
        CatalogNavColumn.Width = show ? new GridLength(220) : new GridLength(0);
        CatalogSidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowseVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinkedTemplatesViewModel.IsLoading) && _vm is { IsLoading: false })
        {
            UpdateCatalogNavChrome();
        }

        if (e.PropertyName is nameof(LinkedTemplatesViewModel.BrowseSourceTemplate)
            or nameof(LinkedTemplatesViewModel.BrowseSourceTemplateName))
        {
            SyncBrowseTemplate();
        }
    }

    private void EnsureBrowseFrame()
    {
        if (_browseFrameReady || BrowseDataFrame is null)
        {
            return;
        }

        _browseFrameReady = true;
        BrowseDataFrame.Navigated += OnBrowseFrameNavigated;
        BrowseDataFrame.Navigate(
            typeof(MasterDataPage),
            new MasterDataNavigationArgs { Mode = MasterDataSurfaceMode.LinkedBrowse });
    }

    private void OnBrowseFrameNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        if (e.Content is MasterDataPage page)
        {
            page.ConfigureEmbeddedBrowseChrome();
            SyncBrowseTemplate();
        }
    }

    private void SyncBrowseTemplate()
    {
        if (_vm?.BrowseSourceTemplate is null)
        {
            return;
        }

        if (BrowseDataFrame?.Content is MasterDataPage page)
        {
            page.ApplyBrowseTemplate(_vm.BrowseSourceTemplate.Id);
        }
    }
}
