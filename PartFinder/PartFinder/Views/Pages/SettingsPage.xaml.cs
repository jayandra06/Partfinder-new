using Microsoft.Extensions.DependencyInjection;

using Microsoft.UI.Xaml;

using Microsoft.UI.Xaml.Controls;

using Microsoft.UI.Xaml.Media;

using Microsoft.UI.Xaml.Media.Animation;

using Microsoft.UI.Xaml.Input;

using PartFinder.Models;

using PartFinder.Services;

using PartFinder.ViewModels;

using Windows.ApplicationModel.DataTransfer;

using Windows.Storage;

using Windows.Storage.Pickers;

using System.Collections.Specialized;



namespace PartFinder.Views.Pages;



public sealed partial class SettingsPage : Page

{

    private readonly ActivityLogger _activity = App.Services.GetRequiredService<ActivityLogger>();



    public SettingsPage()

    {

        InitializeComponent();

        DataContext = App.Services.GetRequiredService<SettingsViewModel>();
        VisualStateManager.GoToState(this, "Collapsed", false);

    }

    private void OnSidebarEntered(object sender, PointerRoutedEventArgs e) => VisualStateManager.GoToState(this, "Expanded", true);
    private void OnSidebarExited(object sender, PointerRoutedEventArgs e) => VisualStateManager.GoToState(this, "Collapsed", true);



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

        ShowSection(profile: true, security: false, password: false, appLock: false, loginMgmt: false);



    private void OnNavSecurityClick(object sender, RoutedEventArgs e) =>

        ShowSection(profile: false, security: true, password: false, appLock: false, loginMgmt: false);



    private void OnNavPasswordClick(object sender, RoutedEventArgs e) =>

        ShowSection(profile: false, security: false, password: true, appLock: false, loginMgmt: false);



    private void OnNavAppLockClick(object sender, RoutedEventArgs e) =>

        ShowSection(profile: false, security: false, password: false, appLock: true, loginMgmt: false);



    private async void OnNavLoginMgmtClick(object sender, RoutedEventArgs e)

    {

        ShowSection(profile: false, security: false, password: false, appLock: false, loginMgmt: true);

        // Auto-load sessions when navigating to this section

        if (DataContext is SettingsViewModel vm)

        {

            vm.ActiveSessions.CollectionChanged -= OnActiveSessionsChanged;

            vm.ActiveSessions.CollectionChanged += OnActiveSessionsChanged;

            if (vm.LoadActiveSessionsCommand.CanExecute(null))

            {

                await Task.Delay(50);

                await vm.LoadActiveSessionsCommand.ExecuteAsync(null);

                RebuildSessionCards();

            }

        }

    }



    private void OnActiveSessionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)

    {

        RebuildSessionCards();

    }



    private void RebuildSessionCards()

    {

        if (DataContext is not SettingsViewModel vm) return;

        SessionCardsPanel.Children.Clear();



        foreach (var session in vm.ActiveSessions)

        {

            var isCurrent = vm.IsCurrentSession(session);

            var card = BuildSessionCard(session, isCurrent);

            SessionCardsPanel.Children.Add(card);

        }

    }



    private Border BuildSessionCard(LoginSessionRecord session, bool isCurrent)

    {

        // Card border

        var card = new Border

        {

            Background = (Brush)Application.Current.Resources["CardBackgroundBrush"],

            BorderBrush = isCurrent

                ? (Brush)Application.Current.Resources["AccentPrimaryBrush"]

                : (Brush)Application.Current.Resources["BorderDefaultBrush"],

            BorderThickness = isCurrent ? new Thickness(1.5) : new Thickness(1),

            CornerRadius = new CornerRadius(10),

            Padding = new Thickness(20, 16, 20, 16),

        };



        var grid = new Grid { ColumnSpacing = 16 };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });



        // Device icon

        var iconBorder = new Border

        {

            Width = 44, Height = 44,

            CornerRadius = new CornerRadius(22),

            Background = (Brush)Application.Current.Resources["ElevatedCardBackgroundBrush"],

            BorderBrush = (Brush)Application.Current.Resources["BorderDefaultBrush"],

            BorderThickness = new Thickness(1),

            Child = new FontIcon { Glyph = "\uE7F4", FontSize = 18, Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"] }

        };

        Grid.SetColumn(iconBorder, 0);

        grid.Children.Add(iconBorder);



        // Details panel

        var detailsPanel = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        Grid.SetColumn(detailsPanel, 1);



        // Name row with badge

        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        nameRow.Children.Add(new TextBlock

        {

            Text = session.DeviceName,

            FontSize = 14,

            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,

            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"]

        });



        if (isCurrent)

        {

            var badge = new Border

            {

                Background = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x18, 0x1F, 0x7A, 0xE0)),

                BorderBrush = new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(0x30, 0x1F, 0x7A, 0xE0)),

                BorderThickness = new Thickness(1),

                CornerRadius = new CornerRadius(4),

                Padding = new Thickness(6, 2, 6, 2),

                VerticalAlignment = VerticalAlignment.Center,

                Child = new TextBlock

                {

                    Text = "CURRENT",

                    FontSize = 9,

                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,

                    Foreground = (Brush)Application.Current.Resources["AccentPrimaryBrush"],

                    CharacterSpacing = 40

                }

            };

            nameRow.Children.Add(badge);

        }

        detailsPanel.Children.Add(nameRow);



        // Info rows

        var infoPanel = new StackPanel { Spacing = 4 };



        // Location

        var locRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        locRow.Children.Add(new FontIcon { Glyph = "\uE774", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"] });

        locRow.Children.Add(new TextBlock { Text = session.Location, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"] });

        infoPanel.Children.Add(locRow);



        // IP

        var ipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        ipRow.Children.Add(new FontIcon { Glyph = "\uE968", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"] });

        ipRow.Children.Add(new TextBlock { Text = $"IP: {session.IpAddress}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"] });

        infoPanel.Children.Add(ipRow);



        // Login time

        var timeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        timeRow.Children.Add(new FontIcon { Glyph = "\uE787", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"] });

        timeRow.Children.Add(new TextBlock { Text = $"Logged in: {session.LoginTime.ToLocalTime():g}", FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextTertiaryBrush"] });

        infoPanel.Children.Add(timeRow);



        detailsPanel.Children.Add(infoPanel);

        grid.Children.Add(detailsPanel);



        // Logout button - only for non-current sessions

        if (!isCurrent)

        {

            var logoutBtn = new Button

            {

                Content = "Logout",

                Foreground = (Brush)Application.Current.Resources["DangerBrush"],

                Style = (Style)Application.Current.Resources["OutlinedButtonStyle"],

                VerticalAlignment = VerticalAlignment.Center,

                Padding = new Thickness(16, 8, 16, 8),

                Tag = session,

            };

            logoutBtn.Click += OnRemoteLogoutClick;

            Grid.SetColumn(logoutBtn, 2);

            grid.Children.Add(logoutBtn);

        }



        card.Child = grid;

        return card;

    }



    private async void OnLogoutClick(object sender, RoutedEventArgs e)

    {

        var dialog = new ContentDialog

        {

            Title = "Logout",

            Content = "Are you sure you want to logout? You will need to login again to access PartFinder.",

            PrimaryButtonText = "Logout",

            CloseButtonText = "Cancel",

            DefaultButton = ContentDialogButton.Close,

            XamlRoot = XamlRoot

        };



        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary)

            return;



        var adminSession = App.Services.GetRequiredService<AdminSessionStore>();

        adminSession.Clear();

        // Do NOT delete setup-state.json — it holds orgCode/dbUri needed after re-login



        if (App.MainAppWindow is MainWindow main)

        {

            main.ResetToSetup();

        }

        else

        {

            Application.Current.Exit();

        }

    }



    private void ShowSection(bool profile, bool security, bool password, bool appLock, bool loginMgmt)

    {

        // Sections

        ProfileSection.Visibility = profile ? Visibility.Visible : Visibility.Collapsed;

        SecuritySection.Visibility = security ? Visibility.Visible : Visibility.Collapsed;

        PasswordSection.Visibility = password ? Visibility.Visible : Visibility.Collapsed;

        AppLockSection.Visibility = appLock ? Visibility.Visible : Visibility.Collapsed;

        LoginMgmtSection.Visibility = loginMgmt ? Visibility.Visible : Visibility.Collapsed;



        // Nav selection styling (UI only)

        SetNavSelected(NavProfileItem, profile);

        SetNavSelected(NavSecurityItem, security);

        SetNavSelected(NavTwoFactorItem, security);

        SetNavSelected(NavPasswordItem, password);

        SetNavSelected(NavAppLockItem, appLock);

        SetNavSelected(NavLoginMgmtItem, loginMgmt);

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



    private void OnGeneratePasswordClick(object sender, RoutedEventArgs e)

    {

        if (DataContext is not SettingsViewModel vm) return;

        var generated = SettingsViewModel.GenerateStrongPassword();

        vm.ChangeNewPassword = generated;

        vm.ChangeConfirmPassword = generated;

        NewPasswordBox.Password = generated;

        ConfirmPasswordBox.Password = generated;

        UpdateStrengthBar(vm);

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

        CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        CurrentPasswordRevealIcon.Glyph = "\uE890";

        NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        NewPasswordRevealIcon.Glyph = "\uE890";

        ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        ConfirmPasswordRevealIcon.Glyph = "\uE890";

        PasswordMessageBorder.Visibility = Visibility.Collapsed;

        UpdateStrengthBar(vm);

    }



    private void OnCurrentPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)

    {

        if (CurrentPasswordBox.PasswordRevealMode == PasswordRevealMode.Hidden)

        {

            CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Visible;

            CurrentPasswordRevealIcon.Glyph = "\uF22B";

        }

        else

        {

            CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            CurrentPasswordRevealIcon.Glyph = "\uE890";

        }

    }



    private void OnNewPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)

    {

        if (NewPasswordBox.PasswordRevealMode == PasswordRevealMode.Hidden)

        {

            NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Visible;

            NewPasswordRevealIcon.Glyph = "\uF22B";

        }

        else

        {

            NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            NewPasswordRevealIcon.Glyph = "\uE890";

        }

    }



    private void OnConfirmPasswordRevealClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)

    {

        if (ConfirmPasswordBox.PasswordRevealMode == PasswordRevealMode.Hidden)

        {

            ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Visible;

            ConfirmPasswordRevealIcon.Glyph = "\uF22B";

        }

        else

        {

            ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            ConfirmPasswordRevealIcon.Glyph = "\uE890";

        }

    }



    // Override ChangePasswordCommand result display

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)

    {

        base.OnNavigatedTo(e);

        if (DataContext is SettingsViewModel vm)

        {

            vm.PropertyChanged += OnSettingsVmPropertyChanged;

            // Refresh org info every time the page is opened so ORGANIZATION section

            // always shows the latest data fetched at login (never shows stale/empty values).

            vm.RefreshOrgInfo();

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

        if (DataContext is not SettingsViewModel vm) return;



        // Auto-fill password boxes when Generate button sets ChangeNewPassword

        if (e.PropertyName == nameof(SettingsViewModel.ChangeNewPassword))

        {

            if (NewPasswordBox.Password != vm.ChangeNewPassword)

            {

                NewPasswordBox.Password = vm.ChangeNewPassword;

                UpdateStrengthBar(vm);

            }

        }



        if (e.PropertyName == nameof(SettingsViewModel.ChangeConfirmPassword))

        {

            if (ConfirmPasswordBox.Password != vm.ChangeConfirmPassword)

                ConfirmPasswordBox.Password = vm.ChangeConfirmPassword;

        }



        if (e.PropertyName != nameof(SettingsViewModel.ChangePasswordMessage)) return;



        var msg = vm.ChangePasswordMessage;

        if (string.IsNullOrWhiteSpace(msg))

        {

            PasswordMessageBorder.Visibility = Visibility.Collapsed;

            CurrentPasswordErrorBorder.Visibility = Visibility.Collapsed;

        CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        CurrentPasswordRevealIcon.Glyph = "\uE890";

        NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        NewPasswordRevealIcon.Glyph = "\uE890";

        ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        ConfirmPasswordRevealIcon.Glyph = "\uE890";

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

        CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        CurrentPasswordRevealIcon.Glyph = "\uE890";

        NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        NewPasswordRevealIcon.Glyph = "\uE890";

        ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

        ConfirmPasswordRevealIcon.Glyph = "\uE890";



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

            CurrentPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            CurrentPasswordRevealIcon.Glyph = "\uE890";

            NewPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            NewPasswordRevealIcon.Glyph = "\uE890";

            ConfirmPasswordBox.PasswordRevealMode = PasswordRevealMode.Hidden;

            ConfirmPasswordRevealIcon.Glyph = "\uE890";

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



    private async void OnRemoteLogoutClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)

    {

        if (sender is not Button btn || btn.Tag is not PartFinder.Models.LoginSessionRecord session) return;

        if (DataContext is not SettingsViewModel vm) return;



        // Check if this is the current session

        if (vm.IsCurrentSession(session))

        {

            ShowToast("Not Allowed", "You cannot logout from the current device here.");

            return;

        }



        var dialog = new ContentDialog

        {

            Title = "Logout Device",

            Content = $"Are you sure you want to sign out from \"{session.DeviceName}\"?\nLocation: {session.Location}\nIP: {session.IpAddress}",

            PrimaryButtonText = "Logout",

            CloseButtonText = "Cancel",

            DefaultButton = ContentDialogButton.Close,

            XamlRoot = XamlRoot

        };



        var result = await dialog.ShowAsync();

        if (result != ContentDialogResult.Primary) return;



        if (vm.LogoutRemoteSessionCommand.CanExecute(session))

        {

            await vm.LogoutRemoteSessionCommand.ExecuteAsync(session);

            RebuildSessionCards();

            ShowToast("Device Logged Out", $"Signed out from {session.DeviceName}.");

        }

    }



}

