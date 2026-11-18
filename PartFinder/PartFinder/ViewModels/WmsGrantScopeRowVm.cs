using CommunityToolkit.Mvvm.ComponentModel;

namespace PartFinder.ViewModels;

/// <summary>Checkbox row for operator grant warehouse / stock-owner scope (03A.2).</summary>
public partial class WmsGrantScopeRowVm : ObservableObject
{
    public string Id { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected = true;
}