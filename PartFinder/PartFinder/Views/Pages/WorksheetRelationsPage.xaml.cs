using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class WorksheetRelationsPage : Page
{
    public WorksheetRelationsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<WorksheetRelationsViewModel>();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is WorksheetRelationsViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
