using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Views.Components;

public sealed partial class ShellLayout : UserControl
{
    public ShellLayout()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ShellViewModel>();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.Initialize(ContentFrame);

        if (DataContext is ShellViewModel shellViewModel && shellViewModel.SelectedNavigationItem is not null)
        {
            navigationService.Navigate(shellViewModel.SelectedNavigationItem.Page);
        }
    }
}
