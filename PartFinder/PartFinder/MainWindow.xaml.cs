using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using PartFinder.Services;
using PartFinder.ViewModels;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Windows.Foundation;
using Windows.Graphics;
using WinRT.Interop;

namespace PartFinder;

public sealed partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions SetupJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private int _step = 1;
    private bool _isCustomDbConnectionSuccessful;
    private string _workingOrgCode = "";
    private string? _cachedOrgDatabaseUri;
    private string? _resolvedSignedInEmail;
    private string? _resolvedSignedInRole;
    private bool _mustChangePassword = true;
    private bool _signedInThisSession;
    private bool _loginTotpVerifiedThisSession;
    private SetupStatusResult? _lastStatus;

    private Microsoft.UI.Xaml.DispatcherTimer? _maintenanceTimer;
    private DateTimeOffset? _maintenanceUntilUtc;
    private string? _orgCodeForMaintenanceRetry;
    private bool _shellTitleBarRegionsActive;

    private readonly string _setupFilePath = SetupPaths.SetupStateFilePath;

    public MainWindow()
    {
        InitializeComponent();
        // WinUI ignores XAML EventTrigger, so a Loaded fade left this screen at opacity 0.
        SetupRoot.Opacity = 1;
        // Setup / blocked / app-lock use the standard caption strip; shell applies extended chrome.
        ExtendsContentIntoTitleBar = false;
        BackButton.IsEnabled = false;
        ShellRoot.SizeChanged += OnShellRootSizeChanged;

        RootGrid.Loaded += OnRootGridLoadedResumeWizard;
        UpdateStepUi();

        var themeService = App.Services.GetRequiredService<IThemeService>();
        themeService.AttachWindowRoot(RootGrid);
        themeService.ThemeChanged += OnShellThemeChangedForTitleBar;
    }

    private void OnShellThemeChangedForTitleBar(object? sender, EventArgs e)
    {
        if (_shellTitleBarRegionsActive)
        {
            ApplyShellTitleBarChrome();
        }
    }

    private AppWindow? TryGetAppWindow()
    {
        try
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Standard (non-extended) caption strip for login, setup, and blocked screens.</summary>
    private void ApplySetupTitleBarChrome()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = TryGetAppWindow()?.TitleBar;
        if (titleBar is null)
        {
            return;
        }

        var background = Windows.UI.Color.FromArgb(255, 10, 14, 21);
        var foreground = Windows.UI.Color.FromArgb(255, 234, 242, 255);
        var muted = Windows.UI.Color.FromArgb(255, 123, 141, 168);
        var hoverBg = Windows.UI.Color.FromArgb(255, 26, 39, 64);
        var pressedBg = Windows.UI.Color.FromArgb(255, 42, 61, 88);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = muted;
        titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        titleBar.ButtonInactiveForegroundColor = muted;
    }

    /// <summary>Extended shell title bar: transparent strip with theme-aware control glyphs.</summary>
    private void ApplyShellTitleBarChrome()
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = TryGetAppWindow()?.TitleBar;
        if (titleBar is null)
        {
            return;
        }

        var isDark = App.Services.GetRequiredService<IThemeService>().IsDark;
        var foreground = isDark
            ? Windows.UI.Color.FromArgb(255, 244, 248, 255)
            : Windows.UI.Color.FromArgb(255, 23, 34, 52);
        var muted = isDark
            ? Windows.UI.Color.FromArgb(255, 196, 208, 223)
            : Windows.UI.Color.FromArgb(255, 75, 95, 119);
        var hoverBg = isDark
            ? Windows.UI.Color.FromArgb(64, 255, 255, 255)
            : Windows.UI.Color.FromArgb(48, 0, 0, 0);
        var pressedBg = isDark
            ? Windows.UI.Color.FromArgb(96, 255, 255, 255)
            : Windows.UI.Color.FromArgb(72, 0, 0, 0);
        var transparent = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        titleBar.BackgroundColor = transparent;
        titleBar.InactiveBackgroundColor = transparent;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveForegroundColor = muted;
        titleBar.ButtonBackgroundColor = transparent;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hoverBg;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedBackgroundColor = pressedBg;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = transparent;
        titleBar.ButtonInactiveForegroundColor = muted;
    }

    private void EnableShellTitleBar()
    {
        try
        {
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(ShellRoot.TitleBarDragElement);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            _shellTitleBarRegionsActive = false;
            return;
        }

        _shellTitleBarRegionsActive = true;

        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            ShellRoot.UpdateTitleBarInsets(0, 0);
            UpdateShellTitleBarRegions();
            return;
        }

        ShellRoot.UpdateTitleBarInsets(0, 0);
        UpdateShellTitleBarRegions();
        ApplyShellTitleBarChrome();
    }

    private void DisableShellTitleBar()
    {
        _shellTitleBarRegionsActive = false;
        try
        {
            ExtendsContentIntoTitleBar = false;
            SetTitleBar(null);
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            // Window closing — ignore.
        }

        ShellRoot.UpdateTitleBarInsets(0, 0);
        ApplySetupTitleBarChrome();
    }

    private void OnShellRootSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_shellTitleBarRegionsActive)
        {
            UpdateShellTitleBarRegions();
        }
    }

    public void RefreshShellTitleBarRegions()
    {
        UpdateShellTitleBarRegions();
    }
     
    private void UpdateShellTitleBarRegions()
    {
        if (!_shellTitleBarRegionsActive || ShellRoot is null || ShellRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        try
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
            var inputSource = InputNonClientPointerSource.GetForWindowId(windowId);
            if (inputSource is null)
            {
                return;
            }

            var rects = ShellRoot.GetTitleBarPassthroughRects();
            inputSource.SetRegionRects(NonClientRegionKind.Passthrough, rects);
        }
        catch (System.Runtime.InteropServices.COMException ex)
            when (ex.HResult == unchecked((int)0x80010108)
                  || ex.HResult == unchecked((int)0x800710DD)
                  || ex.HResult == unchecked((int)0x80004005)
                  || ex.Message.Contains("closed", StringComparison.OrdinalIgnoreCase)
                  || ex.Message.Contains("not valid", StringComparison.OrdinalIgnoreCase))
        {
            _shellTitleBarRegionsActive = false;
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("warn", "UpdateShellTitleBarRegions", ex.Message);
        }
    }

    private sealed class SetupState
    {
        public bool completed { get; set; }
        public string? orgCode { get; set; }
        public string? dbUri { get; set; }
        public string? databaseMode { get; set; }
        public string? adminName { get; set; }
        public string? adminEmail { get; set; }
        public bool invitedUserLogin { get; set; }
    }

    private SetupState LoadSetupState()
    {
        try
        {
            var existingPath = SetupPaths.FindExistingSetupStatePath();
            if (!File.Exists(existingPath))
            {
                return new SetupState();
            }

            return JsonSerializer.Deserialize<SetupState>(File.ReadAllText(existingPath), SetupJson)
                   ?? new SetupState();
        }
        catch
        {
            return new SetupState();
        }
    }

    private void SaveSetupState(SetupState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_setupFilePath)!);
        File.WriteAllText(_setupFilePath, JsonSerializer.Serialize(state, SetupJson));
        SetupPaths.PurgeLegacySetupStateFiles();
    }

    private bool IsSetupCompleted()
    {
        var s = LoadSetupState();
        return s.completed && !string.IsNullOrWhiteSpace(s.orgCode);
    }

    private string? TryReadOrgCodeFromSetup() => LoadSetupState().orgCode?.Trim();

    private string? TryReadDbUriFromSetup() => LoadSetupState().dbUri?.Trim();

    private void OnRootGridLoadedResumeWizard(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnRootGridLoadedResumeWizard;
        ApplySetupTitleBarChrome();

        _signedInThisSession = false;

        // Do not pre-fill org code or email — user must enter them each sign-in (privacy).
        _workingOrgCode = string.Empty;
        _lastStatus = null;
        OrgCodeBox.Text = string.Empty;
        LicenseSummaryPanel.Visibility = Visibility.Collapsed;

        InviteLoginEmailBox.Text = string.Empty;
        InviteLoginPasswordBox.Password = string.Empty;
        _step = 1;
        SetupRoot.Opacity = 1;
        SetupRoot.Visibility = Visibility.Visible;
        ShellRoot.Visibility = Visibility.Collapsed;
        UpdateStepUi();
    }

    private bool _inviteLoginEmailLowercaseGuard;

    private void OnInviteLoginEmailBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_inviteLoginEmailLowercaseGuard || sender is not TextBox box)
        {
            return;
        }

        var text = box.Text;
        var lower = text.ToLowerInvariant();
        if (string.Equals(text, lower, StringComparison.Ordinal))
        {
            return;
        }

        _inviteLoginEmailLowercaseGuard = true;
        var caret = box.SelectionStart;
        box.Text = lower;
        box.SelectionStart = Math.Clamp(caret, 0, lower.Length);
        _inviteLoginEmailLowercaseGuard = false;
    }

    private void OnOrgCodeBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        var text = OrgCodeBox.Text.Trim();
        var previousOrg = LoadSetupState().orgCode?.Trim();
        if (!string.IsNullOrEmpty(previousOrg) &&
            text.Length == 6 &&
            !string.Equals(previousOrg, text, StringComparison.Ordinal))
        {
            _resolvedSignedInEmail = null;
            _resolvedSignedInRole = null;
            _mustChangePassword = true;
            InviteLoginPasswordBox.Password = string.Empty;
        }

        LicenseSummaryPanel.Visibility = Visibility.Collapsed;
    }

    private async void OnRootGridLoadedPostSetupValidate(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnRootGridLoadedPostSetupValidate;
        // Never open the shell without an explicit sign-in this session.
        if (!_signedInThisSession)
        {
            _step = 1;
            SetupRoot.Visibility = Visibility.Visible;
            ShellRoot.Visibility = Visibility.Collapsed;
            UpdateStepUi();
            return;
        }

        await ValidateAndShowShellAsync().ConfigureAwait(true);
    }

    private async Task ValidateAndShowShellAsync()
    {
        if (!_signedInThisSession)
        {
            _step = 1;
            SetupRoot.Visibility = Visibility.Visible;
            ShellRoot.Visibility = Visibility.Collapsed;
            UpdateStepUi();
            return;
        }

        var orgCode = TryReadOrgCodeFromSetup();
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            ShowSubscriptionBlocked(
                "No organisation code was found in local setup. Use \"Re-enter organisation code\" or complete setup again.");
            return;
        }

        var dbUri = TryReadDbUriFromSetup();
        if (string.IsNullOrWhiteSpace(dbUri))
        {
            ShowSubscriptionBlocked(
                "No database URI in local setup. Use \"Re-enter organisation code\" to run setup again.");
            return;
        }

        var (status, statusErr) = await SetupApiClient.StatusAsync(orgCode);
        if (status is null)
        {
            var api = LicenseApiClient.GetBaseUrl();
            var detail = string.IsNullOrWhiteSpace(statusErr) ? "" : " " + statusErr.Trim();
            ShowSubscriptionBlocked(
                $"Active internet is required. Cannot reach the API at {api}.{detail} Check your connection and LicenseApi:BaseUrl in appsettings.json (default: https://backend.partfinder.store).");
            return;
        }

        if (!status.Valid)
        {
            ShowSubscriptionBlocked(
                string.IsNullOrWhiteSpace(status.Message)
                    ? "This organisation is not licenced to use PartFinder right now."
                    : status.Message);
            return;
        }

        var ping = await MongoConnectionTester.PingAsync(dbUri, orgCode);
        if (!ping.Ok)
        {
            var detail = string.IsNullOrWhiteSpace(ping.ErrorDetail)
                ? ""
                : " " + ping.ErrorDetail.Trim();
            ShowSubscriptionBlocked(
                "Cannot reach your organisation MongoDB from this PC." + detail);
            return;
        }

        var verify = await LicenseApiClient.VerifyAsync(orgCode);
        if (verify is null)
        {
            ShowSubscriptionBlocked(
                "Active internet is required to verify your licence. Connect to the internet, then restart the app or use \"Re-enter organisation code\" to try again.");
            return;
        }

        if (!verify.Valid)
        {
            if (string.Equals(verify.Reason, "PLATFORM_MAINTENANCE", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(verify.MaintenanceUntil))
            {
                ShowMaintenanceBlocked(verify.MaintenanceUntil, orgCode);
                return;
            }

            ShowSubscriptionBlocked(
                string.IsNullOrWhiteSpace(verify.Message)
                    ? "Licence check failed. Is PartFinder-Backend running?"
                    : verify.Message);
            return;
        }

        var appState = App.Services.GetRequiredService<IAppStateStore>();
        appState.OrgDisplayName = !string.IsNullOrWhiteSpace(verify.OrganizationName)
            ? verify.OrganizationName
            : (!string.IsNullOrWhiteSpace(status.OrganizationName) ? status.OrganizationName : orgCode);
        appState.OrgPlan = !string.IsNullOrWhiteSpace(status.Plan) ? status.Plan : string.Empty;
        appState.OrgType = !string.IsNullOrWhiteSpace(status.OrgType) ? status.OrgType : string.Empty;

        ShowShell();
    }

    private void ShowSubscriptionBlocked(string message)
    {
        StopMaintenanceTimer();
        DisableShellTitleBar();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
        SubscriptionBlockedMessage.Text = message;
        SubscriptionBlockedRoot.Visibility = Visibility.Visible;
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Collapsed;
    }

    private void ShowMaintenanceBlocked(string maintenanceUntilIso, string orgCode)
    {
        StopMaintenanceTimer();
        DisableShellTitleBar();
        SubscriptionBlockedRoot.Visibility = Visibility.Collapsed;
        _orgCodeForMaintenanceRetry = orgCode.Trim();
        if (!DateTimeOffset.TryParse(
                maintenanceUntilIso,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var end))
        {
            ShowSubscriptionBlocked(
                "Maintenance is active but the end time could not be read. Try again later or contact your administrator.");
            return;
        }

        _maintenanceUntilUtc = end;
        MaintenanceBlockedRoot.Visibility = Visibility.Visible;
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Collapsed;
        RefreshMaintenanceCountdownUi();
        _maintenanceTimer ??= new Microsoft.UI.Xaml.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _maintenanceTimer.Tick -= OnMaintenanceTimerTick;
        _maintenanceTimer.Tick += OnMaintenanceTimerTick;
        _maintenanceTimer.Start();
    }

    private void StopMaintenanceTimer()
    {
        if (_maintenanceTimer != null)
        {
            _maintenanceTimer.Tick -= OnMaintenanceTimerTick;
            _maintenanceTimer.Stop();
        }

        _maintenanceUntilUtc = null;
        _orgCodeForMaintenanceRetry = null;
    }

    private void OnMaintenanceTimerTick(object? sender, object e)
    {
        if (_maintenanceUntilUtc == null)
        {
            return;
        }

        if (DateTimeOffset.UtcNow >= _maintenanceUntilUtc.Value)
        {
            _maintenanceTimer?.Stop();
            MaintenanceCountdownText.Text = "Checking…";
            _ = ResumeAfterMaintenanceAsync();
            return;
        }

        RefreshMaintenanceCountdownUi();
    }

    private void RefreshMaintenanceCountdownUi()
    {
        if (_maintenanceUntilUtc == null)
        {
            return;
        }

        var end = _maintenanceUntilUtc.Value;
        var now = DateTimeOffset.UtcNow;
        if (now >= end)
        {
            MaintenanceLiveByText.Text = "Maintenance window should have ended.";
            MaintenanceCountdownText.Text = "Time remaining: —";
            return;
        }

        var remaining = end - now;
        var local = end.ToLocalTime();
        MaintenanceLiveByText.Text =
            $"Will be live by {local:yyyy-MM-dd HH:mm} (local time).";
        MaintenanceCountdownText.Text = $"Time remaining: {FormatDuration(remaining)}";
    }

    private static string FormatDuration(TimeSpan t)
    {
        if (t.TotalSeconds <= 0)
        {
            return "0";
        }

        var days = (int)Math.Floor(t.TotalDays);
        var hours = t.Hours;
        var minutes = t.Minutes;
        var parts = new List<string>();
        if (days > 0)
        {
            parts.Add($"{days} day{(days == 1 ? "" : "s")}");
        }

        if (hours > 0)
        {
            parts.Add($"{hours} hr{(hours == 1 ? "" : "s")}");
        }

        if (days == 0 && hours == 0 && minutes > 0)
        {
            parts.Add($"{minutes} min");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "less than a minute";
    }

    private async Task ResumeAfterMaintenanceAsync()
    {
        var code = _orgCodeForMaintenanceRetry?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        var verify = await LicenseApiClient.VerifyAsync(code);
        if (verify is null)
        {
            ShowSubscriptionBlocked(
                "Active internet is required to verify your licence after maintenance. Connect to the internet and try again.");
            return;
        }

        if (verify.Valid)
        {
            var dbUri = TryReadDbUriFromSetup();
            if (string.IsNullOrWhiteSpace(dbUri))
            {
                ShowSubscriptionBlocked(
                    "No database URI in local setup. Use \"Re-enter organisation code\" to run setup again.");
                return;
            }

            var pingAfterMaintenance = await MongoConnectionTester.PingAsync(dbUri, code);
            if (!pingAfterMaintenance.Ok)
            {
                var detail = string.IsNullOrWhiteSpace(pingAfterMaintenance.ErrorDetail)
                    ? ""
                    : " " + pingAfterMaintenance.ErrorDetail.Trim();
                ShowSubscriptionBlocked(
                    "Cannot reach your organisation MongoDB from this PC." + detail);
                return;
            }

            StopMaintenanceTimer();
            MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
            ShowShell();
            return;
        }

        if (string.Equals(verify.Reason, "PLATFORM_MAINTENANCE", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(verify.MaintenanceUntil))
        {
            ShowMaintenanceBlocked(verify.MaintenanceUntil, code);
            return;
        }

        ShowSubscriptionBlocked(
            string.IsNullOrWhiteSpace(verify.Message)
                ? "Licence check failed after maintenance."
                : verify.Message);
    }

    private void OnSubscriptionBlockedExitClicked(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private void OnMaintenanceBlockedResetSetupClicked(object sender, RoutedEventArgs e)
    {
        StopMaintenanceTimer();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
        OnSubscriptionBlockedResetSetupClicked(sender, e);
    }

    private void OnSubscriptionBlockedResetSetupClicked(object sender, RoutedEventArgs e)
    {
        ResetToSetup();
    }

    public void ResetToSetup()
    {
        StopMaintenanceTimer();
        DisableShellTitleBar();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;

        // Clear ALL cached state from previous session
        try
        {
            var adminSession = App.Services.GetRequiredService<AdminSessionStore>();
            adminSession.Clear();
        }
        catch { }

        try
        {
            var profile = App.Services.GetRequiredService<LocalProfileStore>();
            profile.Clear();
        }
        catch { }

        SetupPaths.ClearAllSetupStateFiles();

        SubscriptionBlockedRoot.Visibility = Visibility.Collapsed;
        SetupRoot.Visibility = Visibility.Visible;
        ShellRoot.Visibility = Visibility.Collapsed;
        AppLockRoot.Visibility = Visibility.Collapsed;
        LoginTwoFactorRoot.Visibility = Visibility.Collapsed;
        _loginTotpVerifiedThisSession = false;
        LoginTotpBox.Text = string.Empty;
        _step = 1;
        _workingOrgCode = "";
        _cachedOrgDatabaseUri = null;
        _resolvedSignedInEmail = null;
        _resolvedSignedInRole = null;
        _mustChangePassword = true;
        _signedInThisSession = false;
        _lastStatus = null;
        OrgCodeBox.Text = "";
        InviteLoginEmailBox.Text = "";
        InviteLoginPasswordBox.Password = "";
        InviteLoginPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
        if (InviteLoginPasswordRevealIcon != null) InviteLoginPasswordRevealIcon.Glyph = "\uE890";
        if (CurrentPasswordBox != null) CurrentPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
        if (SetupCurrentPasswordRevealIcon != null) SetupCurrentPasswordRevealIcon.Glyph = "\uE890";
        if (NewPasswordBox != null) NewPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
        if (SetupNewPasswordRevealIcon != null) SetupNewPasswordRevealIcon.Glyph = "\uE890";
        if (ConfirmPasswordBox != null) ConfirmPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
        if (SetupConfirmPasswordRevealIcon != null) SetupConfirmPasswordRevealIcon.Glyph = "\uE890";
        ValidationText.Text = "";
        LicenseSummaryPanel.Visibility = Visibility.Collapsed;
        UpdateStepUi();
    }

    private static int ResolveTargetStep(SetupStatusResult s)
    {
        if (s.HasOrgDatabase != true)
        {
            return 2;
        }

        if (string.Equals(s.OrgAdminStatus, "yes", StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        return 3;
    }

    private void SaveProgressOrgCodeOnly(string code)
    {
        var st = LoadSetupState();
        if (!string.Equals(st.orgCode?.Trim(), code.Trim(), StringComparison.Ordinal))
        {
            st.dbUri = null;
            st.completed = false;
            st.databaseMode = null;
            st.adminName = null;
            _cachedOrgDatabaseUri = null;
        }

        st.orgCode = code;
        if (!string.IsNullOrWhiteSpace(_resolvedSignedInEmail))
        {
            st.adminEmail = _resolvedSignedInEmail;
            st.invitedUserLogin = true;
        }
        else
        {
            st.adminEmail = null;
            st.invitedUserLogin = false;
        }
        SaveSetupState(st);
    }

    private void SaveProgressAfterDatabase(string code, string uri)
    {
        var st = LoadSetupState();
        st.orgCode = code;
        try
        {
            st.dbUri = MongoConnectionStringUtil.Prepare(uri, code);
        }
        catch (ArgumentException)
        {
            st.dbUri = uri;
        }

        SaveSetupState(st);
    }

    private void SaveCompletedSetup()
    {
        var mode = DatabaseModeButtons.SelectedIndex == 1 ? "Custom" : "Default";
        var st = LoadSetupState();
        st.completed = true;
        st.orgCode = _workingOrgCode;
        var storedUri = _cachedOrgDatabaseUri;
        if (!string.IsNullOrWhiteSpace(storedUri))
        {
            try
            {
                storedUri = MongoConnectionStringUtil.Prepare(storedUri, _workingOrgCode);
            }
            catch (ArgumentException)
            {
                // Keep the URI that was entered when it is not a Mongo connection string.
            }
        }

        st.dbUri = storedUri;
        st.databaseMode = mode;
        st.adminName = "Organisation Admin";
        st.adminEmail = string.IsNullOrWhiteSpace(_resolvedSignedInEmail)
            ? InviteLoginEmailBox.Text.Trim()
            : _resolvedSignedInEmail;
        st.invitedUserLogin = !string.IsNullOrWhiteSpace(_resolvedSignedInEmail);
        SaveSetupState(st);
    }

    private async void ShowShell()
    {
        if (!_signedInThisSession)
        {
            _step = 1;
            SetupRoot.Visibility = Visibility.Visible;
            ShellRoot.Visibility = Visibility.Collapsed;
            UpdateStepUi();
            return;
        }

        try
        {
            DisableShellTitleBar();
            SetupRoot.Visibility = Visibility.Collapsed;

            var security = App.Services.GetRequiredService<LocalUserSecurityStore>();
            security.Load();

            if (security.TwoFactorEnabled && !_loginTotpVerifiedThisSession)
            {
                LoginTotpErrorBorder.Visibility = Visibility.Collapsed;
                LoginTotpBox.Text = string.Empty;
                LoginTwoFactorRoot.Visibility = Visibility.Visible;
                ShellRoot.Visibility = Visibility.Collapsed;
                AppLockRoot.Visibility = Visibility.Collapsed;
                return;
            }

            LoginTwoFactorRoot.Visibility = Visibility.Collapsed;

            if (security.AppLockEnabled)
            {
                // Check availability
                var available = await Windows.Security.Credentials.UI.UserConsentVerifier
                    .CheckAvailabilityAsync();

                if (available == Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
                {
                    var result = await Windows.Security.Credentials.UI.UserConsentVerifier
                        .RequestVerificationAsync("Verify your identity to open PartFinder.");

                    if (result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
                    {
                        AppLockRoot.Visibility = Visibility.Collapsed;
                        ShellRoot.Visibility = Visibility.Visible;
                        EnableShellTitleBar();
                        await ReinitializeShellAsync();
                        return;
                    }

                    // Not verified — show lock screen with retry
                    DisableShellTitleBar();
                    AppLockRoot.Visibility = Visibility.Visible;
                    ShellRoot.Visibility = Visibility.Collapsed;
                    AppLockErrorText.Text = "Verification failed. Try again.";
                    AppLockErrorBorder.Visibility = Visibility.Visible;
                    return;
                }
            }

            // App lock off or Windows Hello not available — open directly
            AppLockRoot.Visibility = Visibility.Collapsed;
            ShellRoot.Visibility = Visibility.Visible;
            EnableShellTitleBar();
            await ReinitializeShellAsync();
        }
        catch (Exception ex)
        {
            DebugLogClient.Post("error", "ShowShellException", ex.ToString());
            ShowSubscriptionBlocked($"Could not load application workspace: {ex.Message}");
        }
    }

    private async Task ReinitializeShellAsync()
    {
        // Always re-initialize shell with fresh data (permissions, name, nav items)
        // This ensures no stale data from previous user session.
        App.Services.GetRequiredService<INavigationService>().Clear();
        var shellVm = App.Services.GetRequiredService<ShellViewModel>();
        await shellVm.InitializeAsync().ConfigureAwait(true);
        LogAutoLoginIfSessionActive();
    }

    private void OnLoginTotpVerifyClick(object sender, RoutedEventArgs e)
    {
        LoginTotpErrorBorder.Visibility = Visibility.Collapsed;
        var code = LoginTotpBox.Text.Trim();
        if (code.Length != 6 || !code.All(char.IsDigit))
        {
            LoginTotpErrorText.Text = "Enter the 6-digit code from your authenticator app.";
            LoginTotpErrorBorder.Visibility = Visibility.Visible;
            return;
        }

        var security = App.Services.GetRequiredService<LocalUserSecurityStore>();
        security.Load();
        var secret = security.GetTwoFactorSecret();
        if (secret is null || !TotpHelper.Verify(code, secret))
        {
            LoginTotpErrorText.Text = "That code is not valid. Check the time on your device and try again.";
            LoginTotpErrorBorder.Visibility = Visibility.Visible;
            return;
        }

        _loginTotpVerifiedThisSession = true;
        LoginTwoFactorRoot.Visibility = Visibility.Collapsed;
        ShowShell();
    }

    private void OnLoginTotpBackClick(object sender, RoutedEventArgs e)
    {
        _signedInThisSession = false;
        _loginTotpVerifiedThisSession = false;
        LoginTotpBox.Text = string.Empty;
        LoginTwoFactorRoot.Visibility = Visibility.Collapsed;
        LoginTotpErrorBorder.Visibility = Visibility.Collapsed;
        SetupRoot.Visibility = Visibility.Visible;
        _step = 1;
        UpdateStepUi();
    }

    private void LogAutoLoginIfSessionActive()
    {
        try
        {
            var session = App.Services.GetRequiredService<AdminSessionStore>();
            session.Load();
            if (session.HasSession)
            {
                var logger = App.Services.GetRequiredService<ActivityLogger>();
                logger.Log("User Action", "Login", $"User '{session.Email ?? "Unknown"}' resumed session on startup");
            }
        }
        catch { /* ignore */ }
    }

    // ── App Lock handlers ─────────────────────────────────────────────

    private async void OnAppLockRetryClick(object sender, RoutedEventArgs e)
    {
        AppLockErrorBorder.Visibility = Visibility.Collapsed;

        var result = await Windows.Security.Credentials.UI.UserConsentVerifier
            .RequestVerificationAsync("Verify your identity to open PartFinder.");

        if (result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
        {
            AppLockRoot.Visibility = Visibility.Collapsed;
            ShellRoot.Visibility = Visibility.Visible;
            EnableShellTitleBar();
            LogAutoLoginIfSessionActive();
        }
        else
        {
            AppLockErrorText.Text = result switch
            {
                Windows.Security.Credentials.UI.UserConsentVerificationResult.Canceled
                    => "Verification was cancelled.",
                Windows.Security.Credentials.UI.UserConsentVerificationResult.DeviceNotPresent
                    => "No biometric device found.",
                _ => "Verification failed. Try again.",
            };
            AppLockErrorBorder.Visibility = Visibility.Visible;
        }
    }

    private void OnAppLockExitClick(object sender, RoutedEventArgs e)
    {
        Application.Current.Exit();
    }

    private void OnBackClicked(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            UpdateStepUi();
        }
    }

    private async void OnNextClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            ValidationText.Text = string.Empty;

            if (_step == 1)
        {
            if (string.IsNullOrWhiteSpace(OrgCodeBox.Text))
            {
                ShowValidationMessage("Please enter organisation code.");
                return;
            }

            var trimmed = OrgCodeBox.Text.Trim();
            if (trimmed.Length != 6 || !trimmed.All(char.IsDigit))
            {
                ShowValidationMessage("Organisation code must be exactly 6 digits.");
                return;
            }

            if (!string.Equals(_workingOrgCode, trimmed, StringComparison.Ordinal))
            {
                _resolvedSignedInEmail = null;
                _resolvedSignedInRole = null;
                _signedInThisSession = false;
                _mustChangePassword = true;
            }

            NextButton.IsEnabled = false;
            try
            {
                var (status, statusErr) = await SetupApiClient.StatusAsync(trimmed);
                if (status is null)
                {
                    var api = LicenseApiClient.GetBaseUrl();
                    var detail = string.IsNullOrWhiteSpace(statusErr) ? "" : " " + statusErr.Trim();
                    ShowValidationMessage(
                        $"Active internet is required. Cannot reach {api}.{detail} Check your connection and LicenseApi:BaseUrl in appsettings.json (default: https://backend.partfinder.store).");
                    return;
                }

                if (!status.Valid)
                {
                    ShowValidationMessage(string.IsNullOrWhiteSpace(status.Message)
                        ? "This organisation code is not valid for PartFinder."
                        : status.Message);
                    return;
                }

                _workingOrgCode = trimmed;
                _lastStatus = status;
                _cachedOrgDatabaseUri = status.OrgDatabaseUri;
                LicenseSummaryPanel.Visibility = Visibility.Collapsed;
                var needsInviteLogin =
                    status.RequiresInviteLogin == true ||
                    string.Equals(status.OrgAdminStatus, "yes", StringComparison.OrdinalIgnoreCase);
                if (needsInviteLogin)
                {
                    var invitedEmail = InviteLoginEmailBox.Text.Trim();
                    var tempPassword = InviteLoginPasswordBox.Password;
                    var alreadySignedInThisStep =
                        _signedInThisSession &&
                        string.Equals(_resolvedSignedInEmail, invitedEmail, StringComparison.OrdinalIgnoreCase);

                    if (!alreadySignedInThisStep)
                    {
                        if (string.IsNullOrWhiteSpace(invitedEmail) || string.IsNullOrWhiteSpace(tempPassword))
                        {
                            ShowValidationMessage(
                                "This organisation is already configured. Enter invited Email ID and temporary password.");
                            return;
                        }

                        if (!IsValidEmail(invitedEmail))
                        {
                            ShowValidationMessage("Please enter a valid invited Email ID.");
                            return;
                        }

                        var (okInviteLogin, inviteErr, inviteLoginResponse) = await SetupApiClient
                            .ValidateInviteLoginAsync(trimmed, invitedEmail, tempPassword)
                            .ConfigureAwait(true);
                        if (!okInviteLogin)
                        {
                            ShowValidationMessage(inviteErr ?? "Invalid invited Email ID or temporary password.");
                            return;
                        }

                        _resolvedSignedInEmail = invitedEmail;
                        _mustChangePassword = inviteLoginResponse?.MustChangePassword ?? true;
                        _resolvedSignedInRole = inviteLoginResponse?.Role;
                        _signedInThisSession = true;

                        if (!string.IsNullOrWhiteSpace(inviteLoginResponse?.AccessToken))
                        {
                            var adminSession = App.Services.GetRequiredService<AdminSessionStore>();
                            adminSession.Save(inviteLoginResponse.AccessToken, invitedEmail);
                        }

                        if (!_mustChangePassword)
                        {
                            var directory = App.Services.GetService<IOrgUserDirectoryService>();
                            if (directory is not null)
                            {
                                _ = await directory.MarkInvitedUserActiveAfterLoginAsync(invitedEmail)
                                    .ConfigureAwait(true);
                            }
                        }
                    }
                }

                SaveProgressOrgCodeOnly(trimmed);
                _step = ResolveTargetStep(status);

                // Configured org: require successful sign-in above; never open shell from Step 1.
                if (_step == 4 && !string.IsNullOrWhiteSpace(status.OrgDatabaseUri))
                {
                    _cachedOrgDatabaseUri = status.OrgDatabaseUri;
                    SaveProgressAfterDatabase(trimmed, status.OrgDatabaseUri);

                    if (string.IsNullOrWhiteSpace(_resolvedSignedInEmail))
                    {
                        ShowValidationMessage(
                            "This organisation requires sign-in. Enter your email and password.");
                        _step = 1;
                        UpdateStepUi();
                        return;
                    }

                    if (_mustChangePassword)
                    {
                        // Invited user with temporary password — force password change step.
                        _step = 3;
                    }
                    else
                    {
                        SaveCompletedSetup();
                        var appState = App.Services.GetRequiredService<IAppStateStore>();
                        appState.OrgDisplayName = !string.IsNullOrWhiteSpace(status.OrganizationName)
                            ? status.OrganizationName
                            : trimmed;
                        appState.OrgPlan = !string.IsNullOrWhiteSpace(status.Plan) ? status.Plan : string.Empty;
                        appState.OrgType = !string.IsNullOrWhiteSpace(status.OrgType) ? status.OrgType : string.Empty;
                        _step = 4;
                    }
                }
            }
            finally
            {
                NextButton.IsEnabled = true;
            }

            UpdateStepUi();
            return;
        }

        if (_step == 2)
        {
            if (string.IsNullOrWhiteSpace(_workingOrgCode))
            {
                ValidationText.Text = "Organisation code is missing. Go back to step 1.";
                return;
            }

            NextButton.IsEnabled = false;
            try
            {
                if (DatabaseModeButtons.SelectedIndex == 0)
                {
                    var (ok, err, uri) = await SetupApiClient.ProvisionDefaultAsync(_workingOrgCode);
                    if (!ok || string.IsNullOrWhiteSpace(uri))
                    {
                        ValidationText.Text = err ?? "Could not provision default database.";
                        return;
                    }

                    _cachedOrgDatabaseUri = uri;
                    SaveProgressAfterDatabase(_workingOrgCode, uri);
                    var (st, _) = await SetupApiClient.StatusAsync(_workingOrgCode);
                    if (st != null)
                    {
                        _lastStatus = st;
                    }

                    _step = ResolveTargetStep(
                        st ?? new SetupStatusResult
                        {
                            Valid = true,
                            HasOrgDatabase = true,
                            OrgDatabaseUri = uri,
                            OrgAdminStatus = "no",
                        });
                }
                else
                {
                    var customUri = MongoUriBox.Text.Trim();
                    if (string.IsNullOrWhiteSpace(customUri))
                    {
                        ValidationText.Text = "Please paste MongoDB URI.";
                        return;
                    }

                    var customPing = await MongoConnectionTester.PingAsync(customUri, _workingOrgCode);
                    if (!customPing.Ok)
                    {
                        ValidationText.Text = string.IsNullOrWhiteSpace(customPing.ErrorDetail)
                            ? "Cannot reach this MongoDB from this PC."
                            : customPing.ErrorDetail;
                        return;
                    }

                    var (ok, err, savedUri) =
                        await SetupApiClient.SaveCustomDatabaseAsync(_workingOrgCode, customUri, false);
                    if (!ok)
                    {
                        if (!await MongoTenantBootstrap.TryInitializeTenantAsync(customUri, _workingOrgCode))
                        {
                            ValidationText.Text = err ?? "Server could not save database URI.";
                            return;
                        }

                        (ok, err, savedUri) =
                            await SetupApiClient.SaveCustomDatabaseAsync(
                                _workingOrgCode,
                                customUri,
                                true);
                    }

                    if (!ok || string.IsNullOrWhiteSpace(savedUri))
                    {
                        ValidationText.Text = err ?? "Could not save custom database URI.";
                        return;
                    }

                    _cachedOrgDatabaseUri = savedUri;
                    SaveProgressAfterDatabase(_workingOrgCode, savedUri);
                    var (st2, _) = await SetupApiClient.StatusAsync(_workingOrgCode);
                    if (st2 != null)
                    {
                        _lastStatus = st2;
                    }

                    _step = ResolveTargetStep(
                        st2 ?? new SetupStatusResult
                        {
                            Valid = true,
                            HasOrgDatabase = true,
                            OrgDatabaseUri = savedUri,
                            OrgAdminStatus = "no",
                        });
                }
            }
            finally
            {
                NextButton.IsEnabled = true;
            }

            UpdateStepUi();
            return;
        }

        if (_step == 3)
        {
            if (string.IsNullOrWhiteSpace(_workingOrgCode))
            {
                ValidationText.Text = "Organisation code is missing. Go back to step 1.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_resolvedSignedInEmail))
            {
                ValidationText.Text = "Invited admin email is missing. Go back to Step 1 and sign in.";
                return;
            }

            if (string.IsNullOrWhiteSpace(CurrentPasswordBox.Password) ||
                string.IsNullOrWhiteSpace(NewPasswordBox.Password) ||
                string.IsNullOrWhiteSpace(ConfirmPasswordBox.Password))
            {
                ValidationText.Text = "Current temporary password, new password, and confirm password are required.";
                return;
            }

            if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
            {
                ValidationText.Text = "New password and confirm password do not match.";
                return;
            }

            if (!IsStrongPassword(NewPasswordBox.Password))
            {
                ValidationText.Text = "New password must be at least 8 chars with upper, lower, and number.";
                return;
            }

            NextButton.IsEnabled = false;
            try
            {
                // Invited employees use ChangePassword; first org admin uses CreateOrgAdmin.
                bool ok;
                string? err;
                var isFirstAdminTempChange =
                    _mustChangePassword &&
                    _lastStatus?.RequiresInviteLogin == true &&
                    !string.IsNullOrWhiteSpace(_lastStatus.FirstAdminEmail) &&
                    string.Equals(_resolvedSignedInEmail, _lastStatus.FirstAdminEmail, StringComparison.OrdinalIgnoreCase);

                if (isFirstAdminTempChange)
                {
                    var (loginOk, loginErr, _) = await SetupApiClient
                        .ValidateInviteLoginAsync(_workingOrgCode, _resolvedSignedInEmail!, CurrentPasswordBox.Password)
                        .ConfigureAwait(true);
                    if (!loginOk)
                    {
                        ValidationText.Text = loginErr ?? "Current temporary password is invalid.";
                        return;
                    }

                    var uri = !string.IsNullOrWhiteSpace(_cachedOrgDatabaseUri)
                        ? _cachedOrgDatabaseUri
                        : _lastStatus?.OrgDatabaseUri;
                    if (string.IsNullOrWhiteSpace(uri) ||
                        !await MongoTenantBootstrap.TrySetOrgAdminPasswordAsync(
                            uri,
                            _resolvedSignedInEmail!,
                            NewPasswordBox.Password).ConfigureAwait(true))
                    {
                        ValidationText.Text = "Could not save the new password. Check the organisation database connection.";
                        return;
                    }

                    var (adminOk, adminErr, _) = await SetupApiClient.CreateOrgAdminAsync(
                        _workingOrgCode,
                        _resolvedSignedInEmail!,
                        CurrentPasswordBox.Password,
                        NewPasswordBox.Password).ConfigureAwait(true);
                    ok = true;
                    err = adminOk ? null : adminErr;
                }
                else if (_mustChangePassword && !string.Equals(_lastStatus?.OrgAdminStatus, "no", StringComparison.OrdinalIgnoreCase))
                {
                    // Invited user (employee or returning admin) — use change-password endpoint
                    (ok, err) = await SetupApiClient.ChangePasswordAsync(
                        _workingOrgCode,
                        _resolvedSignedInEmail,
                        CurrentPasswordBox.Password,
                        NewPasswordBox.Password);
                }
                else
                {
                    // First admin setup — use create-org-admin endpoint
                    var (adminOk, adminErr, body) = await SetupApiClient.CreateOrgAdminAsync(
                        _workingOrgCode,
                        _resolvedSignedInEmail,
                        CurrentPasswordBox.Password,
                        NewPasswordBox.Password);
                    ok = adminOk;
                    err = adminErr;

                    if (body?.Skipped == true)
                    {
                        ValidationText.Text = "An admin already exists for this organisation. Continuing.";
                    }
                }

                if (!ok)
                {
                    ValidationText.Text = err ?? "Could not change password.";
                    return;
                }

                if (!string.IsNullOrWhiteSpace(_resolvedSignedInEmail))
                {
                    var directory = App.Services.GetService<IOrgUserDirectoryService>();
                    if (directory is not null)
                    {
                        _ = await directory.CompleteInvitedUserActivationAsync(
                            _resolvedSignedInEmail,
                            NewPasswordBox.Password).ConfigureAwait(true);
                    }
                }

                _signedInThisSession = true;
                SaveCompletedSetup();
                _step = 4;
            }
            finally
            {
                NextButton.IsEnabled = true;
            }

            UpdateStepUi();
            return;
        }

        if (_step < 4)
        {
            _step++;
            UpdateStepUi();
        }
    }
    catch (Exception ex)
    {
        NextButton.IsEnabled = true;
        ValidationText.Text = $"Error: {ex.Message}";
        DebugLogClient.Post("error", "OnNextClickedException", ex.ToString());
    }
}

    private async void OnLetsGoClicked(object sender, RoutedEventArgs e)
    {
        if (!_signedInThisSession && string.IsNullOrWhiteSpace(_resolvedSignedInEmail))
        {
            ValidationText.Text = "Sign in on Step 1 before launching PartFinder.";
            _step = 1;
            UpdateStepUi();
            return;
        }

        _signedInThisSession = true;

        // Fetch and store org display name + plan before showing shell
        // so Settings page ORGANIZATION section is never blank after login.
        var orgCode = TryReadOrgCodeFromSetup();
        if (!string.IsNullOrWhiteSpace(orgCode))
        {
            try
            {
                var appState = App.Services.GetRequiredService<IAppStateStore>();
                var (status, _) = await SetupApiClient.StatusAsync(orgCode);
                var verify = await LicenseApiClient.VerifyAsync(orgCode);

                if (verify is not null && verify.Valid)
                {
                    appState.OrgDisplayName = !string.IsNullOrWhiteSpace(verify.OrganizationName)
                        ? verify.OrganizationName
                        : (!string.IsNullOrWhiteSpace(status?.OrganizationName) ? status.OrganizationName : orgCode);
                }
                else if (status is not null)
                {
                    appState.OrgDisplayName = !string.IsNullOrWhiteSpace(status.OrganizationName)
                        ? status.OrganizationName : orgCode;
                }
                else
                {
                    appState.OrgDisplayName = orgCode;
                }

                appState.OrgPlan = !string.IsNullOrWhiteSpace(status?.Plan) ? status.Plan : string.Empty;
                appState.OrgType = !string.IsNullOrWhiteSpace(status?.OrgType) ? status.OrgType : string.Empty;
            }
            catch { /* best effort — shell will show with whatever is available */ }
        }

        ShowShell();
    }

    private void OnDatabaseModeChanged(object sender, SelectionChangedEventArgs e)
    {
        var isCustom = DatabaseModeButtons.SelectedIndex == 1;
        CustomDbPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;

        if (!isCustom)
        {
            _isCustomDbConnectionSuccessful = true;
            TestConnectionButton.Visibility = Visibility.Visible;
            ConnectionSuccessText.Visibility = Visibility.Collapsed;
            ConnectionSuccessText.Text = "DB connection successful.";
        }
        else
        {
            _isCustomDbConnectionSuccessful = false;
            TestConnectionButton.Visibility = Visibility.Visible;
            ConnectionSuccessText.Visibility = Visibility.Collapsed;
        }

        if (_step == 2)
        {
            NextButton.IsEnabled = !isCustom || _isCustomDbConnectionSuccessful;
        }
    }

    private async void OnTestConnectionClicked(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(MongoUriBox.Text))
        {
            ValidationText.Text = "Please paste MongoDB URI first.";
            return;
        }

        var testPing = await MongoConnectionTester.PingAsync(MongoUriBox.Text.Trim(), _workingOrgCode);
        if (!testPing.Ok)
        {
            ValidationText.Text = string.IsNullOrWhiteSpace(testPing.ErrorDetail)
                ? "Cannot ping this MongoDB URI from this PC."
                : testPing.ErrorDetail;
            _isCustomDbConnectionSuccessful = false;
            NextButton.IsEnabled = false;
            return;
        }

        _isCustomDbConnectionSuccessful = true;
        TestConnectionButton.Visibility = Visibility.Collapsed;
        ConnectionSuccessText.Visibility = Visibility.Visible;
        ConnectionSuccessText.Text = "DB connection successful.";
        NextButton.IsEnabled = true;
    }

    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
    {
        var isStrong = IsStrongPassword(NewPasswordBox.Password);
        PasswordHintText.Text = isStrong
            ? "Password strength: strong"
            : "New password must be at least 8 characters and include upper, lower, and a number.";
        PasswordHintText.Foreground = isStrong
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 163, 74))
            : (Brush)Application.Current.Resources["AppSubtleTextBrush"];
    }

    private void UpdateStepUi()
    {
        Step1Panel.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Panel.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Panel.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Panel.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
        NavigationRow.Visibility = _step == 4 ? Visibility.Collapsed : Visibility.Visible;
        BackButton.IsEnabled = _step > 1;
        if (_step == 2)
        {
            var isCustom = DatabaseModeButtons.SelectedIndex == 1;
            CustomDbPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            if (!isCustom)
            {
                _isCustomDbConnectionSuccessful = true;
            }

            NextButton.IsEnabled = !isCustom || _isCustomDbConnectionSuccessful;
        }
        else
        {
            NextButton.IsEnabled = true;
        }

        StepTitle.Text = _step switch
        {
            1 => "Step 1 of 4: Organisation",
            2 => "Step 2 of 4: Database",
            3 => "Step 3 of 4: Change Password",
            _ => "Step 4 of 4: Completed",
        };
        ProgressText.Text = _step switch
        {
            1 => "● ○ ○ ○",
            2 => "● ● ○ ○",
            3 => "● ● ● ○",
            _ => "● ● ● ●",
        };

        // Update left panel step circles
        UpdateStepCircle(Step1Circle, Step1CircleText, Step1Label, stepNum: 1);
        UpdateStepCircle(Step2Circle, Step2CircleText, Step2Label, stepNum: 2);
        UpdateStepCircle(Step3Circle, Step3CircleText, Step3Label, stepNum: 3);
        UpdateStepCircle(Step4Circle, Step4CircleText, Step4Label, stepNum: 4);

        // Connector lines: highlight if step before them is done
        Step1Connector.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            _step > 1 ? Windows.UI.Color.FromArgb(255, 31, 122, 224) : Windows.UI.Color.FromArgb(255, 42, 61, 88));
        Step2Connector.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            _step > 2 ? Windows.UI.Color.FromArgb(255, 31, 122, 224) : Windows.UI.Color.FromArgb(255, 42, 61, 88));
        Step3Connector.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
            _step > 3 ? Windows.UI.Color.FromArgb(255, 31, 122, 224) : Windows.UI.Color.FromArgb(255, 42, 61, 88));

        if (_step == 3)
        {
            Step3EmailText.Text = string.IsNullOrWhiteSpace(_resolvedSignedInEmail)
                ? "Invited Email: —"
                : $"Invited Email: {_resolvedSignedInEmail}";

            var roleLabel = string.Equals(_resolvedSignedInRole, "Admin", StringComparison.OrdinalIgnoreCase)
                ? "Admin Account"
                : "Employee Account";
            Step3AccountTypeText.Text = roleLabel;
        }

        NextButton.Content = _step == 3 ? "Change Password" : "Next";
    }

    private void UpdateStepCircle(Border circle, TextBlock circleText, StackPanel label, int stepNum)
    {
        var isCompleted = _step > stepNum;
        var isActive    = _step == stepNum;

        if (isCompleted)
        {
            // Green checkmark - step done
            circle.Background     = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 42, 189, 143));
            circle.BorderThickness = new Thickness(0);
            circle.Opacity        = 1.0;
            circleText.Text       = "\u2713"; // ✓
            circleText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            label.Opacity         = 1.0;
        }
        else if (isActive)
        {
            // Blue - current step
            circle.Background     = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 122, 224));
            circle.BorderThickness = new Thickness(0);
            circle.Opacity        = 1.0;
            circleText.Text       = stepNum.ToString();
            circleText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            label.Opacity         = 1.0;
        }
        else
        {
            // Grey - future step
            circle.Background     = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 23, 35, 52));
            circle.BorderThickness = new Thickness(1.5);
            circle.Opacity        = 0.5;
            circleText.Text       = stepNum.ToString();
            circleText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 123, 141, 168));
            label.Opacity         = 0.4;
        }
    }

    private void OnInviteLoginPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (InviteLoginPasswordBox.PasswordRevealMode == Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden)
        {
            InviteLoginPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Visible;
            InviteLoginPasswordRevealIcon.Glyph = "\uF22B";
        }
        else
        {
            InviteLoginPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
            InviteLoginPasswordRevealIcon.Glyph = "\uE890";
        }
    }

    private void OnSetupCurrentPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (CurrentPasswordBox.PasswordRevealMode == Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden)
        {
            CurrentPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Visible;
            SetupCurrentPasswordRevealIcon.Glyph = "\uF22B";
        }
        else
        {
            CurrentPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
            SetupCurrentPasswordRevealIcon.Glyph = "\uE890";
        }
    }

    private void OnSetupNewPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (NewPasswordBox.PasswordRevealMode == Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden)
        {
            NewPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Visible;
            SetupNewPasswordRevealIcon.Glyph = "\uF22B";
        }
        else
        {
            NewPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
            SetupNewPasswordRevealIcon.Glyph = "\uE890";
        }
    }

    private void OnSetupConfirmPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (ConfirmPasswordBox.PasswordRevealMode == Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden)
        {
            ConfirmPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Visible;
            SetupConfirmPasswordRevealIcon.Glyph = "\uF22B";
        }
        else
        {
            ConfirmPasswordBox.PasswordRevealMode = Microsoft.UI.Xaml.Controls.PasswordRevealMode.Hidden;
            SetupConfirmPasswordRevealIcon.Glyph = "\uE890";
        }
    }

    private void ShowValidationMessage(string message)
    {
        ValidationText.Text = message;
        ValidationText.UpdateLayout();
        try
        {
            var top = Math.Max(0, ValidationText.ActualOffset.Y - 24);
            SetupFormScrollViewer?.ChangeView(null, top, null, disableAnimation: true);
        }
        catch
        {
            // Best effort — message is still set.
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var at = email.IndexOf('@');
        var dot = email.LastIndexOf('.');
        return at > 0 && dot > at + 1 && dot < email.Length - 1;
    }

    private static bool IsStrongPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            return false;
        }

        var hasUpper = password.Any(char.IsUpper);
        var hasLower = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        return hasUpper && hasLower && hasDigit;
    }
}
