using Microsoft.UI.Xaml.Controls;
using PartFinder.Views.Pages;

namespace PartFinder.Services;

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<AppPage, Type> _routes = new()
    {
        [AppPage.MasterData] = typeof(MasterDataPage),
        [AppPage.Dashboard] = typeof(DashboardPage),
        [AppPage.Parts] = typeof(MasterDataPage),
        [AppPage.Inventory] = typeof(InventoryPage),
        [AppPage.Analytics] = typeof(AnalyticsPage),
        [AppPage.Alerts] = typeof(AlertsPage),
        [AppPage.Suppliers] = typeof(SuppliersPage),
        [AppPage.Catalog] = typeof(CatalogPage),
        [AppPage.Orders] = typeof(OrdersPage),
        [AppPage.Audit] = typeof(AuditPage),
        [AppPage.Templates] = typeof(TemplatesPage),
        [AppPage.WorksheetRelations] = typeof(WorksheetRelationsPage),
        [AppPage.ViewData] = typeof(ViewDataPage),
        [AppPage.UserManagement] = typeof(UserManagementPage),
        [AppPage.Settings] = typeof(SettingsPage),
        [AppPage.QrCodeManager] = typeof(QrCodeManagerPage),
        [AppPage.Branches] = typeof(BranchesPage)
    };

    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void Initialize(Frame frame) => _frame = frame;

    public bool Navigate(AppPage page, object? parameter = null)
    {
        if (_frame is null || !_routes.TryGetValue(page, out var targetType))
        {
            return false;
        }

        if (_frame.Content?.GetType() == targetType)
        {
            return false;
        }

        return _frame.Navigate(targetType, parameter);
    }

    public void GoBack()
    {
        if (CanGoBack)
        {
            _frame!.GoBack();
        }
    }
}
