using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public interface IThemeService
{
    bool IsDark { get; }
    ElementTheme Current { get; }
    event EventHandler? ThemeChanged;

    void AttachShell(FrameworkElement shellRoot);
    void SetDarkMode(bool isDark);
    void Toggle();
}
