using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
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
        if (DataContext is not SettingsViewModel vm)
        {
            return;
        }

        vm.ProfileName = string.Empty;
        if (vm.SaveProfileCommand.CanExecute(null))
        {
            vm.SaveProfileCommand.Execute(null);
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
        if (DataContext is not SettingsViewModel vm || string.IsNullOrWhiteSpace(vm.TwoFactorSecretDisplay))
        {
            return;
        }

        var data = new DataPackage();
        data.SetText(vm.TwoFactorSecretDisplay);
        Clipboard.SetContent(data);
        vm.TwoFactorMessage = "Setup key copied to clipboard.";
        await vm.ShowTwoFactorKeyCopiedAsync();
    }

}
