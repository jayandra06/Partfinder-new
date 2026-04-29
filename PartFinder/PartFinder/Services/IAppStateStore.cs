using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public interface IAppStateStore
{
    string CurrentTenant { get; set; }
    string CurrentUserName { get; set; }
    ElementTheme CurrentTheme { get; set; }
    string OrgDisplayName { get; set; }
    string OrgPlan { get; set; }
    string OrgType { get; set; }
}
