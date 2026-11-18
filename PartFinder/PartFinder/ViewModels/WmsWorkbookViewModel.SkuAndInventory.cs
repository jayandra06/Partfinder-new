using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;

namespace PartFinder.ViewModels;

public partial class WmsWorkbookViewModel
{
    public ObservableCollection<WmsUomDto> UomRows { get; } = new();
    public ObservableCollection<WmsSkuIdentifierDto> IdentifierRows { get; } = new();
    public ObservableCollection<WmsUnmappedRowDisplay> UnmappedRows { get; } = new();

    public IReadOnlyList<string> IdentifierNamespaceOptions { get; } =
        WmsSkuIdentifierNamespaces.All;

    [RelayCommand]
    private async Task ApplySearchAsync()
    {
        await LoadSectionAsync(SelectedSectionId).ConfigureAwait(true);
    }

    [RelayCommand]
    private void CancelSkuDraft()
    {
        NewSkuDisplayName = string.Empty;
        NewSkuDescription = string.Empty;
        NewSkuPrimaryUom = "EA";
        StatusMessage = "SKU draft cleared.";
    }

    [RelayCommand]
    private async Task SaveNewSkuAsync()
    {
        if (!CanEditSetup)
        {
            StatusMessage = "Admin permission required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewSkuDisplayName))
        {
            StatusMessage = "Display name is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateSkuAsync(new CreateWmsSkuRequest
            {
                DisplayName = NewSkuDisplayName.Trim(),
                Description = string.IsNullOrWhiteSpace(NewSkuDescription) ? null : NewSkuDescription.Trim(),
                PrimaryUom = NewSkuPrimaryUom.Trim().ToUpperInvariant(),
            }).ConfigureAwait(true);

            if (!result.Ok || result.Data is null)
            {
                ApplyApiError(result.Error);
                return;
            }

            await ReloadSkusAsync().ConfigureAwait(true);
            CancelSkuDraft();
            StatusMessage = $"SKU {result.Data.SkuId} registered.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddIdentifierAsync()
    {
        if (!CanEditSetup || SelectedSku is null)
        {
            StatusMessage = "Select a SKU and use an admin account.";
            return;
        }

        if (string.IsNullOrWhiteSpace(IdentifierValue))
        {
            StatusMessage = "Identifier value is required.";
            return;
        }

        IsBusy = true;
        try
        {
            var metadata = new Dictionary<string, string>();
            if (IdentifierNamespace == "manufacturerPn" && !string.IsNullOrWhiteSpace(IdentifierManufacturer))
                metadata["manufacturerName"] = IdentifierManufacturer.Trim();

            var check = await _wms.CheckSkuIdentifierAsync(
                new AddWmsSkuIdentifierRequest
                {
                    Namespace = IdentifierNamespace,
                    Value = IdentifierValue.Trim(),
                    Metadata = metadata.Count > 0 ? metadata : null,
                },
                SelectedSku.SkuId).ConfigureAwait(true);

            if (!check.Ok)
            {
                ApplyApiError(check.Error);
                return;
            }

            if (check.Data?.Conflict is not null)
            {
                StatusMessage = "Identifier conflict — choose a different scope or value.";
                return;
            }

            var result = await _wms.AddSkuIdentifierAsync(
                SelectedSku.SkuId,
                new AddWmsSkuIdentifierRequest
                {
                    Namespace = IdentifierNamespace,
                    Value = IdentifierValue.Trim(),
                    Metadata = metadata.Count > 0 ? metadata : null,
                }).ConfigureAwait(true);

            if (!result.Ok)
            {
                ApplyApiError(result.Error);
                return;
            }

            IdentifierValue = string.Empty;
            await ReloadSkuIdentifiersAsync().ConfigureAwait(true);
            StatusMessage = "Identifier saved.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveNewUomAsync()
    {
        if (!CanEditSetup)
        {
            StatusMessage = "Admin permission required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewUomCode) || string.IsNullOrWhiteSpace(NewUomName))
        {
            StatusMessage = "UOM code and name are required.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _wms.CreateUomAsync(new CreateWmsUomRequest
            {
                Code = NewUomCode.Trim(),
                Name = NewUomName.Trim(),
                Category = "custom",
            }).ConfigureAwait(true);

            if (!result.Ok)
            {
                ApplyApiError(result.Error);
                return;
            }

            NewUomCode = string.Empty;
            NewUomName = string.Empty;
            await ReloadUomAsync().ConfigureAwait(true);
            StatusMessage = $"UOM {result.Data?.Code} saved.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadSkusAsync(CancellationToken ct = default)
    {
        SkuRows.Clear();
        var skus = await _wms.ListSkusAsync("active", ct).ConfigureAwait(true);
        if (!skus.Ok)
        {
            ApplyApiError(skus.Error);
            return;
        }

        if (skus.Data?.Items is null)
        {
            return;
        }

        foreach (var s in FilterBySearch(skus.Data.Items, InventorySearch, x => $"{x.SkuId} {x.DisplayName}"))
        {
            SkuRows.Add(s);
        }
    }

    private async Task ReloadSkuIdentifiersAsync(CancellationToken ct = default)
    {
        IdentifierRows.Clear();
        if (SelectedSku is null)
        {
            return;
        }

        var detail = await _wms.GetSkuAsync(SelectedSku.SkuId, ct).ConfigureAwait(true);
        if (!detail.Ok)
        {
            ApplyApiError(detail.Error);
            return;
        }

        if (detail.Data?.Identifiers is null)
        {
            return;
        }

        foreach (var id in detail.Data.Identifiers.Where(i => i.Status == "active"))
        {
            IdentifierRows.Add(id);
        }
    }

    private async Task ReloadUomAsync(CancellationToken ct = default)
    {
        UomRows.Clear();
        var uom = await _wms.ListUomAsync(ct).ConfigureAwait(true);
        if (!uom.Ok)
        {
            ApplyApiError(uom.Error);
            return;
        }

        foreach (var row in FilterBySearch(uom.Items, InventorySearch, x => $"{x.Code} {x.Name}"))
        {
            UomRows.Add(row);
        }
    }

    private async Task ReloadDryRunAsync(CancellationToken ct = default)
    {
        MappingErrorRows.Clear();
        UnmappedRows.Clear();
        var dry = await _wms.OpeningBalanceDryRunAsync(ct).ConfigureAwait(true);
        if (!dry.Ok || dry.Data is null)
        {
            ApplyApiError(dry.Error);
            return;
        }

        OpeningDryRunSummary =
            $"Source rows: {dry.Data.TotalSourceRows}; mapped: {dry.Data.MappedCount}; " +
            $"unmapped: {dry.Data.UnmappedCount}; duplicate candidates: {dry.Data.DuplicateCandidateCount}; " +
            $"invalid qty: {dry.Data.InvalidQuantityCount}.";

        if (dry.Data.UnmappedCount > 0)
            MappingErrorRows.Add($"{dry.Data.UnmappedCount} legacy inventory rows lack SKU mapping.");

        if (dry.Data.InvalidQuantityCount > 0)
            MappingErrorRows.Add($"{dry.Data.InvalidQuantityCount} rows have invalid quantities.");

        if (dry.Data.Unmapped is not null)
        {
            foreach (var row in dry.Data.Unmapped.Take(500))
            {
                UnmappedRows.Add(new WmsUnmappedRowDisplay
                {
                    LegacyId = row.LegacyInventoryId,
                    PartName = row.PartName,
                    PartId = row.PartId,
                    Quantity = row.Quantity,
                });
            }
        }
    }
}

public sealed class WmsUnmappedRowDisplay
{
    public string LegacyId { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
    public string PartId { get; init; } = string.Empty;
    public double Quantity { get; init; }
}