using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Text.Json;
using Windows.UI;

namespace PartFinder.Services;

public sealed class ThemeService : IThemeService
{
    private readonly IAppStateStore _appState;
    private FrameworkElement? _shellRoot;
    private FrameworkElement? _windowRoot;
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

        if (Application.Current?.Resources is not null)
        {
            PaintBrushes();
        }
    }

    public bool IsDark => _isDark;

    public ElementTheme Current => _isDark ? ElementTheme.Dark : ElementTheme.Light;

    public event EventHandler? ThemeChanged;

    public void AttachShell(FrameworkElement shellRoot)
    {
        _shellRoot = shellRoot;
        Apply();
    }

    public void AttachWindowRoot(FrameworkElement windowRoot)
    {
        _windowRoot = windowRoot;
        Apply();
    }

    public void SetDarkMode(bool isDark)
    {
        if (_isDark == isDark)
        {
            PaintBrushes();
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

        if (_windowRoot is not null)
        {
            _windowRoot.RequestedTheme = Current;
        }

        if (_shellRoot is not null)
        {
            _shellRoot.RequestedTheme = Current;
        }

        PaintBrushes();
        _shellRoot?.DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            PaintBrushes);
    }

    /// <summary>
    /// Shared brushes are StaticResource references, so a theme switch does not retarget them.
    /// Paint the existing brush objects so text stays light on dark surfaces and dark on light surfaces.
    /// </summary>
    private void PaintBrushes()
    {
        if (Application.Current?.Resources is not ResourceDictionary resources)
        {
            return;
        }

        var palette = _isDark ? DarkPalette : LightPalette;
        foreach (var (key, color) in palette)
        {
            PaintBrushKey(resources, key, color);
        }

        RepaintMergedColorDictionaries(resources, palette);

        if (FindObject(resources, "AccentHeroBrush") is LinearGradientBrush hero && hero.GradientStops.Count >= 2)
        {
            hero.GradientStops[0].Color = _isDark
                ? Color.FromArgb(255, 124, 183, 255)
                : Color.FromArgb(255, 74, 149, 232);
            hero.GradientStops[1].Color = _isDark
                ? Color.FromArgb(255, 94, 162, 255)
                : Color.FromArgb(255, 47, 127, 210);
        }
    }

    private static void PaintBrushKey(ResourceDictionary root, string key, Color color)
    {
        if (FindBrush(root, key) is SolidColorBrush brush)
        {
            brush.Color = color;
        }
    }

    private static void RepaintMergedColorDictionaries(
        ResourceDictionary root,
        (string Key, Color Color)[] palette)
    {
        foreach (var merged in root.MergedDictionaries)
        {
            if (merged.ThemeDictionaries.Count > 0)
            {
                foreach (var (key, color) in palette)
                {
                    PaintBrushKey(merged, key, color);
                }
            }

            RepaintMergedColorDictionaries(merged, palette);
        }
    }

    private static SolidColorBrush? FindBrush(ResourceDictionary root, string key) =>
        FindObject(root, key) as SolidColorBrush;

    private static object? FindObject(ResourceDictionary root, string key)
    {
        if (root.TryGetValue(key, out var direct))
        {
            return direct;
        }

        foreach (var merged in root.MergedDictionaries)
        {
            var found = FindObject(merged, key);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static readonly (string Key, Color Color)[] DarkPalette = BuildPalette(
        page: "#50070C14",
        card: "#B8141D2A",
        elevated: "#D81A2637",
        input: "#96111A27",
        gridHeader: "#B21C2C40",
        gridAlt: "#660E1520",
        shell: "#A1162130",
        sidebar: "#8C0B1220",
        glass: "#4CF4F8FF",
        glassStrong: "#66B8D3FF",
        glassHighlight: "#22FFFFFF",
        rowHover: "#7A213047",
        rowSelected: "#A01D456C",
        rowSelectedBorder: "#FF7DB8FF",
        accent: "#FF5EA2FF",
        accentHover: "#FF7CB7FF",
        accentPressed: "#FF2F75D6",
        text: "#FFF4F8FF",
        textSecondary: "#FFC4D0DF",
        textTertiary: "#FF93A6C0",
        border: "#406884A8",
        borderHover: "#7A9CC2E8",
        borderFocus: "#FFBDD8FF",
        success: "#FF38D6A4",
        warning: "#FFFFC98D",
        danger: "#FFFFB4C7",
        info: "#FFB8D6FF",
        navHover: "#29243A57",
        navSelected: "#664A95E8",
        navSelectedBorder: "#995EA2FF",
        header: "#CC111A27",
        chart: "#FF141D2A",
        content: "#FF111A27",
        windowRoot: "#FF0A0E15",
        ambientBlue: "#332F7FD2",
        ambientPurple: "#2E8B5CF6",
        ambientPink: "#26F472B6",
        skeleton: "#26FFFFFF");

    private static readonly (string Key, Color Color)[] LightPalette = BuildPalette(
        page: "#FFD8E1EC",
        card: "#FFF0F4FA",
        elevated: "#FFFBFCFE",
        input: "#FFEEF2F8",
        gridHeader: "#FFC5D6EA",
        gridAlt: "#99E2E9F3",
        shell: "#FFE8EDF5",
        sidebar: "#FFDCE4EF",
        glass: "#5A8AA4C4",
        glassStrong: "#6E81A9D8",
        glassHighlight: "#55FFFFFF",
        rowHover: "#FFD8E6F4",
        rowSelected: "#CCE4F8FF",
        rowSelectedBorder: "#FF5EA2FF",
        accent: "#FF2F7FD2",
        accentHover: "#FF4A95E8",
        accentPressed: "#FF1F64B2",
        text: "#FF0B1220",
        textSecondary: "#FF2C3B4F",
        textTertiary: "#FF4A5C73",
        border: "#66A8BDD8",
        borderHover: "#8D7FA6D6",
        borderFocus: "#FF2F7FD2",
        success: "#FF0FAF7A",
        warning: "#FFDB8B14",
        danger: "#FFD95879",
        info: "#FF3D86E8",
        navHover: "#D8E4F2FF",
        navSelected: "#402F7FD2",
        navSelectedBorder: "#662F7FD2",
        header: "#E8EEF4F9",
        chart: "#FFF0F4FA",
        content: "#FFD8E1EC",
        windowRoot: "#FFD8E1EC",
        ambientBlue: "#3A3878C8",
        ambientPurple: "#348B5CF6",
        ambientPink: "#30E879A8",
        skeleton: "#2E7FA6D6");

    private static (string Key, Color Color)[] BuildPalette(
        string page,
        string card,
        string elevated,
        string input,
        string gridHeader,
        string gridAlt,
        string shell,
        string sidebar,
        string glass,
        string glassStrong,
        string glassHighlight,
        string rowHover,
        string rowSelected,
        string rowSelectedBorder,
        string accent,
        string accentHover,
        string accentPressed,
        string text,
        string textSecondary,
        string textTertiary,
        string border,
        string borderHover,
        string borderFocus,
        string success,
        string warning,
        string danger,
        string info,
        string navHover,
        string navSelected,
        string navSelectedBorder,
        string header,
        string chart,
        string content,
        string windowRoot,
        string ambientBlue,
        string ambientPurple,
        string ambientPink,
        string skeleton)
    {
        Color C(string hex) => Parse(hex);
        return
        [
            ("PageBackgroundBrush", C(page)),
            ("CardBackgroundBrush", C(card)),
            ("ElevatedCardBackgroundBrush", C(elevated)),
            ("InputBackgroundBrush", C(input)),
            ("GridHeaderBackgroundBrush", C(gridHeader)),
            ("GridRowAltBackgroundBrush", C(gridAlt)),
            ("ShellSurfaceBrush", C(shell)),
            ("ShellSidebarBrush", C(sidebar)),
            ("GlassStrokeBrush", C(glass)),
            ("GlassStrongStrokeBrush", C(glassStrong)),
            ("GlassHighlightBrush", C(glassHighlight)),
            ("RowHoverBackgroundBrush", C(rowHover)),
            ("RowSelectedBackgroundBrush", C(rowSelected)),
            ("RowSelectedBorderBrush", C(rowSelectedBorder)),
            ("AccentPrimaryBrush", C(accent)),
            ("AccentHoverBrush", C(accentHover)),
            ("AccentPressedBrush", C(accentPressed)),
            ("TextPrimaryBrush", C(text)),
            ("TextSecondaryBrush", C(textSecondary)),
            ("TextTertiaryBrush", C(textTertiary)),
            ("TextOnAccentBrush", Color.FromArgb(255, 255, 255, 255)),
            ("BorderDefaultBrush", C(border)),
            ("BorderHoverBrush", C(borderHover)),
            ("BorderFocusBrush", C(borderFocus)),
            ("SuccessBrush", C(success)),
            ("WarningBrush", C(warning)),
            ("DangerBrush", C(danger)),
            ("InfoBrush", C(info)),
            ("NavBackgroundBrush", C(sidebar)),
            ("NavItemHoverBrush", C(navHover)),
            ("NavItemSelectedBrush", C(navSelected)),
            ("NavItemSelectedBorderBrush", C(navSelectedBorder)),
            ("HeaderBarBackgroundBrush", C(header)),
            ("ChartSurfaceBackgroundBrush", C(chart)),
            ("ContentShellBackgroundBrush", C(content)),
            ("AmbientBlueBrush", C(ambientBlue)),
            ("AmbientPurpleBrush", C(ambientPurple)),
            ("AmbientPinkBrush", C(ambientPink)),
            ("AppBackgroundBrush", C(page)),
            ("AppSidebarBrush", C(sidebar)),
            ("AppSurfaceBrush", C(shell)),
            ("AppBorderBrush", C(border)),
            ("AppSubtleTextBrush", C(textSecondary)),
            ("AppTextBrush", C(text)),
            ("AppSuccessBrush", C(success)),
            ("AppSkeletonBrush", C(skeleton)),
        ];
    }

    private static Color Parse(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 6)
        {
            value = "FF" + value;
        }

        var packed = Convert.ToUInt32(value, 16);
        return Color.FromArgb(
            (byte)((packed >> 24) & 0xFF),
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF));
    }

    private sealed class ThemeSettingsDto
    {
        public bool IsDark { get; set; } = true;
    }
}
