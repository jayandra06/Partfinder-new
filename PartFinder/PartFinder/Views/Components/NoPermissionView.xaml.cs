using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Views.Components;

public sealed partial class NoPermissionView : UserControl
{
    public NoPermissionView()
    {
        InitializeComponent();
    }

    public string Message
    {
        get => MessageText.Text;
        set => MessageText.Text = value;
    }
}
