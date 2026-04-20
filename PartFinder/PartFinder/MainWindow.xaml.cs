using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using PartFinder.Services;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

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

    public MainWindow()
    {
        InitializeComponent();
        // Use native caption/title bar buttons to avoid overlap with app content.
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

        ShowShell();
    }

    private void ShowSubscriptionBlocked(string message)
    {
        StopMaintenanceTimer();
        MaintenanceBlockedRoot.Visibility = Visibility.Collapsed;
        SubscriptionBlockedMessage.Text = message;
        SubscriptionBlockedRoot.Visibility = Visibility.Visible;
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Collapsed;
    }

    private void ShowMaintenanceBlocked(string maintenanceUntilIso, string orgCode)
    {
        StopMaintenanceTimer();
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
        StopMaintenanceTimer();
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
        _step = 1;
        _workingOrgCode = "";
        _cachedOrgDatabaseUri = null;
        _resolvedSignedInEmail = null;
        _lastStatus = null;
        OrgCodeBox.Text = "";
        InviteLoginEmailBox.Text = "";
        InviteLoginPasswordBox.Password = "";
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

    private void ShowShell()
    {
        SetupRoot.Visibility = Visibility.Collapsed;
        ShellRoot.Visibility = Visibility.Visible;
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

    private void OnLetsGoClicked(object sender, RoutedEventArgs e)
    {
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
        if (_step == 3)
        {
            Step3EmailText.Text = string.IsNullOrWhiteSpace(_resolvedSignedInEmail)
                ? "Invited Email: —"
                : $"Invited Email: {_resolvedSignedInEmail}";
        }

        NextButton.Content = _step == 3 ? "Change Password" : "Next";
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
