using Microsoft.UI.Xaml.Controls;
using PartFinder.Helpers;

namespace PartFinder.Views.Components;

public sealed partial class InviteUserControl : UserControl
{
    public InviteUserControl()
    {
        InitializeComponent();
        Loaded += (_, _) => UiResourceHelper.PrepareDialogInputTree(this);
    }
}
