using CommunityToolkit.Mvvm.ComponentModel;

namespace PartFinder.ViewModels;

public sealed partial class TemplatePickItemViewModel : ObservableObject
{
    public required string TemplateId { get; init; }
    public required string Name { get; init; }

    [ObservableProperty]
    private bool isSelected;
}
