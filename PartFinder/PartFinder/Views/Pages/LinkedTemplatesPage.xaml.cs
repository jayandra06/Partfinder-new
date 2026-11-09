using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class LinkedTemplatesPage : Page
{
    public LinkedTemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<LinkedTemplatesViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        PageEntranceStoryboard.Begin();

        if (DataContext is LinkedTemplatesViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
