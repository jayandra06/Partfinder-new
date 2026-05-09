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

    private void OnBellClicked(object sender, RoutedEventArgs e)
    {
        var navigation = App.Services.GetRequiredService<INavigationService>();
        navigation.Navigate(AppPage.Alerts);
        // Clear badge after opening alerts
        if (DataContext is ShellViewModel vm)
            vm.HasUnreadAlerts = false;
    }

    private async void OnProfileLogoutMenuClicked(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionStore>();
        var activityLogger = App.Services.GetRequiredService<ActivityLogger>();
        
        activityLogger.LogLogout(adminSession.Email ?? "unknown");
        await activityLogger.FlushAsync().ConfigureAwait(true);
        
        adminSession.Clear();
        // Do NOT delete setup-state.json — it holds orgCode/dbUri needed after re-login

        if (App.MainAppWindow is MainWindow main)
        {
            main.ResetToSetup();
        }
        else
        {
            Application.Current.Exit();
        }
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
        // Increased to 80px for a more substantial look while remaining icon-only
        SidebarColumn.Width = new GridLength(80);
        SidebarRoot.Padding = new Thickness(0, 16, 0, 16);
        NavListView.Margin = new Thickness(0, 12, 0, 0);
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

    private void NavItem_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && ToolTipService.GetToolTip(fe) is ToolTip tt)
        {
            tt.IsOpen = true;
        }
    }

    private void NavItem_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && ToolTipService.GetToolTip(fe) is ToolTip tt)
        {
            tt.IsOpen = false;
        }
    }
}
