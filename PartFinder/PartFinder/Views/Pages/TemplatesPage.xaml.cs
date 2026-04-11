using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class TemplatesPage : Page
{
    public TemplatesPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<TemplatesViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await ((TemplatesViewModel)DataContext).LoadAsync();
    }
}
