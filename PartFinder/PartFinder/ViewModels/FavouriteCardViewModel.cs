using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;

namespace PartFinder.ViewModels;

public sealed partial class FavouriteCardViewModel : ViewModelBase
{
    public FavouriteCardViewModel(PartTemplateDefinition template, bool isFavourite,
        IReadOnlyList<MasterDataRowRecord>? records = null)
    {
        Template = template;
        Name = template.Name;
        FieldCountLabel = $"{template.Fields.Count} columns";
        _isFavourite = isFavourite;
        Records = records ?? Array.Empty<MasterDataRowRecord>();
    }

    public PartTemplateDefinition Template { get; }

    public string Name { get; }

    public string FieldCountLabel { get; }

    /// <summary>Actual data rows for this template — used to show values in the card.</summary>
    public IReadOnlyList<MasterDataRowRecord> Records { get; }

    [ObservableProperty]
    private bool _isFavourite;
}
