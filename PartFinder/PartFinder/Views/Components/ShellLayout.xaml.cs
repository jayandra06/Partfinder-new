using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PartFinder.Services;
using PartFinder.ViewModels;
using System.ComponentModel;

namespace PartFinder.Views.Components;

public sealed partial class ShellLayout : UserControl
{
    public ShellLayout()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = vm;
        vm.PropertyChanged += OnShellVmPropertyChanged;
        Loaded += OnShellLayoutLoaded;
        ApplySidebarWidth();
    }

    private void OnAccountSettingsMenuClicked(object sender, RoutedEventArgs e)
    {
        var navigation = App.Services.GetRequiredService<INavigationService>();
        _ = navigation.Navigate(AppPage.Settings);
    }

    private void OnProfileLogoutMenuClicked(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionStore>();
        adminSession.Clear();
        SetupPaths.ClearAllSetupStateFiles();
        Application.Current.Exit();
    }

    private void OnShellVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsSidebarCollapsed))
        {
            ApplySidebarWidth();
        }
    }

    private void ApplySidebarWidth()
    {
        if (DataContext is ShellViewModel vm)
        {
            SidebarColumn.Width = new GridLength(vm.IsSidebarCollapsed ? 52 : 220);
            SidebarRoot.Padding = vm.IsSidebarCollapsed
                ? new Thickness(8, 12, 8, 16)
                : new Thickness(12, 12, 12, 16);
            NavListView.Margin = vm.IsSidebarCollapsed
                ? new Thickness(0, 4, 0, 0)
                : new Thickness(0, 2, 0, 0);
        }
    }

    private async void OnShellLayoutLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnShellLayoutLoaded;
        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.Initialize(ContentFrame);

        if (DataContext is ShellViewModel shellViewModel)
        {
            await shellViewModel.InitializeAsync().ConfigureAwait(true);
        }
    }
}
