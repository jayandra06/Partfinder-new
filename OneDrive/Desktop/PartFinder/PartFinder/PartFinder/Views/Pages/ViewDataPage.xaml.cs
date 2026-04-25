using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace PartFinder.Views.Pages;

public sealed partial class ViewDataPage : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _closeFlyoutTimer;
    private bool _pointerInsideFlyout;

    public ViewDataPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ViewDataViewModel>();
        Loaded += OnLoaded;
        KeyDown += OnPageKeyDown;
        DataContextChanged += OnDataContextChanged;
        _closeFlyoutTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _closeFlyoutTimer.Interval = TimeSpan.FromMilliseconds(220);
        _closeFlyoutTimer.Tick += (_, _) =>
        {
            _closeFlyoutTimer.Stop();
            if (!_pointerInsideFlyout && DataContext is ViewDataViewModel vm)
            {
                vm.CloseFlyoutCommand.Execute(null);
            }
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ViewDataViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && DataContext is ViewDataViewModel vm)
        {
            vm.CloseFlyoutCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is ViewDataViewModel vm)
        {
            vm.PropertyChanged -= OnVmPropertyChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewDataViewModel.IsFlyoutOpen)
            && sender is ViewDataViewModel vm
            && vm.IsFlyoutOpen)
        {
            var anim = new DoubleAnimation
            {
                From = 24,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)),
                EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut },
            };
            Storyboard.SetTarget(anim, DetailsPanelTransform);
            Storyboard.SetTargetProperty(anim, "X");
            var sb = new Storyboard();
            sb.Children.Add(anim);
            sb.Begin();
        }
    }

    private void OnRowPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _closeFlyoutTimer.Stop();
        if (sender is FrameworkElement { Tag: ViewDataRowItem row }
            && DataContext is ViewDataViewModel vm)
        {
            vm.HoveredRow = row;
            RowsListView.SelectedItem = row;
        }
    }

    private void OnRowPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerInsideFlyout)
        {
            _closeFlyoutTimer.Stop();
            _closeFlyoutTimer.Start();
        }
    }

    private void OnDetailsPanelPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _pointerInsideFlyout = true;
        _closeFlyoutTimer.Stop();
    }

    private void OnDetailsPanelPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _pointerInsideFlyout = false;
        _closeFlyoutTimer.Stop();
        _closeFlyoutTimer.Start();
    }
}
