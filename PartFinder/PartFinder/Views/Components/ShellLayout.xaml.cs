using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Services;
using PartFinder.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.Graphics;

namespace PartFinder.Views.Components;

public sealed partial class ShellLayout : UserControl
{
    public ShellLayout()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<ShellViewModel>();
        DataContext = vm;
        Loaded += OnShellLayoutLoaded;
        AppTitleBarLayout.LayoutUpdated += OnAppTitleBarLayoutUpdated;
    }

    public UIElement TitleBarDragElement => TitleBarDragStrip;

    public void UpdateTitleBarInsets(double leftInset, double rightInset)
    {
        TitleBarLeftInsetColumn.Width = new GridLength(Math.Max(0, leftInset));
        TitleBarRightInsetColumn.Width = new GridLength(Math.Max(0, rightInset));
    }

    public RectInt32[] GetTitleBarPassthroughRects()
    {
        if (XamlRoot is null)
        {
            return Array.Empty<RectInt32>();
        }

        var scaleAdjustment = XamlRoot.RasterizationScale;
        var rects = new List<RectInt32>(3);
        TryAddPassthroughRect(rects, TopNavInteractiveRegion, scaleAdjustment);
        TryAddPassthroughRect(rects, TopNavSearchRegion, scaleAdjustment);
        TryAddPassthroughRect(rects, TopNavActionsRegion, scaleAdjustment);
        return rects.ToArray();
    }

    private static void TryAddPassthroughRect(List<RectInt32> rects, FrameworkElement element, double scaleAdjustment)
    {
        if (element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return;
        }

        var transform = element.TransformToVisual(null);
        var bounds = transform.TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        rects.Add(
            new RectInt32(
                (int)Math.Round(bounds.X * scaleAdjustment),
                (int)Math.Round(bounds.Y * scaleAdjustment),
                (int)Math.Round(bounds.Width * scaleAdjustment),
                (int)Math.Round(bounds.Height * scaleAdjustment)));
    }

    private void OnAccountSettingsMenuClicked(object sender, RoutedEventArgs e)
    {
        SelectNavigationPage(AppPage.Settings);
    }

    private void OnBellClicked(object sender, RoutedEventArgs e)
    {
        SelectNavigationPage(AppPage.Alerts);

        // Clear badge after opening alerts
        if (DataContext is ShellViewModel vm)
        {
            vm.HasUnreadAlerts = false;
        }
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

    private void OnTopNavItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        // Try to get item from DataContext
        if (element.DataContext is not NavItemViewModel item)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellLayout] Nav item click failed: DataContext is {element.DataContext?.GetType().Name ?? "null"} (expected NavItemViewModel)");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ShellLayout] Nav item clicked: {item.Label} (Page: {item.Page})");

        if (DataContext is ShellViewModel vm)
        {
            vm.SelectedNavigationItem = item;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ShellLayout] Nav item click failed: ShellLayout DataContext is not ShellViewModel.");
        }
    }

    private void SelectNavigationPage(AppPage page)
    {
        if (DataContext is not ShellViewModel vm)
        {
            return;
        }

        var item = vm.NavigationItems.FirstOrDefault(n => n.Page == page);
        if (item is not null)
        {
            vm.SelectedNavigationItem = item;
            return;
        }

        var navigation = App.Services.GetRequiredService<INavigationService>();
        _ = navigation.Navigate(page);
    }

    private async void OnShellLayoutLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnShellLayoutLoaded;
        if (Resources["AmbientDriftStoryboard"] is Storyboard ambientStoryboard)
        {
            ambientStoryboard.Begin();
        }

        var navigationService = App.Services.GetRequiredService<INavigationService>();
        navigationService.Initialize(ContentFrame);

        if (DataContext is ShellViewModel shellViewModel)
        {
            await shellViewModel.InitializeAsync().ConfigureAwait(true);
        }

        NotifyTitleBarRegionsChanged();
    }

    private void OnAppTitleBarLayoutUpdated(object? sender, object e)
    {
        NotifyTitleBarRegionsChanged();
    }

    private void NotifyTitleBarRegionsChanged()
    {
        if (App.MainAppWindow is MainWindow main)
        {
            main.RefreshShellTitleBarRegions();
        }
    }
}
