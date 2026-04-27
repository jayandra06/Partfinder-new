using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Pages;

public sealed partial class BranchesPage : Page
{
    public BranchesPage()
    {
        InitializeComponent();
    }

    private void OnAddBranchClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - POST /api/branches
    }

    private void OnSyncAllClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - POST /api/branches/sync-all
    }

    private void OnViewCategoryClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - GET /api/branches/{id}/categories/{categoryId}
    }

    private void OnReorderClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - POST /api/reorder-requests
    }

    private void OnNewTransferClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - POST /api/transfers
    }

    private void OnExportReportClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - GET /api/branches/{id}/report
    }

    private void OnSyncInventoryClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - POST /api/branches/{id}/sync
    }

    private void OnViewAuditClick(object sender, RoutedEventArgs e)
    {
        // TODO: Navigate to Audit page filtered by branch
    }

    private void OnManageUsersClick(object sender, RoutedEventArgs e)
    {
        // TODO: Navigate to User Management page filtered by branch
    }

    private void OnBranchSelectClick(object sender, RoutedEventArgs e)
    {
        // TODO: Connect to backend - GET /api/branches/{id}/overview
    }
}
