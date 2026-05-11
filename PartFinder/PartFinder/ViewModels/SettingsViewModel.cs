using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PartFinder.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using System.Threading;
using Windows.Storage;
using System.Collections.ObjectModel;
using PartFinder.Models;
using System.Net.Http;
using System.Text.Json;

namespace PartFinder.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly LocalUserSecurityStore _security;
    private readonly AdminSessionStore _session;
    private readonly LocalProfileStore _profile;
    private readonly ActivityLogger _activityLogger;
    private readonly IAppStateStore _appState;
    private readonly ILocalSetupContext _setupContext;
    private readonly DeviceMetadataService _metadataService;
    private readonly SessionPersistenceService _sessionPersistence;
    private string? _pendingTwoFactorSecret;

    public SettingsViewModel(
        LocalUserSecurityStore security,
        AdminSessionStore session,
        LocalProfileStore profile,
        ActivityLogger activityLogger,
        IAppStateStore appState,
        ILocalSetupContext setupContext,
        DeviceMetadataService metadataService,
        SessionPersistenceService sessionPersistence)
    {
        _security = security;
        _session = session;
        _profile = profile;
        _activityLogger = activityLogger;
        _appState = appState;
        _setupContext = setupContext;
        _metadataService = metadataService;
        _sessionPersistence = sessionPersistence;
        _setupContext.Refresh();

        LoginHistoryItems = new ObservableCollection<LoginHistory>();
        _ = LoadLoginHistoryAsync();

        // Load the actual persisted session
        _ = LoadLoginSessionAsync();

        // MANDATORY TEST ASSIGNMENTS (Step 4)
        LastLoginIp = "192.168.1.1";
        LastLoginLocation = "Hyderabad, IN";
        LastLoginBrowser = "WinUI 3 Desktop App";
        LastLoginOsVersion = Environment.OSVersion.ToString();
        LastLoginDeviceName = Environment.MachineName;
        LastLoginTime = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt");
        LastLoginCoordinates = "17.3850, 78.4867";

        RefreshAllState();

        System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] INSTANCE CREATED: {this.GetHashCode()}");
    }

    /// <summary>Org code from local setup state.</summary>
    public string OrgCode =>
        !string.IsNullOrWhiteSpace(_setupContext.OrgCode) ? _setupContext.OrgCode! : "—";

    /// <summary>Real organization name from the license server.</summary>
    public string OrgDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_appState.OrgDisplayName))
                return _appState.OrgDisplayName;
            // Fallback: use OrgCode from local setup state (available after logout+restart)
            if (!string.IsNullOrWhiteSpace(_setupContext.OrgCode))
                return _setupContext.OrgCode!;
            return "—";
        }
    }

    /// <summary>Plan from the license server (e.g. "starter", "pro") — Title-cased for display.</summary>
    public string OrgPlanDisplay
    {
        get
        {
            var plan = _appState.OrgPlan?.Trim();
            var type = _appState.OrgType?.Trim();

            // Build a combined display: "Starter · Standard" or just "Starter"
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(plan))
                parts.Add(ToTitleCase(plan));
            if (!string.IsNullOrWhiteSpace(type))
                parts.Add(ToTitleCase(type));

            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }
    }

    /// <summary>
    /// Called when the Settings page is navigated to — refreshes org info from AppStateStore
    /// so the ORGANIZATION section always shows the latest data fetched at login.
    /// </summary>
    public void RefreshOrgInfo()
    {
        _setupContext.Refresh();
        OnPropertyChanged(nameof(OrgCode));
        OnPropertyChanged(nameof(OrgDisplayName));
        OnPropertyChanged(nameof(OrgPlanDisplay));
    }

    private static string ToTitleCase(string s) =>
        string.IsNullOrWhiteSpace(s) ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeCurrent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeNew { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeConfirm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PasscodeIsConfigured { get; set; }

    [ObservableProperty]
    public partial bool ShowAppLockEditor { get; set; }

    [ObservableProperty]
    public partial bool AppLockEnabled { get; set; }

    partial void OnAppLockEnabledChanged(bool value)
    {
        _security.SetAppLockEnabled(value);
        _activityLogger.LogUserAction("App Lock", value ? "Windows Hello lock enabled" : "Windows Hello lock disabled");
    }

    [ObservableProperty]
    public partial string TwoFactorSecretDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? TwoFactorQrImage { get; set; }

    [ObservableProperty]
    public partial string TwoFactorConfirmCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TwoFactorMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool TwoFactorEnabled { get; set; }

    [ObservableProperty]
    public partial bool TwoFactorSetupVisible { get; set; }

    [ObservableProperty]
    public partial string TwoFactorDisableCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TwoFactorVerifyCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeRemoveTotpCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoginEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoginPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LoginMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AdminSessionActive { get; set; }

    [ObservableProperty]
    public partial string SessionEmailDisplay { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginIp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginLocation { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginCoordinates { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginBrowser { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginOsVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginDeviceName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginTime { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LastLoginStatus { get; set; } = "Secure Session";

    [ObservableProperty]
    public partial string LastLoginStatusColor { get; set; } = "#2ABD8F"; // Green


    public ObservableCollection<LoginHistory> LoginHistoryItems { get; set; }
    public ObservableCollection<SessionUIWrapper> ActiveSessions { get; } = new();

    [ObservableProperty]
    public partial string CurrentSessionId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProfileDepartment { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageSource? AvatarImageSource { get; set; }

    /// <summary>Initials fallback when no avatar photo is set.</summary>
    public string AvatarInitial =>
        string.IsNullOrWhiteSpace(ProfileName)
            ? (!string.IsNullOrWhiteSpace(SessionEmailDisplay) && SessionEmailDisplay != "—"
                ? SessionEmailDisplay.Trim()[0].ToString().ToUpperInvariant()
                : "?")
            : ProfileName.Trim()[0].ToString().ToUpperInvariant();

    /// <summary>True when a custom avatar photo has been loaded.</summary>
    public bool HasAvatarPhoto => AvatarImageSource is not null;

    [ObservableProperty]
    public partial string ChangeCurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangeNewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChangeConfirmPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ChangePasswordIsError { get; set; }

    [ObservableProperty]
    public partial string ChangePasswordMessage { get; set; } = string.Empty;

    partial void OnChangeNewPasswordChanged(string value)
    {
        OnPropertyChanged(nameof(PasswordStrengthScore));
        OnPropertyChanged(nameof(PasswordStrengthLabel));
        OnPropertyChanged(nameof(PasswordStrengthColor));
        OnPropertyChanged(nameof(PasswordStrengthBar1));
        OnPropertyChanged(nameof(PasswordStrengthBar2));
        OnPropertyChanged(nameof(PasswordStrengthBar3));
        OnPropertyChanged(nameof(PasswordStrengthBar4));
    }

    public int PasswordStrengthScore => GetPasswordStrengthScore(ChangeNewPassword);
    public string PasswordStrengthLabel => GetPasswordStrengthLabel(PasswordStrengthScore);
    public string PasswordStrengthColor => PasswordStrengthScore switch
    {
        1 => "#E05252",
        2 => "#E8A040",
        3 => "#4CAF50",
        4 => "#2ABD8F",
        _ => "Transparent",
    };
    public bool PasswordStrengthBar1 => PasswordStrengthScore >= 1;
    public bool PasswordStrengthBar2 => PasswordStrengthScore >= 2;
    public bool PasswordStrengthBar3 => PasswordStrengthScore >= 3;
    public bool PasswordStrengthBar4 => PasswordStrengthScore >= 4;

    [RelayCommand]
    private void GeneratePassword()
    {
        ChangeNewPassword = GenerateStrongPassword();
        ChangeConfirmPassword = ChangeNewPassword;
    }

    [ObservableProperty]
    public partial string ResetPasscodeTotp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasscodeNew { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasscodeConfirm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasscodeRecoveryMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasswordRecoveryEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasswordRecoveryTotp { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasswordRecoveryNew { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResetPasswordRecoveryConfirm { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordRecoveryMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool ShowTwoFactorKeyCopied { get; set; }

    [ObservableProperty]
    public partial bool ShowServerAccountEditor { get; set; }

    [ObservableProperty]
    public partial bool ShowTwoFactorEditor { get; set; }

    partial void OnProfileNameChanged(string value)
    {
        OnPropertyChanged(nameof(AvatarInitial));
    }

    partial void OnAvatarImageSourceChanged(ImageSource? value)
    {
        OnPropertyChanged(nameof(HasAvatarPhoto));
    }

    partial void OnTwoFactorEnabledChanged(bool value)    {
        OnPropertyChanged(nameof(IsStartTwoFactorEnabled));
        OnPropertyChanged(nameof(TwoFactorStatusText));
        OnPropertyChanged(nameof(TwoFactorStatusDetail));
        OnPropertyChanged(nameof(ShowPasscodeRemoveTotpField));
        OnPropertyChanged(nameof(ShowTwoFactorOffPanel));
        OnPropertyChanged(nameof(ShowTwoFactorEnabledPanel));
        OnPropertyChanged(nameof(TwoFactorPrimaryActionText));
    }

    partial void OnPasscodeIsConfiguredChanged(bool value)
    {
        OnPropertyChanged(nameof(PasscodeStatusText));
        OnPropertyChanged(nameof(PasscodePrimaryActionText));
        OnPropertyChanged(nameof(ShowPasscodeCurrentField));
        OnPropertyChanged(nameof(ShowPasscodeRemoveAction));
        OnPropertyChanged(nameof(ShowPasscodeRemoveTotpField));
    }

    partial void OnTwoFactorSetupVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStartTwoFactorEnabled));
        OnPropertyChanged(nameof(ShowTwoFactorOffPanel));
        OnPropertyChanged(nameof(ShowTwoFactorEnabledPanel));
        OnPropertyChanged(nameof(ShowTwoFactorSetupPanel));
        OnPropertyChanged(nameof(TwoFactorPrimaryActionText));
    }

    partial void OnAdminSessionActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(AdminSessionStatusText));
        OnPropertyChanged(nameof(ShowServerLoginPanel));
        OnPropertyChanged(nameof(ShowServerManagePanel));
        OnPropertyChanged(nameof(ServerAccountPrimaryActionText));
    }

    partial void OnSessionEmailDisplayChanged(string value)
    {
        OnPropertyChanged(nameof(AdminSessionStatusText));
    }

    public bool IsStartTwoFactorEnabled => !TwoFactorEnabled && !TwoFactorSetupVisible;

    public string TwoFactorStatusText => TwoFactorEnabled ? "On" : "Off";

    public string PasscodeStatusText => PasscodeIsConfigured ? "Configured" : "Not configured";

    public string TwoFactorStatusDetail => TwoFactorEnabled ? "Enabled on this device" : "Not enabled on this device";

    public string AdminSessionStatusText => AdminSessionActive
        ? $"Signed in: {SessionEmailDisplay}"
        : "Not signed in to server";

    public string PasscodePrimaryActionText => PasscodeIsConfigured ? "Change app lock" : "Set app lock";

    public bool ShowPasscodeCurrentField => PasscodeIsConfigured;

    public bool ShowPasscodeRemoveAction => PasscodeIsConfigured;

    public bool ShowPasscodeRemoveTotpField => PasscodeIsConfigured && TwoFactorEnabled;

    public bool ShowTwoFactorOffPanel => !TwoFactorEnabled && !TwoFactorSetupVisible;

    public bool ShowTwoFactorEnabledPanel => TwoFactorEnabled && !TwoFactorSetupVisible;

    public bool ShowTwoFactorSetupPanel => TwoFactorSetupVisible;

    public string TwoFactorPrimaryActionText => TwoFactorEnabled || TwoFactorSetupVisible ? "Manage 2FA" : "Enable 2FA";

    public bool ShowServerLoginPanel => !AdminSessionActive;

    public bool ShowServerManagePanel => AdminSessionActive;

    public string ServerAccountPrimaryActionText => AdminSessionActive ? "Manage server account" : "Sign in to server";

    private void RefreshAllState()
    {
        _security.Load();
        if (!_session.HasSession)
        {
            _session.Load();
        }
        _profile.Load();
        PasscodeIsConfigured = _security.PasscodeIsSet;
        AppLockEnabled = _security.AppLockEnabled;
        ShowAppLockEditor = false;
        TwoFactorEnabled = _security.TwoFactorEnabled;
        TwoFactorSetupVisible = false;
        ShowTwoFactorEditor = false;
        _pendingTwoFactorSecret = null;
        TwoFactorSecretDisplay = string.Empty;
        TwoFactorQrImage = null;
        TwoFactorConfirmCode = string.Empty;
        TwoFactorDisableCode = string.Empty;
        AdminSessionActive = _session.HasSession;
        ShowServerAccountEditor = false;

        // Show email: prefer session email, fallback to setup-state adminEmail
        var emailToShow = _session.Email;
        if (string.IsNullOrWhiteSpace(emailToShow))
        {
            // Try reading adminEmail from setup-state.json directly
            emailToShow = TryReadAdminEmailFromSetup();
        }
        SessionEmailDisplay = string.IsNullOrWhiteSpace(emailToShow) ? "—" : emailToShow;
        ProfileName = _profile.DisplayName ?? string.Empty;
        ProfileDepartment = _profile.Department ?? string.Empty;
        
        // Load persisted session asynchronously (best effort)
        _ = LoadLoginSessionAsync();

        _ = LoadAvatarAsync(_profile.AvatarPath);
        
        // Fetch latest profile data from server if authenticated to populate geolocation
        if (AdminSessionActive && !string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            _ = Task.Run(async () =>
            {
                (bool ok, string? err, AdminAuthApiClient.LoginUserDto? user) = await AdminAuthApiClient.GetProfileAsync(_session.AccessToken);
                if (ok && user != null)
                {
                    _profile.SaveProfile(
                        user.Email,
                        _profile.Department,
                        user.LastLoginIp,
                        user.LastLoginLat,
                        user.LastLoginLon,
                        user.LastLoginLocation);
                        
                    // Update UI on main thread
                    App.MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[Profile] Server-side profile refresh for: {user.Email}");
                        
                        if (!string.IsNullOrWhiteSpace(user.LastLoginIp)) LastLoginIp = user.LastLoginIp;
                        if (!string.IsNullOrWhiteSpace(user.LastLoginLocation)) LastLoginLocation = user.LastLoginLocation;
                        
                        if (user.LastLoginLat != 0 || user.LastLoginLon != 0)
                        {
                            LastLoginCoordinates = $"{user.LastLoginLat:F4}, {user.LastLoginLon:F4}";
                        }

                        if (user.LastSession != null)
                        {
                            LastLoginBrowser = user.LastSession.Browser ?? "PartFinder App";
                            LastLoginOsVersion = user.LastSession.OperatingSystem ?? LastLoginOsVersion;
                            LastLoginDeviceName = user.LastSession.DeviceType ?? LastLoginDeviceName;
                            LastLoginTime = user.LastSession.LoginTime.ToLocalTime().ToString("dd-MMM-yyyy hh:mm tt");
                            
                            LastLoginStatus = user.LastSession.Status == "suspicious" ? "Suspicious Login" : 
                                             user.LastSession.Status == "warning" ? "New Device" : "Secure Session";
                            LastLoginStatusColor = user.LastSession.Status == "suspicious" ? "#E05252" : 
                                                   user.LastSession.Status == "warning" ? "#E8A040" : "#2ABD8F";
                        }
                    });
                }
            });

            // Fetch History
            _ = Task.Run(async () =>
            {
                var (ok, _, history) = await AdminAuthApiClient.GetLoginHistoryAsync(_session.AccessToken);
                if (ok && history != null)
                {
                    App.MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        LoginHistoryItems.Clear();
                        foreach (var s in history)
                        {
                            LoginHistoryItems.Add(new LoginHistory
                            {
                                Time = s.LoginTime.ToLocalTime().ToString("dd-MMM-yyyy hh:mm tt"),
                                Details = $"{s.Browser ?? "Unknown"} on {s.OperatingSystem ?? "Unknown"} | {s.City ?? "Unknown"}",
                                IP = s.IpAddress ?? "Unknown",
                                Status = s.Status ?? "SUCCESS"
                            });
                        }
                        OnPropertyChanged(nameof(HasLoginHistory));
                    });
                }
            });

            // Fetch Active Sessions
            _ = RefreshSessionsAsync();
        }

        OnPropertyChanged(nameof(IsStartTwoFactorEnabled));
        OnPropertyChanged(nameof(TwoFactorStatusText));
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        RefreshAllState();
        PasscodeMessage = string.Empty;
        TwoFactorMessage = string.Empty;
        ProfileMessage = string.Empty;
        LoginMessage = string.Empty;
        ChangePasswordMessage = string.Empty;
    }

    [RelayCommand]
    private async Task RefreshSessionsAsync()
    {
        if (!AdminSessionActive || string.IsNullOrWhiteSpace(_session.AccessToken)) return;
        var (ok, _, sessions) = await AdminAuthApiClient.GetActiveSessionsAsync(_session.AccessToken);
        if (ok && sessions != null)
        {
            // Try to find current session if not set
            if (string.IsNullOrEmpty(CurrentSessionId))
            {
                // Simple heuristic: latest session from this IP/OS
                var current = sessions.OrderByDescending(s => s.LoginTime).FirstOrDefault();
                if (current != null) CurrentSessionId = current.Id ?? string.Empty;
            }

            App.MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
            {
                ActiveSessions.Clear();
                foreach (var s in sessions) 
                    ActiveSessions.Add(new SessionUIWrapper(s, CurrentSessionId));
            });
        }
    }

    [RelayCommand]
    private async Task LogoutSessionAsync(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(_session.AccessToken)) return;
        var (ok, _) = await AdminAuthApiClient.LogoutSessionAsync(_session.AccessToken, sessionId);
        if (ok)
        {
            _activityLogger.LogUserAction("Session Revoked", $"Logged out device session: {sessionId}");
            await RefreshSessionsAsync();
        }
    }

    [RelayCommand]
    private async Task LogoutAllSessionsAsync()
    {
        if (string.IsNullOrWhiteSpace(_session.AccessToken)) return;
        var (ok, _) = await AdminAuthApiClient.LogoutAllSessionsAsync(_session.AccessToken);
        if (ok)
        {
            _activityLogger.LogUserAction("All Sessions Revoked", "Logged out all other device sessions");
            await RefreshSessionsAsync();
        }
    }

    [RelayCommand]
    private void SaveProfile()
    {
        ProfileMessage = string.Empty;
        var trimmed = ProfileName.Trim();
        if (trimmed.Length > 80)
        {
            ProfileMessage = "Profile name must be 80 characters or fewer.";
            return;
        }

        _profile.SaveProfile(trimmed, ProfileDepartment.Trim());
        ProfileName = _profile.DisplayName ?? string.Empty;
        ProfileDepartment = _profile.Department ?? string.Empty;
        ProfileMessage = string.IsNullOrWhiteSpace(ProfileName)
            ? "Profile name cleared. Email will be shown."
            : "Profile name updated.";
        _activityLogger.LogUserAction("Profile Updated", $"Display name set to \"{ProfileName}\"");
    }

    [RelayCommand]
    private async Task UploadAvatarAsync(StorageFile? file)
    {
        if (file is null) return;
        var savedPath = _profile.SaveAvatar(file.Path);
        if (savedPath is not null)
        {
            await LoadAvatarAsync(savedPath).ConfigureAwait(true);
            _activityLogger.LogUserAction("Avatar Updated", "Profile photo was changed");
        }
    }

    private async Task LoadAvatarAsync(string? path)
    {
        // If user has a custom saved photo, load that
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using var stream = await file.OpenReadAsync();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                AvatarImageSource = bitmap;
                return;
            }
            catch { /* fall through to default */ }
        }

        // No custom photo — load default Assets/profile.jpg via stream so it's fully loaded before binding
        try
        {
            var defaultFile = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/profile.jpg"));
            using var defaultStream = await defaultFile.OpenReadAsync();
            var defaultBitmap = new BitmapImage();
            await defaultBitmap.SetSourceAsync(defaultStream);
            AvatarImageSource = defaultBitmap;
        }
        catch
        {
            AvatarImageSource = null;
        }
    }

    [RelayCommand]
    private void SavePasscode()
    {
        PasscodeMessage = string.Empty;
        if (!IsSixDigits(PasscodeNew) || !IsSixDigits(PasscodeConfirm))
        {
            PasscodeMessage = "Passcode must be exactly 6 digits.";
            return;
        }

        if (PasscodeNew != PasscodeConfirm)
        {
            PasscodeMessage = "New passcode and confirmation do not match.";
            return;
        }

        if (PasscodeIsConfigured)
        {
            if (!IsSixDigits(PasscodeCurrent))
            {
                PasscodeMessage = "Enter your current 6-digit passcode.";
                return;
            }

            if (!_security.VerifyPasscode(PasscodeCurrent))
            {
                PasscodeMessage = "Current passcode is incorrect.";
                return;
            }
        }

        _security.SavePasscode(PasscodeNew);
        PasscodeCurrent = string.Empty;
        PasscodeNew = string.Empty;
        PasscodeConfirm = string.Empty;
        PasscodeMessage = "Passcode saved.";
        PasscodeIsConfigured = true;
        ShowAppLockEditor = false;
        _activityLogger.LogUserAction("Passcode Changed", "App lock passcode was set or changed");
    }

    [RelayCommand]
    private void ClearPasscode()
    {
        PasscodeMessage = string.Empty;
        if (!PasscodeIsConfigured)
        {
            PasscodeMessage = "No passcode is set.";
            return;
        }

        if (TwoFactorEnabled)
        {
            if (!IsSixDigits(PasscodeRemoveTotpCode))
            {
                PasscodeMessage = "Enter the 6-digit authenticator code to remove the passcode.";
                return;
            }

            var secret = _security.GetTwoFactorSecret();
            if (secret is null || !TotpHelper.Verify(PasscodeRemoveTotpCode, secret))
            {
                PasscodeMessage = "Authenticator code is not valid.";
                return;
            }
        }
        else
        {
            if (!IsSixDigits(PasscodeCurrent))
            {
                PasscodeMessage = "Enter your current passcode to remove it.";
                return;
            }

            if (!_security.VerifyPasscode(PasscodeCurrent))
            {
                PasscodeMessage = "Current passcode is incorrect.";
                return;
            }
        }

        _security.ClearPasscode();
        PasscodeCurrent = string.Empty;
        PasscodeRemoveTotpCode = string.Empty;
        TwoFactorDisableCode = string.Empty;
        PasscodeMessage = "Passcode removed.";
        PasscodeIsConfigured = false;
        ShowAppLockEditor = false;
    }

    [RelayCommand]
    private async Task StartTwoFactorSetupAsync()
    {
        TwoFactorMessage = string.Empty;
        if (TwoFactorEnabled)
        {
            return;
        }

        _pendingTwoFactorSecret = TotpHelper.GenerateRandomSecretBase32();
        TwoFactorSecretDisplay = _pendingTwoFactorSecret;
        TwoFactorConfirmCode = string.Empty;
        TwoFactorSetupVisible = true;
        TwoFactorMessage =
            "Scan the QR code with Microsoft Authenticator or Google Authenticator, or enter the secret manually. Then type the 6-digit code to confirm.";

        await RefreshTwoFactorQrAsync(_pendingTwoFactorSecret).ConfigureAwait(true);
        if (TwoFactorQrImage is null)
        {
            TwoFactorMessage =
                "Could not render QR image on this device. Use the setup key shown below in your authenticator app.";
        }
    }

    [RelayCommand]
    private void CancelTwoFactorSetup()
    {
        TwoFactorSetupVisible = false;
        ShowTwoFactorEditor = false;
        _pendingTwoFactorSecret = null;
        TwoFactorSecretDisplay = string.Empty;
        TwoFactorQrImage = null;
        TwoFactorConfirmCode = string.Empty;
        TwoFactorMessage = string.Empty;
    }

    [RelayCommand]
    private async Task ConfirmTwoFactorSetupAsync(CancellationToken ct)
    {
        TwoFactorMessage = string.Empty;
        if (_pendingTwoFactorSecret is null)
        {
            TwoFactorMessage = "Start setup again.";
            return;
        }

        if (!IsSixDigits(TwoFactorConfirmCode))
        {
            TwoFactorMessage = "Enter the 6-digit code from your authenticator app.";
            return;
        }

        if (!TotpHelper.Verify(TwoFactorConfirmCode, _pendingTwoFactorSecret))
        {
            TwoFactorMessage = "That code does not match. Check the time on your device and try again.";
            return;
        }

        _security.SetTwoFactor(_pendingTwoFactorSecret, true);
        TwoFactorSetupVisible = false;
        var syncedSecret = _pendingTwoFactorSecret;
        _pendingTwoFactorSecret = null;
        TwoFactorSecretDisplay = string.Empty;
        TwoFactorQrImage = null;
        TwoFactorConfirmCode = string.Empty;
        TwoFactorEnabled = true;
        TwoFactorMessage =
            "Two-factor authentication is on. Use your authenticator app when resetting password or passcode.";
        _activityLogger.LogUserAction("2FA Enabled", "Two-factor authentication was enabled");

        _session.Load();
        if (_session.HasSession && _session.AccessToken is not null && syncedSecret is not null)
        {
            var (syncOk, syncErr) =
                await AdminAuthApiClient.SyncTotpSecretAsync(_session.AccessToken, syncedSecret, ct)
                    .ConfigureAwait(true);
            if (!syncOk)
            {
                TwoFactorMessage +=
                    " Server sync failed (needed for forgot password on the server): " + (syncErr ?? "unknown error");
            }
            else
            {
                TwoFactorMessage += " Secret synced to the server for forgot-password recovery.";
            }
        }
        else
        {
            TwoFactorMessage +=
                " Sign in below, then tap \"Sync 2FA to server\" so forgot-password recovery works.";
        }
    }

    [RelayCommand]
    private async Task SyncServerTotpAsync(CancellationToken ct)
    {
        TwoFactorMessage = string.Empty;
        _session.Load();
        if (!_session.HasSession || _session.AccessToken is null)
        {
            TwoFactorMessage = "Sign in first.";
            return;
        }

        _security.Load();
        if (!_security.TwoFactorEnabled)
        {
            TwoFactorMessage = "Enable 2FA locally first.";
            return;
        }

        var secret = _security.GetTwoFactorSecret();
        if (string.IsNullOrWhiteSpace(secret))
        {
            TwoFactorMessage = "No local authenticator secret found.";
            return;
        }

        var (ok, err) = await AdminAuthApiClient.SyncTotpSecretAsync(_session.AccessToken, secret, ct)
            .ConfigureAwait(true);
        TwoFactorMessage = ok
            ? "Authenticator secret synced to the server."
            : (err ?? "Sync failed.");
    }

    [RelayCommand]
    private async Task DisableTwoFactorAsync(CancellationToken ct)
    {
        TwoFactorMessage = string.Empty;
        if (!TwoFactorEnabled)
        {
            return;
        }

        if (!IsSixDigits(TwoFactorDisableCode))
        {
            TwoFactorMessage = "Enter a 6-digit code from your authenticator app to turn off 2FA.";
            return;
        }

        var secret = _security.GetTwoFactorSecret();
        if (secret is null || !TotpHelper.Verify(TwoFactorDisableCode, secret))
        {
            TwoFactorMessage = "Authenticator code is not valid.";
            return;
        }

        _security.DisableTwoFactor();
        TwoFactorDisableCode = string.Empty;
        TwoFactorVerifyCode = string.Empty;
        TwoFactorEnabled = false;
        ShowTwoFactorEditor = false;
        TwoFactorMessage = "Two-factor authentication is off.";
        _activityLogger.LogUserAction("2FA Disabled", "Two-factor authentication was disabled");

        _session.Load();
        if (_session.HasSession && _session.AccessToken is not null)
        {
            var (ok, err) = await AdminAuthApiClient.ClearTotpOnServerAsync(_session.AccessToken, ct)
                .ConfigureAwait(true);
            if (!ok)
            {
                TwoFactorMessage += " Could not clear server copy: " + (err ?? "unknown");
            }
        }
    }

    [RelayCommand]
    private void VerifyTwoFactorCode()
    {
        TwoFactorMessage = string.Empty;
        if (!TwoFactorEnabled)
        {
            TwoFactorMessage = "Enable 2FA first.";
            return;
        }

        if (!IsSixDigits(TwoFactorVerifyCode))
        {
            TwoFactorMessage = "Enter a valid 6-digit code to verify.";
            return;
        }

        var secret = _security.GetTwoFactorSecret();
        if (secret is null)
        {
            TwoFactorMessage = "No local authenticator secret found.";
            return;
        }

        TwoFactorMessage = TotpHelper.Verify(TwoFactorVerifyCode, secret)
            ? "Authenticator code verified successfully."
            : "Code did not match. Check device time and try again.";
    }

    [RelayCommand]
    private async Task LoginAsync(CancellationToken ct)
    {
        LoginMessage = "Authenticating...";
        
        if (string.IsNullOrWhiteSpace(LoginEmail) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginMessage = "Email and password are required.";
            return;
        }

        // 1. Fetch metadata for the login request
        var metadata = await _metadataService.GetMetadataAsync();

        // 2. Perform Authentication
        (bool ok, string? err, AdminAuthApiClient.LoginResponseBody? body) = await AdminAuthApiClient.LoginAsync(
            LoginEmail.Trim(), 
            LoginPassword, 
            metadata.IpAddress,
            metadata.Latitude,
            metadata.Longitude,
            metadata.DeviceName,
            metadata.OsVersion,
            ct: ct).ConfigureAwait(true);

        if (!ok || body?.AccessToken is null)
        {
            LoginMessage = err ?? "Login failed.";
            return;
        }

        // 3. Save Session
        var email = body.User?.Email ?? LoginEmail.Trim();
        _session.Save(body.AccessToken, email);
        AdminSessionActive = true;
        SessionEmailDisplay = email;
        LoginPassword = string.Empty;
        LoginMessage = "Signed in successfully.";

        // 4. MANDATORY: Load and assign current session details to UI immediately
        System.Diagnostics.Debug.WriteLine("[LoginSession] Calling LoadCurrentLoginSession...");
        await LoadCurrentLoginSession();
        System.Diagnostics.Debug.WriteLine("[LoginSession] LoadCurrentLoginSession completed.");

        _activityLogger.LogLogin(email);
        RefreshStatus();
    }

    public async Task LoadCurrentLoginSession()
    {
        System.Diagnostics.Debug.WriteLine("[LoginSession] Starting complete data assignment flow...");

        // 1. Fetch Public IP
        string ip = await GetPublicIPAsync();
        LastLoginIp = string.IsNullOrEmpty(ip) || ip == "Not Available" ? "Not Available" : ip;
        System.Diagnostics.Debug.WriteLine($"[LoginSession] IP: {LastLoginIp}");

        // 2. Fetch Geolocation & Coordinates
        var (lat, lon, status) = await GetLocationAsync();
        if (lat != 0 || lon != 0)
        {
            LastLoginCoordinates = $"{lat:F4}, {lon:F4}";
            LastLoginLocation = status == "Success" ? $"Lat: {lat:F4}, Lon: {lon:F4}" : status;
        }
        else
        {
            LastLoginCoordinates = "Not Available";
            LastLoginLocation = string.IsNullOrEmpty(status) || status == "Not Captured" ? "Not Available" : status;
        }

        // STEP 3 — Add Debugging
        System.Diagnostics.Debug.WriteLine($"[LoginSession] DEBUG => IP: {LastLoginIp}");
        System.Diagnostics.Debug.WriteLine($"[LoginSession] DEBUG => Location: {LastLoginLocation}");
        System.Diagnostics.Debug.WriteLine($"[LoginSession] DEBUG => Coordinates: {LastLoginCoordinates}");
        System.Diagnostics.Debug.WriteLine($"[LoginSession] DEBUG => Device: {LastLoginDeviceName}");

        // 3. Fetch Device Information
        LastLoginDeviceName = Environment.MachineName;
        LastLoginOsVersion = Environment.OSVersion.ToString();
        LastLoginBrowser = "WinUI 3 Desktop App";
        LastLoginTime = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt");
        
        System.Diagnostics.Debug.WriteLine($"[LoginSession] Device: {LastLoginDeviceName}, OS: {LastLoginOsVersion}, Time: {LastLoginTime}");

        // 4. Update fallback specific fields
        LastLoginStatus = "Secure Session";
        LastLoginStatusColor = "#2ABD8F";

        // 5. Persist the current session
        _ = _sessionPersistence.SaveLoginSessionAsync(new LoginSession
        {
            IpAddress = LastLoginIp,
            Location = LastLoginLocation,
            Coordinates = LastLoginCoordinates,
            Browser = LastLoginBrowser,
            OsVersion = LastLoginOsVersion,
            DeviceName = LastLoginDeviceName,
            LoginTime = LastLoginTime
        });

        System.Diagnostics.Debug.WriteLine($"[LoginSession] Session persisted to storage.");

        // STEP 7 — Force Property Notifications
        OnPropertyChanged(nameof(LastLoginIp));
        OnPropertyChanged(nameof(LastLoginLocation));
        OnPropertyChanged(nameof(LastLoginCoordinates));
        OnPropertyChanged(nameof(LastLoginBrowser));
        OnPropertyChanged(nameof(LastLoginOsVersion));
        OnPropertyChanged(nameof(LastLoginDeviceName));
        OnPropertyChanged(nameof(LastLoginTime));
        OnPropertyChanged(nameof(LastLoginStatus));
        OnPropertyChanged(nameof(LastLoginStatusColor));

        // 6. Save session permanently using LocalSettings
        await _sessionPersistence.SaveLoginSessionAsync(new LoginSession
        {
            IpAddress = LastLoginIp,
            Location = LastLoginLocation,
            Coordinates = LastLoginCoordinates,
            Browser = LastLoginBrowser,
            OsVersion = LastLoginOsVersion,
            DeviceName = LastLoginDeviceName,
            LoginTime = LastLoginTime
        });
        
        System.Diagnostics.Debug.WriteLine("[LoginSession] UI properties assigned and notified.");

        // Create a LoginHistory item for this successful session
        var historyItem = new LoginHistory
        {
            Time = DateTime.Now.ToString("dd-MMM-yyyy hh:mm tt"),
            Details = $"{Environment.MachineName} | Windows Desktop",
            IP = LastLoginIp,
            Status = "SUCCESS"
        };
        
        App.MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
        {
            LoginHistoryItems.Insert(0, historyItem);
            OnPropertyChanged(nameof(LoginHistoryItems));
            OnPropertyChanged(nameof(HasLoginHistory));
            _ = SaveLoginHistoryAsync();
        });
    }

    public bool HasLoginHistory => LoginHistoryItems.Count > 0;

    public async Task SaveLoginHistoryAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(LoginHistoryItems);
            ApplicationData.Current.LocalSettings.Values["LoginHistoryAudit"] = json;
            System.Diagnostics.Debug.WriteLine($"[LoginSession] History saved. Count: {LoginHistoryItems.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginSession] Failed to save history: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    public async Task LoadLoginHistoryAsync()
    {
        try
        {
            object data = ApplicationData.Current.LocalSettings.Values["LoginHistoryAudit"];
            if (data != null && !string.IsNullOrWhiteSpace(data.ToString()))
            {
                var history = JsonSerializer.Deserialize<List<LoginHistory>>(data.ToString()!);
                if (history != null)
                {
                    App.MainAppWindow?.DispatcherQueue.TryEnqueue(() =>
                    {
                        LoginHistoryItems.Clear();
                        foreach (var item in history) LoginHistoryItems.Add(item);
                        System.Diagnostics.Debug.WriteLine($"[LoginSession] Loaded history count: {LoginHistoryItems.Count}");
                        OnPropertyChanged(nameof(HasLoginHistory));
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginSession] Failed to load history: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    public async Task LoadLoginSessionAsync()
    {
        var session = _sessionPersistence.LoadLoginSession();
        if (session != null)
        {
            LastLoginIp = session.IpAddress;
            LastLoginLocation = session.Location;
            LastLoginCoordinates = session.Coordinates;
            LastLoginBrowser = session.Browser;
            LastLoginOsVersion = session.OsVersion;
            LastLoginDeviceName = session.DeviceName;
            LastLoginTime = session.LoginTime;
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded IP: {LastLoginIp}");
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded Device: {LastLoginDeviceName}");
        }
        else
        {
            // Only set fallbacks if current values are empty
            if (string.IsNullOrEmpty(LastLoginIp))
            {
                LastLoginIp = "Not Available";
                LastLoginLocation = "Not Available";
                LastLoginCoordinates = "Not Available";
                LastLoginBrowser = "Not Available";
                LastLoginOsVersion = "Not Available";
                LastLoginDeviceName = "Not Available";
                LastLoginTime = "Not Available";
            }
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] No persisted login session found.");
        }
        await Task.CompletedTask;
    }

    public async Task<string> GetPublicIPAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LoginSession] Fetching Public IP from api.ipify.org...");
            using HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var ip = await client.GetStringAsync("https://api.ipify.org").ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(ip) ? "Not Available" : ip.Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginSession] IP fetch failed: {ex.Message}");
            return "Not Available";
        }
    }

    private async Task<(double lat, double lon, string status)> GetLocationAsync()
    {
        try
        {
            var accessStatus = await Windows.Devices.Geolocation.Geolocator.RequestAccessAsync();
            if (accessStatus == Windows.Devices.Geolocation.GeolocationAccessStatus.Allowed)
            {
                var geolocator = new Windows.Devices.Geolocation.Geolocator { DesiredAccuracyInMeters = 50 };
                var pos = await geolocator.GetGeopositionAsync().AsTask().ConfigureAwait(false);
                return (pos.Coordinate.Point.Position.Latitude, pos.Coordinate.Point.Position.Longitude, "Success");
            }
            return (0, 0, "Location Access Denied");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginSession] Location fetch failed: {ex.Message}");
            return (0, 0, "Location Error");
        }
    }



    [RelayCommand]
    private async Task LogoutAsync()
    {
        _activityLogger.LogLogout(SessionEmailDisplay != "—" ? SessionEmailDisplay : "unknown");
        await _activityLogger.FlushAsync().ConfigureAwait(true);
        _session.Clear();
        // Do NOT delete setup-state.json — it holds orgCode/dbUri needed after re-login
        AdminSessionActive = false;
        SessionEmailDisplay = "—";
        LoginMessage = "Signed out.";
        ChangePasswordMessage = string.Empty;
        ShowServerAccountEditor = false;

        if (Application.Current is App && App.MainAppWindow is MainWindow main)
        {
            main.ResetToSetup();
        }
        else
        {
            Application.Current.Exit();
        }
    }

    [RelayCommand]
    private async Task ChangePasswordAsync(CancellationToken ct)
    {
        ChangePasswordMessage = string.Empty;
        ChangePasswordIsError = false;

        if (string.IsNullOrWhiteSpace(ChangeCurrentPassword) ||
            string.IsNullOrWhiteSpace(ChangeNewPassword) ||
            string.IsNullOrWhiteSpace(ChangeConfirmPassword))
        {
            ChangePasswordIsError = true;
            ChangePasswordMessage = "Fill in current password, new password, and confirmation.";
            return;
        }

        if (ChangeNewPassword != ChangeConfirmPassword)
        {
            ChangePasswordIsError = true;
            ChangePasswordMessage = "New password and confirmation do not match.";
            return;
        }

        if (!IsStrongPassword(ChangeNewPassword))
        {
            ChangePasswordIsError = true;
            ChangePasswordMessage = "New password must be at least 8 characters with upper, lower, and a number.";
            return;
        }

        var (ok, err) = await AdminAuthApiClient.ChangePasswordAsync(
                _session.AccessToken ?? string.Empty,
                ChangeCurrentPassword,
                ChangeNewPassword,
                ct: ct)
            .ConfigureAwait(true);

        if (!ok)
        {
            ChangePasswordIsError = true;
            ChangePasswordMessage = err ?? "Could not change password.";
            return;
        }

        ChangeCurrentPassword = string.Empty;
        ChangeNewPassword = string.Empty;
        ChangeConfirmPassword = string.Empty;
        ChangePasswordIsError = false;
        ChangePasswordMessage = "Password updated. If 2FA is enabled, use your authenticator app for any reset flows.";
        _activityLogger.LogUserAction("Password Changed", "Account password was changed");
    }

    /// <summary>
    /// Verifies a 6-digit authenticator code for future password-reset or passcode-reset flows.
    /// </summary>
    public bool VerifyTwoFactorForRecovery(string sixDigitCode)
    {
        _security.Load();
        if (!_security.TwoFactorEnabled)
        {
            return false;
        }

        var secret = _security.GetTwoFactorSecret();
        return secret is not null && TotpHelper.Verify(sixDigitCode, secret);
    }

    [RelayCommand]
    private void ResetPasscodeWithTwoFactor()
    {
        PasscodeRecoveryMessage = string.Empty;
        _security.Load();
        if (!_security.TwoFactorEnabled)
        {
            PasscodeRecoveryMessage = "Turn on 2FA first, then use this if you forgot your passcode.";
            return;
        }

        if (!IsSixDigits(ResetPasscodeTotp))
        {
            PasscodeRecoveryMessage = "Enter the 6-digit code from your authenticator app.";
            return;
        }

        if (!VerifyTwoFactorForRecovery(ResetPasscodeTotp))
        {
            PasscodeRecoveryMessage = "That code does not match your authenticator.";
            return;
        }

        if (!IsSixDigits(ResetPasscodeNew) || !IsSixDigits(ResetPasscodeConfirm))
        {
            PasscodeRecoveryMessage = "New passcode and confirmation must each be exactly 6 digits.";
            return;
        }

        if (ResetPasscodeNew != ResetPasscodeConfirm)
        {
            PasscodeRecoveryMessage = "New passcode and confirmation do not match.";
            return;
        }

        _security.SavePasscode(ResetPasscodeNew);
        ResetPasscodeTotp = string.Empty;
        ResetPasscodeNew = string.Empty;
        ResetPasscodeConfirm = string.Empty;
        PasscodeIsConfigured = true;
        PasscodeRecoveryMessage = "Passcode was reset using your authenticator.";
    }

    [RelayCommand]
    private async Task ResetPasswordWithTwoFactorAsync(CancellationToken ct)
    {
        PasswordRecoveryMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(ResetPasswordRecoveryEmail))
        {
            PasswordRecoveryMessage = "Enter the admin email for this account.";
            return;
        }

        if (!IsSixDigits(ResetPasswordRecoveryTotp))
        {
            PasswordRecoveryMessage = "Enter the 6-digit code from your authenticator app.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ResetPasswordRecoveryNew) ||
            string.IsNullOrWhiteSpace(ResetPasswordRecoveryConfirm))
        {
            PasswordRecoveryMessage = "Enter and confirm the new password.";
            return;
        }

        if (ResetPasswordRecoveryNew != ResetPasswordRecoveryConfirm)
        {
            PasswordRecoveryMessage = "New password and confirmation do not match.";
            return;
        }

        if (!IsStrongPassword(ResetPasswordRecoveryNew))
        {
            PasswordRecoveryMessage = "New password must be at least 8 characters with upper, lower, and a number.";
            return;
        }

        var (ok, err) = await AdminAuthApiClient.ResetPasswordWithTotpAsync(
                ResetPasswordRecoveryEmail.Trim(),
                ResetPasswordRecoveryTotp,
                ResetPasswordRecoveryNew,
                ct: ct)
            .ConfigureAwait(true);
        if (!ok)
        {
            PasswordRecoveryMessage = err ?? "Recovery failed. Ensure 2FA was synced to the server while signed in.";
            return;
        }

        ResetPasswordRecoveryTotp = string.Empty;
        ResetPasswordRecoveryNew = string.Empty;
        ResetPasswordRecoveryConfirm = string.Empty;
        PasswordRecoveryMessage = "Server password was reset. You can sign in with the new password.";
    }

    private static bool IsSixDigits(string value) =>
        value.Length == 6 && value.All(char.IsDigit);

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

    // ── Password strength helpers ──────────────────────────────────────

    /// <summary>0–4 score: 0=empty, 1=weak, 2=fair, 3=strong, 4=very strong</summary>
    public static int GetPasswordStrengthScore(string password)
    {
        if (string.IsNullOrEmpty(password)) return 0;
        int score = 0;
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (password.Any(char.IsUpper) && password.Any(char.IsLower)) score++;
        if (password.Any(char.IsDigit)) score++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) score++;
        return Math.Min(score, 4);
    }

    public static string GetPasswordStrengthLabel(int score) => score switch
    {
        0 => string.Empty,
        1 => "Weak",
        2 => "Fair",
        3 => "Strong",
        _ => "Very Strong",
    };

    /// <summary>Generates a cryptographically random strong password.</summary>
    public static string GenerateStrongPassword(int length = 16)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*-_=+?";
        var all = upper + lower + digits + special;

        var bytes = new byte[length + 4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        var chars = new char[length];
        // Guarantee at least one of each category
        chars[0] = upper[bytes[0] % upper.Length];
        chars[1] = lower[bytes[1] % lower.Length];
        chars[2] = digits[bytes[2] % digits.Length];
        chars[3] = special[bytes[3] % special.Length];
        for (int i = 4; i < length; i++)
            chars[i] = all[bytes[i] % all.Length];

        // Shuffle
        var rng = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(rng);
        for (int i = length - 1; i > 0; i--)
        {
            int j = rng[i] % (i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private async Task RefreshTwoFactorQrAsync(string secretBase32)
    {
        if (string.IsNullOrWhiteSpace(secretBase32))
        {
            TwoFactorQrImage = null;
            return;
        }

        try
        {
            var account = ResolveAuthenticatorAccountLabel();
            var uri = TwoFactorQrCodeService.BuildTotpSetupUri("PartFinder", account, secretBase32);
            var png = TwoFactorQrCodeService.RenderPng(uri, pixelsPerModule: 6);
            TwoFactorQrImage = await BitmapFromPngBytesAsync(png).ConfigureAwait(true);
        }
        catch
        {
            TwoFactorQrImage = null;
        }
    }

    private string ResolveAuthenticatorAccountLabel()
    {
        if (!string.IsNullOrWhiteSpace(LoginEmail))
        {
            return LoginEmail.Trim();
        }

        if (!string.IsNullOrWhiteSpace(SessionEmailDisplay) && SessionEmailDisplay != "—")
        {
            return SessionEmailDisplay.Trim();
        }

        return "PartFinder";
    }

    private static async Task<BitmapImage?> BitmapFromPngBytesAsync(byte[] pngBytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var output = stream.GetOutputStreamAt(0))
        {
            await output.WriteAsync(pngBytes.AsBuffer());
            await output.FlushAsync();
        }

        stream.Seek(0);
        var image = new BitmapImage();
        await image.SetSourceAsync(stream);
        return image;
    }

    public async Task ShowTwoFactorKeyCopiedAsync()
    {        ShowTwoFactorKeyCopied = true;
        await Task.Delay(1500).ConfigureAwait(true);
        ShowTwoFactorKeyCopied = false;
    }

    private static string? TryReadAdminEmailFromSetup()
    {
        try
        {
            foreach (var path in SetupPaths.SetupStateCandidatePaths)
            {
                if (!File.Exists(path))
                    continue;

                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;

                if (root.TryGetProperty("adminEmail", out var ae) &&
                    ae.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var email = ae.GetString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(email))
                        return email;
                }
            }
        }
        catch { }

        return null;
    }
}
