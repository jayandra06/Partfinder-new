using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using PartFinder.Services;
using PartFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PartFinder.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly ActivityLogger _activity = App.Services.GetRequiredService<ActivityLogger>();

    public SettingsPage()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
    }

    // ── Toast Popup ──────────────────────────────────────────
    private async void ShowToast(string title = "Saved successfully", string message = "Your changes have been applied.")
    {
        ToastTitle.Text = title;
        ToastMessage.Text = message;

        // Fade in
        var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(250) };
        var storyIn = new Storyboard();
        Storyboard.SetTarget(fadeIn, ToastPopup);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        storyIn.Children.Add(fadeIn);
        storyIn.Begin();

        await Task.Delay(2500);

        // Fade out
        var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(300) };
        var storyOut = new Storyboard();
        Storyboard.SetTarget(fadeOut, ToastPopup);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        storyOut.Children.Add(fadeOut);
        storyOut.Begin();
    }

    private void OnNavProfileClick(object sender, RoutedEventArgs e) =>
        ShowSection(profile: true, security: false, password: false, appLock: false);

    private void OnNavSecurityClick(object sender, RoutedEventArgs e) =>
        ShowSection(profile: false, security: true, password: false, appLock: false);

    private void OnNavPasswordClick(object sender, RoutedEventArgs e) =>
        ShowSection(profile: false, security: false, password: true, appLock: false);

    private void OnNavAppLockClick(object sender, RoutedEventArgs e) =>
        ShowSection(profile: false, security: false, password: false, appLock: true);

    private void ShowSection(bool profile, bool security, bool password, bool appLock)
    {
        // Sections
        ProfileSection.Visibility = profile ? Visibility.Visible : Visibility.Collapsed;
        SecuritySection.Visibility = security ? Visibility.Visible : Visibility.Collapsed;
        PasswordSection.Visibility = password ? Visibility.Visible : Visibility.Collapsed;
        AppLockSection.Visibility = appLock ? Visibility.Visible : Visibility.Collapsed;

        // Nav selection styling (UI only)
        SetNavSelected(NavProfileItem, profile);
        SetNavSelected(NavSecurityItem, security);
        SetNavSelected(NavTwoFactorItem, security);
        SetNavSelected(NavPasswordItem, password);
        SetNavSelected(NavAppLockItem, appLock);
    }

    private static void SetNavSelected(Border border, bool selected)
    {
        border.Background = selected
            ? (Brush)Application.Current.Resources["NavItemSelectedBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        border.BorderBrush = selected
            ? (Brush)Application.Current.Resources["AccentPrimaryBrush"]
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        border.BorderThickness = new Thickness(1);
    }

    private void OnPasscodeFieldChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // No-op: PIN-based passcode removed, App Lock now uses Windows Hello
    }

    private void OnSavePasscodeClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // No-op: PIN-based passcode removed, App Lock now uses Windows Hello
    }

    // ── Password page handlers ────────────────────────────────────────

    private void OnCurrentPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        vm.ChangeCurrentPassword = CurrentPasswordBox.Password;
        // Hide error when user starts retyping
        CurrentPasswordErrorBorder.Visibility = Visibility.Collapsed;
    }

    private void OnNewPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        vm.ChangeNewPassword = NewPasswordBox.Password;
        UpdateStrengthBar(vm);
    }

    private void OnConfirmPasswordChanged(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        vm.ChangeConfirmPassword = ConfirmPasswordBox.Password;
    }

    private void UpdateStrengthBar(SettingsViewModel vm)
    {
        var score = SettingsViewModel.GetPasswordStrengthScore(vm.ChangeNewPassword);
        var color = score switch
        {
            1 => Microsoft.UI.ColorHelper.FromArgb(255, 0xE0, 0x52, 0x52),
            2 => Microsoft.UI.ColorHelper.FromArgb(255, 0xE8, 0xA0, 0x40),
            3 => Microsoft.UI.ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50),
            4 => Microsoft.UI.ColorHelper.FromArgb(255, 0x2A, 0xBD, 0x8F),
            _ => Microsoft.UI.ColorHelper.FromArgb(255, 0x40, 0x40, 0x40),
        };
        var activeBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        var dimBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["BorderDefaultBrush"];

        StrengthBar1.Fill = score >= 1 ? activeBrush : dimBrush;
        StrengthBar2.Fill = score >= 2 ? activeBrush : dimBrush;
        StrengthBar3.Fill = score >= 3 ? activeBrush : dimBrush;
        StrengthBar4.Fill = score >= 4 ? activeBrush : dimBrush;

        StrengthLabel.Text = SettingsViewModel.GetPasswordStrengthLabel(score);
        StrengthLabel.Foreground = score > 0 ? activeBrush : dimBrush;
    }

    private void OnCopyGeneratedPasswordClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || string.IsNullOrWhiteSpace(vm.ChangeNewPassword)) return;
        var data = new Windows.ApplicationModel.DataTransfer.DataPackage();
        data.SetText(vm.ChangeNewPassword);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(data);
        ShowToast("Copied", "Generated password copied to clipboard.");
    }

    private void OnClearPasswordFieldsClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        CurrentPasswordBox.Password = string.Empty;
        NewPasswordBox.Password = string.Empty;
        ConfirmPasswordBox.Password = string.Empty;
        vm.ChangeCurrentPassword = string.Empty;
        vm.ChangeNewPassword = string.Empty;
        vm.ChangeConfirmPassword = string.Empty;
        vm.ChangePasswordMessage = string.Empty;
        CurrentPasswordErrorBorder.Visibility = Visibility.Collapsed;
        PasswordMessageBorder.Visibility = Visibility.Collapsed;
        UpdateStrengthBar(vm);
    }

    // Override ChangePasswordCommand result display
    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (DataContext is SettingsViewModel vm)
        {
            vm.PropertyChanged += OnSettingsVmPropertyChanged;
        }
    }

    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        if (DataContext is SettingsViewModel vm)
        {
            vm.PropertyChanged -= OnSettingsVmPropertyChanged;
        }
    }

    private void OnSettingsVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.ChangePasswordMessage)) return;
        if (DataContext is not SettingsViewModel vm) return;

        var msg = vm.ChangePasswordMessage;
        if (string.IsNullOrWhiteSpace(msg))
        {
            PasswordMessageBorder.Visibility = Visibility.Collapsed;
            CurrentPasswordErrorBorder.Visibility = Visibility.Collapsed;
            return;
        }

        // Detect "wrong current password" errors
        var isError = msg.Contains("incorrect", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("wrong", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("failed", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("mismatch", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("not match", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("required", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("must be", StringComparison.OrdinalIgnoreCase);

        // Show current password error inline if it's about the current password
        if (msg.Contains("current", StringComparison.OrdinalIgnoreCase) && isError)
        {
            CurrentPasswordErrorText.Text = msg;
            CurrentPasswordErrorBorder.Visibility = Visibility.Visible;
            PasswordMessageBorder.Visibility = Visibility.Collapsed;
            return;
        }

        CurrentPasswordErrorBorder.Visibility = Visibility.Collapsed;

        // Style the message border
        var errorColor = Microsoft.UI.ColorHelper.FromArgb(255, 0xE0, 0x52, 0x52);
        var successColor = Microsoft.UI.ColorHelper.FromArgb(255, 0x2A, 0xBD, 0x8F);
        var infoColor = (Microsoft.UI.ColorHelper.FromArgb(255, 0x1F, 0x7A, 0xE0));

        if (isError)
        {
            PasswordMessageBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(0x18, 0xE0, 0x52, 0x52));
            PasswordMessageBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(0x30, 0xE0, 0x52, 0x52));
            PasswordMessageIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorColor);
            PasswordMessageIcon.Glyph = "\uE783";
            PasswordMessageText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(errorColor);
        }
        else
        {
            PasswordMessageBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(0x18, 0x2A, 0xBD, 0x8F));
            PasswordMessageBorder.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(0x30, 0x2A, 0xBD, 0x8F));
            PasswordMessageIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(successColor);
            PasswordMessageIcon.Glyph = "\uE73E";
            PasswordMessageText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(successColor);
            // Clear fields on success
            CurrentPasswordBox.Password = string.Empty;
            NewPasswordBox.Password = string.Empty;
            ConfirmPasswordBox.Password = string.Empty;
            if (DataContext is SettingsViewModel v)
            {
                v.ChangeCurrentPassword = string.Empty;
                v.ChangeNewPassword = string.Empty;
                v.ChangeConfirmPassword = string.Empty;
            }
            UpdateStrengthBar(vm);
            ShowToast("Password Updated", "Your account password has been changed.");
        }

        PasswordMessageText.Text = msg;
        PasswordMessageBorder.Visibility = Visibility.Visible;
    }

    private void OnOpenAppLockEditorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ShowAppLockEditor = true;
        }
    }

    private async void OnAppLockToggleClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        if (!vm.AppLockEnabled)
        {
            // Turning ON — verify Windows Hello is available first
            var available = await Windows.Security.Credentials.UI.UserConsentVerifier
                .CheckAvailabilityAsync();

            if (available != Windows.Security.Credentials.UI.UserConsentVerifierAvailability.Available)
            {
                var dlg = new ContentDialog
                {
                    Title = "Windows Hello Not Available",
                    Content = "Your device does not support Windows Hello or it has not been set up. Go to Windows Settings → Accounts → Sign-in options to configure it.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot,
                };
                await dlg.ShowAsync();
                return;
            }

            vm.AppLockEnabled = true;
            ShowToast("App Lock Enabled", "Windows Hello will be required on next launch.");
        }
        else
        {
            // Turning OFF — confirm with Windows Hello first
            var result = await Windows.Security.Credentials.UI.UserConsentVerifier
                .RequestVerificationAsync("Verify your identity to disable App Lock.");

            if (result == Windows.Security.Credentials.UI.UserConsentVerificationResult.Verified)
            {
                vm.AppLockEnabled = false;
                ShowToast("App Lock Disabled", "App will open without verification.");
            }
        }
    }

    private void OnCloseAppLockEditorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ShowAppLockEditor = false;
            vm.PasscodeMessage = string.Empty;
        }
    }

    private void OnOpenTwoFactorEditorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ShowTwoFactorEditor = true;
        }
    }

    private void OnUseEmailProfileClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;
        vm.ProfileName = string.Empty;
        if (vm.SaveProfileCommand.CanExecute(null))
        {
            vm.SaveProfileCommand.Execute(null);
            _activity.LogUserAction("Profile Updated", "Display name cleared — email will be shown");
            ShowToast("Profile Updated", "Email will now be shown as display name.");
        }
    }

    private async void OnChangePhotoClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm) return;

        var picker = new FileOpenPicker();
        // Associate the picker with the window handle (required on WinUI 3)
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainAppWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.ViewMode = PickerViewMode.Thumbnail;
        picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        if (vm.UploadAvatarCommand.CanExecute(file))
        {
            await vm.UploadAvatarCommand.ExecuteAsync(file);
            ShowToast("Photo Updated", "Your profile photo has been saved.");
        }
    }

    private void OnCloseTwoFactorEditorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ShowTwoFactorEditor = false;
            vm.TwoFactorMessage = string.Empty;
        }
    }

    private void OnRemovePasscodeClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ConfirmAndRunAsync(
            "Remove app passcode?",
            "This will remove local app lock on this device. Continue?",
            vm => vm.ClearPasscodeCommand);
    }

    private void OnDisableTwoFactorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _ = ConfirmAndRunAsync(
            "Disable 2FA?",
            "This reduces account security and recovery protection. Continue?",
            vm => vm.DisableTwoFactorCommand);
    }

    private async Task ConfirmAndRunAsync(
        string title,
        string body,
        Func<SettingsViewModel, System.Windows.Input.ICommand> commandPicker)
    {
        if (DataContext is not SettingsViewModel vm || XamlRoot is null)
        {
            return;
        }

        var dlg = new ContentDialog
        {
            Title = title,
            Content = body,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var command = commandPicker(vm);
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    private async void OnCopyTwoFactorKeyClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || string.IsNullOrWhiteSpace(vm.TwoFactorSecretDisplay)) return;
        var data = new DataPackage();
        data.SetText(vm.TwoFactorSecretDisplay);
        Clipboard.SetContent(data);
        vm.TwoFactorMessage = "Setup key copied to clipboard.";
        ShowToast("Key Copied", "2FA setup key copied to clipboard.");
        await vm.ShowTwoFactorKeyCopiedAsync();
    }

}
