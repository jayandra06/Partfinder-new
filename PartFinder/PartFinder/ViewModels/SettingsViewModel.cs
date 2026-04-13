using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PartFinder.Services;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using System.Threading;

namespace PartFinder.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly LocalUserSecurityStore _security;
    private readonly AdminSessionStore _session;
    private string? _pendingTwoFactorSecret;

    public SettingsViewModel(LocalUserSecurityStore security, AdminSessionStore session)
    {
        _security = security;
        _session = session;
        RefreshAllState();
    }

    [ObservableProperty]
    private string _passcodeCurrent = string.Empty;

    [ObservableProperty]
    private string _passcodeNew = string.Empty;

    [ObservableProperty]
    private string _passcodeConfirm = string.Empty;

    [ObservableProperty]
    private string _passcodeMessage = string.Empty;

    [ObservableProperty]
    private bool _passcodeIsConfigured;

    [ObservableProperty]
    private bool _showAppLockEditor;

    [ObservableProperty]
    private string _twoFactorSecretDisplay = string.Empty;

    [ObservableProperty]
    private ImageSource? _twoFactorQrImage;

    [ObservableProperty]
    private string _twoFactorConfirmCode = string.Empty;

    [ObservableProperty]
    private string _twoFactorMessage = string.Empty;

    [ObservableProperty]
    private bool _twoFactorEnabled;

    [ObservableProperty]
    private bool _twoFactorSetupVisible;

    [ObservableProperty]
    private string _twoFactorDisableCode = string.Empty;

    [ObservableProperty]
    private string _twoFactorVerifyCode = string.Empty;

    [ObservableProperty]
    private string _passcodeRemoveTotpCode = string.Empty;

    [ObservableProperty]
    private string _loginEmail = string.Empty;

    [ObservableProperty]
    private string _loginPassword = string.Empty;

    [ObservableProperty]
    private string _loginMessage = string.Empty;

    [ObservableProperty]
    private bool _adminSessionActive;

    [ObservableProperty]
    private string _sessionEmailDisplay = string.Empty;

    [ObservableProperty]
    private string _changeCurrentPassword = string.Empty;

    [ObservableProperty]
    private string _changeNewPassword = string.Empty;

    [ObservableProperty]
    private string _changeConfirmPassword = string.Empty;

    [ObservableProperty]
    private string _changePasswordMessage = string.Empty;

    [ObservableProperty]
    private string _resetPasscodeTotp = string.Empty;

    [ObservableProperty]
    private string _resetPasscodeNew = string.Empty;

    [ObservableProperty]
    private string _resetPasscodeConfirm = string.Empty;

    [ObservableProperty]
    private string _passcodeRecoveryMessage = string.Empty;

    [ObservableProperty]
    private string _resetPasswordRecoveryEmail = string.Empty;

    [ObservableProperty]
    private string _resetPasswordRecoveryTotp = string.Empty;

    [ObservableProperty]
    private string _resetPasswordRecoveryNew = string.Empty;

    [ObservableProperty]
    private string _resetPasswordRecoveryConfirm = string.Empty;

    [ObservableProperty]
    private string _passwordRecoveryMessage = string.Empty;

    [ObservableProperty]
    private bool _showTwoFactorKeyCopied;

    [ObservableProperty]
    private bool _showServerAccountEditor;

    [ObservableProperty]
    private bool _showTwoFactorEditor;

    partial void OnTwoFactorEnabledChanged(bool value)
    {
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
        _session.Load();
        PasscodeIsConfigured = _security.PasscodeIsSet;
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
        SessionEmailDisplay = string.IsNullOrWhiteSpace(_session.Email) ? "—" : _session.Email;
        OnPropertyChanged(nameof(IsStartTwoFactorEnabled));
        OnPropertyChanged(nameof(TwoFactorStatusText));
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        RefreshAllState();
        PasscodeMessage = string.Empty;
        TwoFactorMessage = string.Empty;
        LoginMessage = string.Empty;
        ChangePasswordMessage = string.Empty;
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
        LoginMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(LoginEmail) || string.IsNullOrWhiteSpace(LoginPassword))
        {
            LoginMessage = "Email and password are required.";
            return;
        }

        var (ok, err, body) = await AdminAuthApiClient.LoginAsync(LoginEmail.Trim(), LoginPassword, ct)
            .ConfigureAwait(true);
        if (!ok || body?.AccessToken is null)
        {
            LoginMessage = err ?? "Login failed.";
            return;
        }

        var email = body.User?.Email ?? LoginEmail.Trim();
        _session.Save(body.AccessToken, email);
        LoginPassword = string.Empty;
        LoginMessage = "Signed in. You can change your password below.";
        AdminSessionActive = true;
        SessionEmailDisplay = email;
    }

    [RelayCommand]
    private void Logout()
    {
        _session.Clear();
        AdminSessionActive = false;
        SessionEmailDisplay = "—";
        LoginMessage = "Signed out.";
        ChangePasswordMessage = string.Empty;
        ShowServerAccountEditor = false;
    }

    [RelayCommand]
    private async Task ChangePasswordAsync(CancellationToken ct)
    {
        ChangePasswordMessage = string.Empty;
        _session.Load();
        if (!_session.HasSession || _session.AccessToken is null)
        {
            ChangePasswordMessage = "Sign in with your admin email and password first.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ChangeCurrentPassword) ||
            string.IsNullOrWhiteSpace(ChangeNewPassword) ||
            string.IsNullOrWhiteSpace(ChangeConfirmPassword))
        {
            ChangePasswordMessage = "Fill in current password, new password, and confirmation.";
            return;
        }

        if (ChangeNewPassword != ChangeConfirmPassword)
        {
            ChangePasswordMessage = "New password and confirmation do not match.";
            return;
        }

        if (!IsStrongPassword(ChangeNewPassword))
        {
            ChangePasswordMessage = "New password must be at least 8 characters with upper, lower, and a number.";
            return;
        }

        var (ok, err) = await AdminAuthApiClient.ChangePasswordAsync(
                _session.AccessToken,
                ChangeCurrentPassword,
                ChangeNewPassword,
                ct)
            .ConfigureAwait(true);
        if (!ok)
        {
            ChangePasswordMessage = err ?? "Could not change password.";
            return;
        }

        ChangeCurrentPassword = string.Empty;
        ChangeNewPassword = string.Empty;
        ChangeConfirmPassword = string.Empty;
        ChangePasswordMessage = "Password updated. If 2FA is enabled, use your authenticator app for any reset flows.";
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
                ct)
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
    {
        ShowTwoFactorKeyCopied = true;
        await Task.Delay(1500).ConfigureAwait(true);
        ShowTwoFactorKeyCopied = false;
    }
}
