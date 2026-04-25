using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PartFinder.ViewModels;

public sealed partial class MasterDataRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string? rowId;

    public ObservableCollection<MasterDataCellViewModel> Cells { get; } = new();
}
