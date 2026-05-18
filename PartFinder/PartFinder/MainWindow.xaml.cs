using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using PartFinder.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Windows.Foundation;

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
    private SetupStatusResult? _lastStatus;

    private Microsoft.UI.Xaml.DispatcherTimer? _maintenanceTimer;
    private DateTimeOffset? _maintenanceUntilUtc;
    private string? _orgCodeForMaintenanceRetry;

    private readonly string _setupFilePath = SetupPaths.SetupStateFilePath;

    private AppWindow? _shellTitleBarAppWindow;
    private TypedEventHandler<AppWindow, AppWindowChangedEventArgs>? _shellTitleBarAppWindowChangedHandler;

    public MainWindow()
    {
        InitializeComponent();
        // Setup / blocked / app-lock use the standard caption strip; shell applies extended chrome.
        ExtendsContentIntoTitleBar = false;
        BackButton.IsEnabled = false;


        if (IsSetupCompleted())
        {
            RootGrid.Loaded += OnRootGridLoadedPostSetupValidate;
            return;
        }

        RootGrid.Loaded += OnRootGridLoadedResumeWizard;
        UpdateStepUi();
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
    }

    private bool IsSetupCompleted()
    {
        var s = LoadSetupState();
        return s.completed && !string.IsNullOrWhiteSpace(s.orgCode);
    }

    private string? TryReadOrgCodeFromSetup() => LoadSetupState().orgCode?.Trim();

    private string? TryReadDbUriFromSetup() => LoadSetupState().dbUri?.Trim();

    private async void OnRootGridLoadedResumeWizard(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnRootGridLoadedResumeWizard;

        var saved = LoadSetupState();
        if (saved.invitedUserLogin && !string.IsNullOrWhiteSpace(saved.adminEmail))
        {
            _resolvedSignedInEmail = saved.adminEmail.Trim();
        }

        var code = TryReadOrgCodeFromSetup();
        if (string.IsNullOrWhiteSpace(code))
        {
            return;
        }

        OrgCodeBox.Text = code;
        _workingOrgCode = code;

        var (status, _) = await SetupApiClient.StatusAsync(_workingOrgCode);
        if (status is null || !status.Valid)
        {
            return;
        }

        _lastStatus = status;
        _cachedOrgDatabaseUri = status.OrgDatabaseUri;
        UpdateLicenseSummary(status);
        _step = ResolveTargetStep(status);
        UpdateStepUi();
    }

    private async void OnRootGridLoadedPostSetupValidate(object sender, RoutedEventArgs e)
    {
        RootGrid.Loaded -= OnRootGridLoadedPostSetupValidate;
        await ValidateAndShowShellAsync().ConfigureAwait(true);
    }

    private async Task ValidateAndShowShellAsync()
    {
        var orgCode = TryReadOrgCodeFromSetup();
        if (string.IsNullOrWhiteSpace(orgCode))
        {
            ShowSubscriptionBlocked(
                "No organization code was found in local setup. Use \"Re-enter organization code\" or complete setup again.");
            return;
        }

        var dbUri = TryReadDbUriFromSetup();
        if (string.IsNullOrWhiteSpace(dbUri))
        {
            ShowSubscriptionBlocked(
                "No database URI in local setup. Use \"Re-enter organization code\" to run setup again.");
            return;
        }

        var (status, statusErr) = await SetupApiClient.StatusAsync(orgCode);
        if (status is null)
        {
            var api = LicenseApiClient.GetBaseUrl();
            var detail = string.IsNullOrWhiteSpace(statusErr) ? "" : " " + statusErr.Trim();
            ShowSubscriptionBlocked(
                $"Active internet is required. Cannot reach the license server at {api}.{detail} Start PartFinder-Backend (npm run start in PartFinder-Backend) and check LicenseApi:BaseUrl in appsettings.json next to the app.");
            return;
        }

        if (!status.Valid)
        {
            ShowSubscriptionBlocked(
                string.IsNullOrWhiteSpace(status.Message)
                    ? "This organization is not licensed to use PartFinder right now."
                    : status.Message);
            return;
        }

        if (!await MongoConnectionTester.TryPingAsync(dbUri))
        {
            ShowSubscriptionBlocked(
                "Cannot reach your organization MongoDB from this PC. Check the network or MongoDB URI.");
            return;
        }

        var verify = await LicenseApiClient.VerifyAsync(orgCode);
        if (verify is null)
        {
            ShowSubscriptionBlocked(
                "Active internet is required to verify your license. Connect to the internet, then restart the app or use \"Re-enter organization code\" to try again.");
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
                    ? "License check failed. Is PartFinder-Backend running?"
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
        RestoreNonShellTitleBarChrome();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
        SubscriptionBlockedMessage.Text = message;
        SubscriptionBlockedRoot.Visibility = Visibility.Visible;
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Collapsed;
    }

    private void ShowMaintenanceBlocked(string maintenanceUntilIso, string orgCode)
    {
        StopMaintenanceTimer();
        RestoreNonShellTitleBarChrome();
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
                "Active internet is required to verify your license after maintenance. Connect to the internet and try again.");
            return;
        }

        if (verify.Valid)
        {
            var dbUri = TryReadDbUriFromSetup();
            if (string.IsNullOrWhiteSpace(dbUri))
            {
                ShowSubscriptionBlocked(
                    "No database URI in local setup. Use \"Re-enter organization code\" to run setup again.");
                return;
            }

            if (!await MongoConnectionTester.TryPingAsync(dbUri))
            {
                ShowSubscriptionBlocked(
                    "Cannot reach your organization MongoDB from this PC. Check the network or MongoDB URI.");
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
                ? "License check failed after maintenance."
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
        RestoreNonShellTitleBarChrome();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
        try
        {
            if (File.Exists(_setupFilePath))
            {
                File.Delete(_setupFilePath);
            }
        }
        catch
        {
        }

        SubscriptionBlockedRoot.Visibility = Visibility.Collapsed;
        SetupRoot.Visibility = Visibility.Visible;
        ShellRoot.Visibility = Visibility.Collapsed;
        AppLockRoot.Visibility = Visibility.Collapsed;
        _step = 1;
        _workingOrgCode = "";
        _cachedOrgDatabaseUri = null;
        _resolvedSignedInEmail = null;
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

    private void UpdateLicenseSummary(SetupStatusResult s)
    {
        LicenseSummaryPanel.Visibility = Visibility.Visible;
        LicenseOrgNameText.Text = $"Organization: {s.OrganizationName ?? "—"}";
        LicenseCodeText.Text = $"Code: {s.OrgCode ?? "—"}";
        LicenseStatusText.Text = $"Status: {s.Status ?? "—"}";
        LicenseExpiryText.Text = string.IsNullOrWhiteSpace(s.ValidUntil)
            ? "Validity: —"
            : $"Valid until (UTC): {s.ValidUntil}";
        LicenseLimitsText.Text =
            $"Limits: max users {s.MaxUsers ?? 0}, max parts {s.MaxParts ?? 0}";
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
        st.dbUri = uri;
        SaveSetupState(st);
    }

    private void SaveCompletedSetup()
    {
        var mode = DatabaseModeButtons.SelectedIndex == 1 ? "Custom" : "Default";
        var st = LoadSetupState();
        st.completed = true;
        st.orgCode = _workingOrgCode;
        st.dbUri = _cachedOrgDatabaseUri;
        st.databaseMode = mode;
        st.adminName = "Organization Admin";
        st.adminEmail = string.IsNullOrWhiteSpace(_resolvedSignedInEmail)
            ? InviteLoginEmailBox.Text.Trim()
            : _resolvedSignedInEmail;
        st.invitedUserLogin = !string.IsNullOrWhiteSpace(_resolvedSignedInEmail);
        SaveSetupState(st);
    }

    private void UnsubscribeShellTitleBarAppWindowChanged()
    {
        if (_shellTitleBarAppWindow is not null && _shellTitleBarAppWindowChangedHandler is not null)
        {
            _shellTitleBarAppWindow.Changed -= _shellTitleBarAppWindowChangedHandler;
        }
    }

    private void OnShellTitleBarAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        SyncShellTitleBarCaptionInsets();
    }

    private void ApplyShellExtendedTitleBar()
    {
        if (ShellRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        try
        {
            UnsubscribeShellTitleBarAppWindowChanged();
            _shellTitleBarAppWindowChangedHandler = null;
            _shellTitleBarAppWindow = null;

            var appWindow = AppWindow;
            _shellTitleBarAppWindow = appWindow;

            var caption = appWindow.TitleBar;
            caption.ExtendsContentIntoTitleBar = true;

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(ShellRoot.ShellTitleBarDragTarget);

            Windows.UI.Color bar = Windows.UI.Color.FromArgb(255, 10, 14, 21);
            caption.BackgroundColor = bar;
            caption.InactiveBackgroundColor = bar;
            caption.ForegroundColor = Windows.UI.Color.FromArgb(255, 234, 242, 255);
            caption.InactiveForegroundColor = Windows.UI.Color.FromArgb(255, 127, 141, 168);

            caption.ButtonBackgroundColor = bar;
            caption.ButtonInactiveBackgroundColor = bar;
            caption.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 234, 242, 255);
            caption.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 120, 130, 150);
            caption.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 30, 45, 70);
            caption.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            caption.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 25, 38, 58);
            caption.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);

            _shellTitleBarAppWindowChangedHandler = OnShellTitleBarAppWindowChanged;
            appWindow.Changed += _shellTitleBarAppWindowChangedHandler;

            SyncShellTitleBarCaptionInsets();
        }
        catch
        {
            RestoreNonShellTitleBarChrome();
        }
    }

    private void RestoreNonShellTitleBarChrome()
    {
        UnsubscribeShellTitleBarAppWindowChanged();
        _shellTitleBarAppWindowChangedHandler = null;
        _shellTitleBarAppWindow = null;

        try
        {
            SetTitleBar(null);
        }
        catch
        {
            // ignore
        }

        ExtendsContentIntoTitleBar = false;

        try
        {
            var caption = AppWindow.TitleBar;
            caption.ExtendsContentIntoTitleBar = false;
            caption.BackgroundColor = null;
            caption.InactiveBackgroundColor = null;
            caption.ForegroundColor = null;
            caption.InactiveForegroundColor = null;
            caption.ButtonBackgroundColor = null;
            caption.ButtonInactiveBackgroundColor = null;
            caption.ButtonForegroundColor = null;
            caption.ButtonInactiveForegroundColor = null;
            caption.ButtonHoverBackgroundColor = null;
            caption.ButtonHoverForegroundColor = null;
            caption.ButtonPressedBackgroundColor = null;
            caption.ButtonPressedForegroundColor = null;
        }
        catch
        {
            // ignore
        }

        ShellRoot.ResetShellTitleBarCaptionInsets();
    }

    private void SyncShellTitleBarCaptionInsets()
    {
        if (_shellTitleBarAppWindow is null || ShellRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        var caption = _shellTitleBarAppWindow.TitleBar;
        var scale = ShellRoot.XamlRoot?.RasterizationScale ?? 1.0;
        ShellRoot.UpdateShellTitleBarCaptionInsets(caption.LeftInset, caption.RightInset, scale);
    }

    private async void ShowShell()
    {
        RestoreNonShellTitleBarChrome();
        SetupRoot.Visibility = Visibility.Collapsed;

        // Check if Windows Hello app lock is enabled
        var security = App.Services.GetRequiredService<LocalUserSecurityStore>();
        security.Load();

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
                    ApplyShellExtendedTitleBar();
                    LogAutoLoginIfSessionActive();
                    return;
                }

                // Not verified — show lock screen with retry
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
        ApplyShellExtendedTitleBar();
        LogAutoLoginIfSessionActive();
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
            ApplyShellExtendedTitleBar();
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
        ValidationText.Text = string.Empty;

        if (_step == 1)
        {
            _resolvedSignedInEmail = null;
            if (string.IsNullOrWhiteSpace(OrgCodeBox.Text))
            {
                ValidationText.Text = "Please enter organization code.";
                return;
            }

            var trimmed = OrgCodeBox.Text.Trim();
            if (trimmed.Length != 6 || !trimmed.All(char.IsDigit))
            {
                ValidationText.Text = "Organization code must be exactly 6 digits.";
                return;
            }

            NextButton.IsEnabled = false;
            try
            {
                var (status, statusErr) = await SetupApiClient.StatusAsync(trimmed);
                if (status is null)
                {
                    var api = LicenseApiClient.GetBaseUrl();
                    var detail = string.IsNullOrWhiteSpace(statusErr) ? "" : " " + statusErr.Trim();
                    ValidationText.Text =
                        $"Active internet is required. Cannot reach {api}.{detail} Start PartFinder-Backend (port 3000 by default) and check LicenseApi:BaseUrl in appsettings.json.";
                    return;
                }

                if (!status.Valid)
                {
                    ValidationText.Text = string.IsNullOrWhiteSpace(status.Message)
                        ? "This organization code is not valid for PartFinder."
                        : status.Message;
                    return;
                }

                _workingOrgCode = trimmed;
                _lastStatus = status;
                _cachedOrgDatabaseUri = status.OrgDatabaseUri;
                UpdateLicenseSummary(status);
                var needsInviteLogin =
                    status.RequiresInviteLogin == true ||
                    string.Equals(status.OrgAdminStatus, "yes", StringComparison.OrdinalIgnoreCase);
                if (needsInviteLogin)
                {
                    var invitedEmail = InviteLoginEmailBox.Text.Trim();
                    var tempPassword = InviteLoginPasswordBox.Password;
                    if (string.IsNullOrWhiteSpace(invitedEmail) || string.IsNullOrWhiteSpace(tempPassword))
                    {
                        ValidationText.Text =
                            "This organization is already configured. Enter invited Email ID and temporary password.";
                        return;
                    }

                    if (!IsValidEmail(invitedEmail))
                    {
                        ValidationText.Text = "Please enter a valid invited Email ID.";
                        return;
                    }

                    var (okInviteLogin, inviteErr, _) = await SetupApiClient
                        .ValidateInviteLoginAsync(trimmed, invitedEmail, tempPassword)
                        .ConfigureAwait(true);
                    if (!okInviteLogin)
                    {
                        ValidationText.Text = inviteErr ?? "Invalid invited Email ID or temporary password.";
                        return;
                    }

                    _resolvedSignedInEmail = invitedEmail;
                }

                SaveProgressOrgCodeOnly(trimmed);
                _step = ResolveTargetStep(status);

                // If we can jump straight to Step 4 (org already fully configured),
                // persist completed setup so the shell can load orgCode/adminEmail/dbUri.
                if (_step == 4 && !string.IsNullOrWhiteSpace(status.OrgDatabaseUri))
                {
                    _cachedOrgDatabaseUri = status.OrgDatabaseUri;
                    SaveProgressAfterDatabase(trimmed, status.OrgDatabaseUri);
                    SaveCompletedSetup();
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
                ValidationText.Text = "Organization code is missing. Go back to step 1.";
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

                    if (!await MongoConnectionTester.TryPingAsync(customUri))
                    {
                        ValidationText.Text = "Cannot reach this MongoDB from this PC.";
                        return;
                    }

                    var (ok, err, savedUri) =
                        await SetupApiClient.SaveCustomDatabaseAsync(_workingOrgCode, customUri, false);
                    if (!ok)
                    {
                        if (!await MongoTenantBootstrap.TryInitializeTenantAsync(customUri))
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
                ValidationText.Text = "Organization code is missing. Go back to step 1.";
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
                var (ok, err, body) = await SetupApiClient.CreateOrgAdminAsync(
                    _workingOrgCode,
                    _resolvedSignedInEmail,
                    CurrentPasswordBox.Password,
                    NewPasswordBox.Password);
                if (!ok)
                {
                    ValidationText.Text = err ?? "Could not change password.";
                    return;
                }

                if (body?.Skipped == true)
                {
                    ValidationText.Text = "An admin already exists for this organization. Continuing.";
                }

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

    private async void OnLetsGoClicked(object sender, RoutedEventArgs e)
    {
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

        var ok = await MongoConnectionTester.TryPingAsync(MongoUriBox.Text.Trim());
        if (!ok)
        {
            ValidationText.Text = "Cannot ping this MongoDB URI from this PC.";
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
            1 => "Step 1 of 4: Organization",
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
