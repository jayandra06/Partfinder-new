using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
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
        if (DataContext is not UserManagementViewModel vm)
        {
            return;
        }

        await vm.PrepareInviteDialogAsync().ConfigureAwait(true);
        var content = new InviteUserControl
        {
            DataContext = vm,
        };
        var dialog = new ContentDialog
        {
            Title = "Invite user",
            PrimaryButtonText = "Send invite",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = content,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

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

        if (resultInfo is null)
        {
            return;
        }

        var sent = resultInfo.EmailSent
            ? "Invite email sent successfully."
            : $"Invite saved but email could not be sent: {resultInfo.EmailError}";

        var infoDialog = new ContentDialog
        {
            Title = "Invite created",
            Content =
                $"Organization code: {resultInfo.OrganizationCode}\n" +
                $"Email: {resultInfo.Email}\n" +
                $"Temporary password: {resultInfo.TemporaryPassword}\n\n" +
                sent,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await infoDialog.ShowAsync();
    }
}
