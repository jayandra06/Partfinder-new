using CommunityToolkit.Mvvm.ComponentModel;
using PartFinder.Models;

namespace PartFinder.ViewModels;

public sealed partial class MasterDataCellViewModel : ObservableObject
{
    public required string FieldKey { get; init; }

    public TemplateFieldType FieldType { get; init; }

    public string? LinkedTemplateId { get; init; }

    public string? LinkedDisplayFieldKey { get; init; }

    public bool IsRecordLink => FieldType == TemplateFieldType.RecordLink;

    [ObservableProperty]
    private string text = string.Empty;

    /// <summary>Mongo id of the linked row when <see cref="IsRecordLink"/>.</summary>
    [ObservableProperty]
    private string? linkedRowId;

    public string EffectiveCopyValue => IsRecordLink
        ? (!string.IsNullOrEmpty(LinkedRowId) ? LinkedRowId : Text)
        : Text;
}
