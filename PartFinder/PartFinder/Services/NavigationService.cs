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
        [AppPage.Parts]              = typeof(MasterDataPage),
        [AppPage.Inventory]          = typeof(InventoryPage),
        [AppPage.Alerts]             = typeof(AlertsPage),
        [AppPage.Audit]              = typeof(AuditPage),
        [AppPage.Templates]          = typeof(TemplatesPage),
        [AppPage.WorksheetRelations] = typeof(WorksheetRelationsPage),
        [AppPage.ViewData]           = typeof(ViewDataPage),
        [AppPage.UserManagement]     = typeof(UserManagementPage),
        [AppPage.Settings]           = typeof(SettingsPage),
        [AppPage.QrCodeManager]      = typeof(QrCodeManagerPage),
    };

    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void Initialize(Frame frame)
    {
        _frame = frame;
        _frame.CacheSize = 1;
        if (App.Current.Resources.TryGetValue("LuxuryPageTransitions", out var transitions) &&
            transitions is TransitionCollection transitionCollection)
        {
            _frame.ContentTransitions = transitionCollection;
        }
    }

    public bool Navigate(AppPage page, object? parameter = null)
    {
        if (_frame is null || !_routes.TryGetValue(page, out var targetType))
            return false;

        if (_frame.Content?.GetType() == targetType)
            return false;

        return _frame.Navigate(targetType, parameter, new DrillInNavigationTransitionInfo());
    }

    public void GoBack()
    {
        if (CanGoBack)
            _frame!.GoBack();
    }
}
