using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class NavItemViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required string IconGlyph { get; init; }

    public required AppPage Page { get; init; }

    public bool IsEnabled { get; init; } = true;

    /// <summary>False when the shell sidebar is collapsed (icon-only rail).</summary>
    [ObservableProperty]
    private bool showNavLabel = true;

    public HorizontalAlignment NavRowAlignment =>
        ShowNavLabel ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

    public Thickness NavRowPadding =>
        ShowNavLabel ? new Thickness(8, 6, 8, 6) : new Thickness(2, 4, 2, 4);

    partial void OnShowNavLabelChanged(bool value)
    {
        OnPropertyChanged(nameof(NavRowAlignment));
        OnPropertyChanged(nameof(NavRowPadding));
    }
}