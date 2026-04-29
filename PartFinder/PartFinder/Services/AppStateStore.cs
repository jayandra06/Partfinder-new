using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public sealed class AppStateStore : IAppStateStore
{
    public string CurrentTenant { get; set; } = "Default Site";
    public string CurrentUserName { get; set; } = "Operator";
    public ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;
    public string OrgDisplayName { get; set; } = string.Empty;
    public string OrgPlan { get; set; } = string.Empty;
    public string OrgType { get; set; } = string.Empty;
}
