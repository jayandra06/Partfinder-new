using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Input;
using PartFinder.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace PartFinder.Views.Pages;

public sealed partial class SettingsPage : Page
{
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
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.PasscodeCurrent = PasscodeCurrentBox.Password;
        vm.PasscodeNew = PasscodeNewBox.Password;
        vm.PasscodeConfirm = PasscodeConfirmBox.Password;
    }

    private void OnSavePasscodeClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && vm.SavePasscodeCommand.CanExecute(null))
        {
            vm.SavePasscodeCommand.Execute(null);
            ShowToast("App Lock Updated", "Your passcode has been saved.");
        }
    }

    private void OnOpenAppLockEditorClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.ShowAppLockEditor = true;
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
            ShowToast("Profile Updated", "Email will now be shown as display name.");
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
