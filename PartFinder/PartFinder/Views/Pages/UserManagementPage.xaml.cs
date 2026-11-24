using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
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
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

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

    private void OnUserActionsButtonClick(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is not Button btn)
        {
            return;
        }

        // Row DataContext is the user; Tag also binds to the same item for templates where Tag is clearer.
        var user = btn.DataContext as Models.OrgAppUserSummary ?? btn.Tag as Models.OrgAppUserSummary;
        if (user is null)
        {
            return;
        }

        var flyout = BuildUserActionsFlyout(user);
        flyout.XamlRoot = btn.XamlRoot;
        flyout.ShowAt(btn, new FlyoutShowOptions { Placement = FlyoutPlacementMode.Bottom });
    }

    private MenuFlyout BuildUserActionsFlyout(Models.OrgAppUserSummary user)
    {
        var flyout = new MenuFlyout { ShouldConstrainToRootBounds = false };

        var canManage = user.CanEditInUserManagement;

        var edit = new MenuFlyoutItem
        {
            Text = "Edit",
        };
        ToolTipService.SetToolTip(
            edit,
            canManage
                ? "Change name, role, and access for this user."
                : "Organisation directory admins (owner/bootstrap) are not edited on this screen.");

        edit.Click += (_, _) =>
        {
            flyout.Hide();
            if (!user.CanEditInUserManagement)
            {
                EnqueueAfterFlyoutClosed(() => _ = ShowOrgAdminNotManagedDialogAsync(
                    "Cannot edit this user",
                    "This account is the organisation directory admin (often the owner). It is not edited from Team Management. Use Account Settings or your backend admin tools if you need to change it."));
                return;
            }

            EnqueueAfterFlyoutClosed(() => _ = RunEditUserAsync(user));
        };
        flyout.Items.Add(edit);

        var delete = new MenuFlyoutItem
        {
            Text = "Delete",
        };
        if (Application.Current.Resources.TryGetValue("DangerBrush", out var dangerObj) && dangerObj is Brush dangerBrush)
        {
            delete.Foreground = dangerBrush;
        }

        ToolTipService.SetToolTip(
            delete,
            canManage
                ? "Remove this user from the organisation."
                : "Organisation directory admins cannot be removed from this list.");

        delete.Click += (_, _) =>
        {
            flyout.Hide();
            if (!user.CanEditInUserManagement)
            {
                EnqueueAfterFlyoutClosed(() => _ = ShowOrgAdminNotManagedDialogAsync(
                    "Cannot delete this user",
                    "This account is the organisation directory admin (often the owner). It is not removed from Team Management."));
                return;
            }

            EnqueueAfterFlyoutClosed(() => _ = RunDeleteUserAsync(user));
        };
        flyout.Items.Add(delete);

        return flyout;
    }

    /// <summary>
    /// WinUI does not reliably show a <see cref="ContentDialog"/> while a <see cref="MenuFlyout"/> is still open.
    /// Hide the flyout first, then run the action on the UI thread after it closes.
    /// </summary>
    private void EnqueueAfterFlyoutClosed(Action action)
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is null)
        {
            action();
            return;
        }

        _ = dq.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }

    private async Task ShowOrgAdminNotManagedDialogAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };

        await dialog.ShowAsync();
    }

    private async Task RunEditUserAsync(Models.OrgAppUserSummary user)
    {
        if (DataContext is not UserManagementViewModel vm) return;

        await vm.PrepareEditDialogAsync(user).ConfigureAwait(true);
        var content = new InviteUserControl { DataContext = vm };
        var dialog = new ContentDialog
        {
            Title = "Edit User",
            PrimaryButtonText = "Save Changes",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = content,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var err = await vm.SaveEditAsync().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(err))
        {
            var errDialog = new ContentDialog
            {
                Title = "Could not update",
                Content = err,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        ShowToast("User Updated", "User details updated successfully.");
    }

    private async Task RunDeleteUserAsync(Models.OrgAppUserSummary user)
    {
        if (DataContext is not UserManagementViewModel vm) return;

        var dialog = new ContentDialog
        {
            Title = "Delete User",
            Content = $"Are you sure you want to delete user {user.Name}?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary) return;

        var err = await vm.DeleteUserAsync(user).ConfigureAwait(true);
        if (!string.IsNullOrEmpty(err))
        {
            var errDialog = new ContentDialog
            {
                Title = "Could not delete",
                Content = err,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot,
            };
            await errDialog.ShowAsync();
            return;
        }

        ShowToast("User Deleted", "User deleted successfully.");
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
