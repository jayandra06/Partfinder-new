using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class WmsWorkbookPage : Page
{
    public WmsWorkbookViewModel ViewModel { get; }

    public WmsWorkbookPage()
    {
        ViewModel = App.Services.GetRequiredService<WmsWorkbookViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.LoadAsync().ConfigureAwait(true);
    }
}