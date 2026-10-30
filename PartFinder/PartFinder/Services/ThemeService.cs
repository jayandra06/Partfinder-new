using Microsoft.UI.Xaml;
using System.Text.Json;

namespace PartFinder.Services;

public sealed class ThemeService : IThemeService
{
    private readonly IAppStateStore _appState;
    private FrameworkElement? _shellRoot;
    private bool _isDark = true;

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PartFinder",
            "ui-theme.json");

    public ThemeService(IAppStateStore appState)
    {
        _appState = appState;
        Load();
    }

    public bool IsDark => _isDark;

    public ElementTheme Current => _isDark ? ElementTheme.Dark : ElementTheme.Light;

    public event EventHandler? ThemeChanged;

    public void AttachShell(FrameworkElement shellRoot)
    {
        _shellRoot = shellRoot;
        Apply();
    }

    public void SetDarkMode(bool isDark)
    {
        if (_isDark == isDark)
        {
            return;
        }

        _isDark = isDark;
        Save();
        Apply();
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Toggle() => SetDarkMode(!_isDark);

    private void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                _isDark = true;
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var dto = JsonSerializer.Deserialize<ThemeSettingsDto>(json);
            _isDark = dto?.IsDark ?? true;
        }
        catch
        {
            _isDark = true;
        }
    }

    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new ThemeSettingsDto { IsDark = _isDark });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-fatal: theme still applies for this session.
        }
    }

    private void Apply()
    {
        _appState.CurrentTheme = Current;
        if (_shellRoot is not null)
        {
            _shellRoot.RequestedTheme = Current;
        }
    }

    private sealed class ThemeSettingsDto
    {
        public bool IsDark { get; set; } = true;
    }
}
