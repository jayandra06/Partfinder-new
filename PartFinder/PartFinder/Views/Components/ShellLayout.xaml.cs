using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PartFinder.Services;
using PartFinder.ViewModels;
using System;
using System.ComponentModel;
using System.Linq;

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
    }

    private void OnAccountSettingsMenuClicked(object sender, RoutedEventArgs e)
    {
        var navigation = App.Services.GetRequiredService<INavigationService>();
        _ = navigation.Navigate(AppPage.Settings);
    }

    private async void OnProfileLogoutMenuClicked(object sender, RoutedEventArgs e)
    {
        var adminSession = App.Services.GetRequiredService<AdminSessionStore>();
        var activityLogger = App.Services.GetRequiredService<ActivityLogger>();
        
        activityLogger.LogLogout(adminSession.Email ?? "unknown");
        await activityLogger.FlushAsync().ConfigureAwait(true);
        
        adminSession.Clear();

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
        // No sidebar width changes needed anymore
    }

    // ── Tooltip — rendered at root level, shows BELOW the nav icon ──────
    private void OnNavItemPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;

        string label = grid.Tag as string ?? string.Empty;
        if (string.IsNullOrEmpty(label)) return;

        NavTooltipLabel.Text = label;

        var transform = grid.TransformToVisual(RootGrid);
        var pos = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

        // Position tooltip below the nav icon
        double tooltipX = pos.X + (grid.ActualWidth / 2) - 30;
        double tooltipY = pos.Y + grid.ActualHeight + 6;

        Canvas.SetLeft(NavTooltipBox, tooltipX);
        Canvas.SetTop(NavTooltipBox, tooltipY);

        NavTooltipOverlay.Visibility = Visibility.Visible;
    }

    private void OnNavItemPointerExited(object sender, PointerRoutedEventArgs e)
    {
        NavTooltipOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnNavItemTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (DataContext is not ShellViewModel vm) return;

        string label = grid.Tag as string ?? string.Empty;
        var item = vm.NavigationItems.FirstOrDefault(n => n.Label == label);
        if (item is not null)
            vm.SelectedNavigationItem = item;

        NavTooltipOverlay.Visibility = Visibility.Collapsed;
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
