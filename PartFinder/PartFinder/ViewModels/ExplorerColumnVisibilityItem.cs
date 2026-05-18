using CommunityToolkit.Mvvm.ComponentModel;

namespace PartFinder.ViewModels;

public sealed partial class ExplorerColumnVisibilityItem : ObservableObject
{
    public required string Key { get; init; }
    public required string Label { get; init; }

    [ObservableProperty]
    private bool isVisible = true;
}
