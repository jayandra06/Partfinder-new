using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Helpers;

/// <summary>
/// Shows a standard "no permission" dialog when a user tries an action they're not allowed to do.
/// </summary>
public static class PermissionToast
{
    public const string DefaultMessage = "You don't have permission to access this feature.";

    public static async System.Threading.Tasks.Task ShowAsync(XamlRoot? xamlRoot, string? message = null)
    {
        if (xamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Access Restricted",
            Content = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(message) ? DefaultMessage : message,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
        };

        await dialog.ShowAsync();
    }
}
