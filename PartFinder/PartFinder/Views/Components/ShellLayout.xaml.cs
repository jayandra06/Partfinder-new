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
    // Drag-only for SetTitleBar; must not cover nav or taps are eaten by the caption handler.
    public UIElement ShellTitleBarDragTarget => ShellTitleBarDragRegion;

    public ShellLayout()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = vm;
        vm.PropertyChanged += OnShellVmPropertyChanged;
        Loaded += OnShellLayoutLoaded;
    }

    /// <summary>Reserve space for system caption buttons (device pixels → XAML padding via scale).</summary>
    public void UpdateShellTitleBarCaptionInsets(double leftInset, double rightInset, double rasterizationScale)
    {
        var scale = rasterizationScale > 0 ? rasterizationScale : 1.0;
        ShellTitleBarRoot.Padding = new Thickness(12 + leftInset / scale, 0, 16 + rightInset / scale, 0);
    }

    public void ResetShellTitleBarCaptionInsets()
    {
        ShellTitleBarRoot.Padding = new Thickness(12, 0, 16, 0);
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
        if (e.PropertyName is nameof(ShellViewModel.OverflowNavigationItems)
            or nameof(ShellViewModel.HasOverflowNavigation))
        {
            RebuildOverflowNavFlyout();
        }
    }

    private void OnMoreNavButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Flyout is not null)
        {
            button.Flyout.ShowAt(button);
        }
    }

    private void RebuildOverflowNavFlyout()
    {
        if (MoreNavButton is null || DataContext is not ShellViewModel vm)
        {
            return;
        }

        var flyout = new MenuFlyout();
        foreach (var item in vm.OverflowNavigationItems)
        {
            var page = item.Page;
            var menuItem = new MenuFlyoutItem
            {
                Text = item.Label,
                IsEnabled = item.IsEnabled,
            };
            menuItem.Click += (_, _) => vm.NavigateToPage(page);
            flyout.Items.Add(menuItem);
        }

        MoreNavButton.Flyout = flyout.Items.Count > 0 ? flyout : null;
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
        if (sender is not Grid grid || DataContext is not ShellViewModel vm)
        {
            return;
        }

        // ItemsRepeater item root: DataContext is NavItemViewModel; Tag is bound Label as fallback.
        NavItemViewModel? picked = grid.DataContext as NavItemViewModel;
        if (picked is null)
        {
            var label = grid.Tag as string ?? string.Empty;
            picked = vm.NavigationItems.FirstOrDefault(n => n.Label == label);
        }

        if (picked is not null)
        {
            vm.SelectedNavigationItem = picked;
        }

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
            RebuildOverflowNavFlyout();
        }
    }
}
