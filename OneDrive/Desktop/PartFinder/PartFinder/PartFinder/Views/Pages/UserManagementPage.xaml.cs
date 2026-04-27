using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PartFinder.Services;
using PartFinder.ViewModels;
using PartFinder.Views.Components;

namespace PartFinder.Views.Pages;

public sealed partial class UserManagementPage : Page
{
    public UserManagementPage()
    {
        InitializeComponent();
        var vm = App.Services.GetRequiredService<UserManagementViewModel>();
        DataContext = vm;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var access = App.Services.GetRequiredService<ICurrentUserAccessService>();
        await access.RefreshAsync().ConfigureAwait(true);
        if (!access.Capabilities.CanAccessUserManagement)
        {
            App.Services.GetRequiredService<INavigationService>().Navigate(AppPage.Parts);
            return;
        }

        if (DataContext is UserManagementViewModel vm)
        {
            await vm.LoadUsersAsync().ConfigureAwait(true);
        }
    }

    private async void OnInviteClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (DataContext is not UserManagementViewModel vm) return;

        await vm.PrepareInviteDialogAsync().ConfigureAwait(true);
        var content = new InviteUserControl { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = "Invite New User",
            PrimaryButtonText = "Send Invite",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = content,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var (err, resultInfo) = await vm.InviteAsync().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(err))
        {
            var errDialog = new ContentDialog
            {
                Title = "Could not invite",
                Content = err,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        if (resultInfo is null) return;

        // Show toast
        var msg = resultInfo.EmailSent
            ? "Invite email sent successfully."
            : "Invite saved. Email could not be sent.";
        ShowToast("User Invited", msg);
    }

    // ── Toast with slide-up + fade animation ────────────────
    private async void ShowToast(string title = "Done", string message = "Changes saved.")
    {
        ToastTitle.Text = title;
        ToastMessage.Text = message;

        // Slide up + fade in
        var fadeIn = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        var slideIn = new DoubleAnimation { From = 20, To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };

        var storyIn = new Storyboard();
        Storyboard.SetTarget(fadeIn, ToastPopup);
        Storyboard.SetTargetProperty(fadeIn, "Opacity");
        Storyboard.SetTarget(slideIn, ToastTranslate);
        Storyboard.SetTargetProperty(slideIn, "Y");
        storyIn.Children.Add(fadeIn);
        storyIn.Children.Add(slideIn);
        storyIn.Begin();

        await Task.Delay(2500);

        // Slide down + fade out
        var fadeOut = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(350), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        var slideOut = new DoubleAnimation { From = 0, To = 20, Duration = TimeSpan.FromMilliseconds(350), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

        var storyOut = new Storyboard();
        Storyboard.SetTarget(fadeOut, ToastPopup);
        Storyboard.SetTargetProperty(fadeOut, "Opacity");
        Storyboard.SetTarget(slideOut, ToastTranslate);
        Storyboard.SetTargetProperty(slideOut, "Y");
        storyOut.Children.Add(fadeOut);
        storyOut.Children.Add(slideOut);
        storyOut.Begin();
    }
}
