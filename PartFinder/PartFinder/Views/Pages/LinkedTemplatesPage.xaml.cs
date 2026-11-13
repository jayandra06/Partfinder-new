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
    private bool _syncingSidebarSelection;

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
        vm.LinkedSidebarTemplates.CollectionChanged += OnSidebarTemplatesCollectionChanged;
        vm.NonLinkedSidebarTemplates.CollectionChanged += OnSidebarTemplatesCollectionChanged;
        await vm.InitializeAsync().ConfigureAwait(true);
        UpdateCatalogNavChrome();
        EnsureBrowseFrame();
        SyncSidebarListSelection();
        SyncBrowseTemplate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= OnUnloaded;
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnBrowseVmPropertyChanged;
            _vm.LinkedSidebarTemplates.CollectionChanged -= OnSidebarTemplatesCollectionChanged;
            _vm.NonLinkedSidebarTemplates.CollectionChanged -= OnSidebarTemplatesCollectionChanged;
            _vm = null;
        }
    }

    private void OnSidebarTemplatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        UpdateCatalogNavChrome();

    private void UpdateCatalogNavChrome()
    {
        var show = _vm is { HasTemplatesToBrowse: true };
        CatalogNavColumn.Width = show ? new GridLength(220) : new GridLength(0);
        CatalogSidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBrowseVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LinkedTemplatesViewModel.IsLoading) && _vm is { IsLoading: false })
        {
            UpdateCatalogNavChrome();
        }

        if (e.PropertyName is nameof(LinkedTemplatesViewModel.HasTemplatesToBrowse)
            or nameof(LinkedTemplatesViewModel.ShowNoTemplatesAvailableMessage))
        {
            UpdateCatalogNavChrome();
        }

        if (e.PropertyName is nameof(LinkedTemplatesViewModel.BrowseSourceTemplate)
            or nameof(LinkedTemplatesViewModel.BrowseSourceTemplateName))
        {
            SyncSidebarListSelection();
            SyncBrowseTemplate();
        }
    }

    private void SyncSidebarListSelection()
    {
        if (_vm is null)
        {
            return;
        }

        _syncingSidebarSelection = true;
        try
        {
            var pick = _vm.BrowseSourceTemplate;
            LinkedTemplatesListView.SelectedItem = pick is null
                ? null
                : _vm.LinkedSidebarTemplates.FirstOrDefault(t => t.Id == pick.Id);
            NonLinkedTemplatesListView.SelectedItem = pick is null
                ? null
                : _vm.NonLinkedSidebarTemplates.FirstOrDefault(t => t.Id == pick.Id);
        }
        finally
        {
            _syncingSidebarSelection = false;
        }
    }

    private void OnSidebarTemplateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingSidebarSelection || _vm is null || sender is not ListView list)
        {
            return;
        }

        if (list.SelectedItem is PartTemplateDefinition template)
        {
            if (_vm.BrowseSourceTemplate?.Id != template.Id)
            {
                _vm.BrowseSourceTemplate = template;
            }

            if (!ReferenceEquals(sender, LinkedTemplatesListView))
            {
                LinkedTemplatesListView.SelectedItem = null;
            }

            if (!ReferenceEquals(sender, NonLinkedTemplatesListView))
            {
                NonLinkedTemplatesListView.SelectedItem = null;
            }

            return;
        }

        if (e.RemovedItems.Count > 0 && e.AddedItems.Count == 0 && _vm.BrowseSourceTemplate is not null)
        {
            var clearedFromLinked = ReferenceEquals(sender, LinkedTemplatesListView)
                && _vm.LinkedSidebarTemplates.Any(t => t.Id == _vm.BrowseSourceTemplate.Id);
            var clearedFromNonLinked = ReferenceEquals(sender, NonLinkedTemplatesListView)
                && _vm.NonLinkedSidebarTemplates.Any(t => t.Id == _vm.BrowseSourceTemplate.Id);
            if (clearedFromLinked || clearedFromNonLinked)
            {
                _vm.BrowseSourceTemplate = null;
            }
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

    private async void SyncBrowseTemplate()
    {
        if (BrowseDataFrame?.Content is not MasterDataPage page)
        {
            return;
        }

        if (_vm?.BrowseSourceTemplate is null)
        {
            await page.ClearLinkedBrowseAsync().ConfigureAwait(true);
            return;
        }

        await page.ApplyBrowseTemplateAsync(_vm.BrowseSourceTemplate.Id).ConfigureAwait(true);
    }
}
