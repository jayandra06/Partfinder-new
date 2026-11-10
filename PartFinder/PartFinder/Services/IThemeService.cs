using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public interface IThemeService
{
    bool IsDark { get; }
    ElementTheme Current { get; }
    event EventHandler? ThemeChanged;

    void AttachShell(FrameworkElement shellRoot);

    /// <summary>Main window content root — keeps app theme independent of Windows light/dark mode.</summary>
    void AttachWindowRoot(FrameworkElement windowRoot);

    void SetDarkMode(bool isDark);
    void Toggle();
}
