using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;

namespace PartFinder.ViewModels;

public sealed partial class FavouriteCardViewModel : ViewModelBase
{
    public FavouriteCardViewModel(PartTemplateDefinition template, bool isFavourite)
    {
        Template = template;
        Name = template.Name;
        FieldCountLabel = $"{template.Fields.Count} columns";
        _isFavourite = isFavourite;
    }

    public PartTemplateDefinition Template { get; }

    public string Name { get; }

    public string FieldCountLabel { get; }

    [ObservableProperty]
    private bool _isFavourite;
}
