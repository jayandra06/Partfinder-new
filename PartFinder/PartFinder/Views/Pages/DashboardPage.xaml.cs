using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.ViewModels;

namespace PartFinder.Views.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<DashboardViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LazyLoadTrendCommand.CanExecute(null))
        {
            await _viewModel.LazyLoadTrendCommand.ExecuteAsync(null);
        }
    }
}
