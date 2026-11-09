using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Views.Pages;

namespace PartFinder.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<AppPage, Type> _routes = new()
    {
        [AppPage.MasterData]         = typeof(MasterDataPage),
        [AppPage.Dashboard]          = typeof(DashboardPage),
        [AppPage.Parts]              = typeof(PartsPage),
        [AppPage.Inventory]          = typeof(InventoryPage),
        [AppPage.Alerts]             = typeof(AlertsPage),
        [AppPage.Audit]              = typeof(AuditPage),
        [AppPage.Templates]          = typeof(TemplatesPage),
        [AppPage.TemplateData]       = typeof(TemplateDataPage),
        [AppPage.WorksheetRelations] = typeof(WorksheetRelationsPage),
        [AppPage.LinkedTemplates]    = typeof(LinkedTemplatesPage),
        [AppPage.ViewData]           = typeof(ViewDataPage),
        [AppPage.UserManagement]     = typeof(UserManagementPage),
        [AppPage.Settings]           = typeof(SettingsPage),
        [AppPage.QrCodeManager]      = typeof(QrCodeManagerPage),
    };

    private Frame? _frame;
    private int _resetGeneration;
    private readonly TaskCompletionSource _frameReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsFrameReady => _frame is not null;

    public async Task WaitForFrameReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_frame is not null)
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await _frameReady.Task.WaitAsync(timeout.Token).ConfigureAwait(true);
    }

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void Initialize(Frame frame)
    {
        _frame = frame;
        _frameReady.TrySetResult();
        _frame.CacheSize = 1;
        if (App.Current.Resources.TryGetValue("LuxuryPageTransitions", out var transitions) &&
            transitions is TransitionCollection transitionCollection)
        {
            _frame.ContentTransitions = transitionCollection;
        }
    }

    public bool Navigate(AppPage page, object? parameter = null)
    {
        _resetGeneration++;
        return NavigateCore(page, parameter, new DrillInNavigationTransitionInfo());
    }

    public bool ResetTo(AppPage page)
    {
        var generation = ++_resetGeneration;
        var navigated = ShowFresh(page);
        if (_frame is null)
        {
            return navigated;
        }

        // The shell frame keeps its last page while it is hidden. A layout pass after
        // login can put that page back, so confirm the home page once layout has run.
        _frame.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (generation != _resetGeneration || _frame is null)
            {
                return;
            }

            if (!_routes.TryGetValue(page, out var targetType))
            {
                return;
            }

            if (_frame.Content?.GetType() != targetType)
            {
                ShowFresh(page);
            }
            else
            {
                _frame.BackStack.Clear();
                _frame.ForwardStack.Clear();
            }
        });
        return navigated;
    }

    public void Clear()
    {
        _resetGeneration++;
        if (_frame is null)
        {
            return;
        }

        _frame.BackStack.Clear();
        _frame.ForwardStack.Clear();
        if (_frame.Content is null)
        {
            return;
        }

        try
        {
            _frame.Content = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Could not clear frame content: {ex.Message}");
        }
    }

    private bool ShowFresh(AppPage page)
    {
        if (_frame is null || !_routes.TryGetValue(page, out var targetType))
        {
            return false;
        }

        var alreadyThere = _frame.Content?.GetType() == targetType;
        if (!alreadyThere)
        {
            NavigateCore(page, null, new SuppressNavigationTransitionInfo());
        }

        if (_frame.Content?.GetType() != targetType)
        {
            return false;
        }

        _frame.BackStack.Clear();
        _frame.ForwardStack.Clear();
        return true;
    }

    private bool NavigateCore(AppPage page, object? parameter, NavigationTransitionInfo transition)
    {
        if (_frame is null)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Cannot navigate to {page}: Frame is null.");
            return false;
        }

        if (!_routes.TryGetValue(page, out var targetType))
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Cannot navigate to {page}: Route not registered.");
            return false;
        }

        if (_frame.Content?.GetType() == targetType)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Already on {targetType.Name}. Navigation skipped.");
            return false;
        }

        System.Diagnostics.Debug.WriteLine($"[NavigationService] Navigating to {page} ({targetType.Name})...");
        bool result = _frame.Navigate(targetType, parameter, transition);
        System.Diagnostics.Debug.WriteLine($"[NavigationService] Frame.Navigate result: {result}");
        return result;
    }

    public void GoBack()
    {
        _resetGeneration++;
        if (CanGoBack)
            _frame!.GoBack();
    }
}
