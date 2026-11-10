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
        await vm.InitializeAsync().ConfigureAwait(true);
        EnsureBrowseFrame();
        SyncBrowseTemplate();
    }

    private void OnBrowseVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
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
