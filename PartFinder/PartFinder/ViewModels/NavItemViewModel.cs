using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public sealed partial class NavItemViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required string IconGlyph { get; init; }

    public required AppPage Page { get; init; }

    public bool IsEnabled { get; init; } = true;

    [ObservableProperty]
    private bool isSelected;
}