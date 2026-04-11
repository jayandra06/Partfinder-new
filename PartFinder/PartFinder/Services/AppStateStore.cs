using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public sealed class AppStateStore : IAppStateStore
{
    public string CurrentTenant { get; set; } = "Default Site";
    public string CurrentUserName { get; set; } = "Operator";
    public ElementTheme CurrentTheme { get; set; } = ElementTheme.Default;
}
