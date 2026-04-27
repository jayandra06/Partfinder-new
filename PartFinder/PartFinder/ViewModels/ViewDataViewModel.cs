using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class ViewDataViewModel : ViewModelBase
{
    private readonly BackendApiClient _api;

    public ViewDataViewModel(BackendApiClient api)
    {
        _api = api;
    }

    public ObservableCollection<TemplateLiteDto> Templates { get; } = [];
    public ObservableCollection<ViewDataRowItem> Rows { get; } = [];
    public ObservableCollection<DisplayPair> HoveredRowPrimaryDetails { get; } = [];
    public ObservableCollection<DisplayPair> HoveredRowLinkedDetails { get; } = [];
    public ObservableCollection<RelationDetailSection> HoveredRelationSections { get; } = [];

    [ObservableProperty]
    private TemplateLiteDto? _selectedTemplate;

    [ObservableProperty]
    private ViewDataRowItem? _hoveredRow;

    [ObservableProperty]
    private bool _isFlyoutOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _flyoutTitle = "Vendor Information";

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var (ok, error, templates) = await _api.GetTemplatesLiteAsync(ct).ConfigureAwait(true);
            if (!ok)
            {
                ErrorMessage = error ?? "Failed to load templates.";
                return;
            }

            Templates.Clear();
            foreach (var t in templates)
            {
                Templates.Add(t);
            }

            SelectedTemplate = Templates.FirstOrDefault();
            await LoadViewDataAsync(ct).ConfigureAwait(true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedTemplateChanged(TemplateLiteDto? value)
    {
        _ = LoadViewDataAsync();
    }

    partial void OnHoveredRowChanged(ViewDataRowItem? value)
    {
        HoveredRowPrimaryDetails.Clear();
        HoveredRowLinkedDetails.Clear();
        HoveredRelationSections.Clear();

        if (value is null)
        {
            FlyoutTitle = "Vendor Information";
            IsFlyoutOpen = false;
            return;
        }

        foreach (var kv in value.Row.Cells.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            HoveredRowPrimaryDetails.Add(new DisplayPair(kv.Key, kv.Value));
        }

        var orderedRelations = value.Row.LinkedData
            .OrderByDescending(kv => kv.Value.Matched)
            .ThenBy(kv => kv.Value.MenuLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var firstLinked = orderedRelations.Select(kv => kv.Value).FirstOrDefault(v => v.Matched);
        if (firstLinked is not null)
        {
            FlyoutTitle = string.IsNullOrWhiteSpace(firstLinked.MenuLabel) ? "Vendor Information" : firstLinked.MenuLabel;
            foreach (var kv in firstLinked.DisplayValues.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                HoveredRowLinkedDetails.Add(new DisplayPair(kv.Key, kv.Value));
            }
        }
        else
        {
            FlyoutTitle = "Vendor Information";
            HoveredRowLinkedDetails.Add(new DisplayPair("Status", "No vendor linked"));
        }

        foreach (var relation in orderedRelations)
        {
            var details = relation.Value.DisplayValues
                .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => new DisplayPair(kv.Key, kv.Value))
                .ToList();
            if (details.Count == 0)
            {
                details.Add(new DisplayPair("Status", relation.Value.Matched ? "Matched" : "No vendor linked"));
            }

            HoveredRelationSections.Add(
                new RelationDetailSection
                {
                    Title = string.IsNullOrWhiteSpace(relation.Value.MenuLabel) ? relation.Key : relation.Value.MenuLabel,
                    IsMatched = relation.Value.Matched,
                    Details = details,
                });
        }

        IsFlyoutOpen = true;
    }

    [RelayCommand]
    private async Task LoadViewDataAsync(CancellationToken ct = default)
    {
        if (SelectedTemplate is null)
        {
            Rows.Clear();
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var (ok, error, rows) = await _api.GetViewDataAsync(SelectedTemplate.Id, ct).ConfigureAwait(true);
            if (!ok)
            {
                ErrorMessage = error ?? "Failed to load view data.";
                return;
            }

            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(ViewDataRowItem.FromDto(row));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void CloseFlyout()
    {
        IsFlyoutOpen = false;
        HoveredRow = null;
    }
}

public sealed record DisplayPair(string Key, string Value)
{
    public string DisplayText => $"{Key}: {Value}";
}

public sealed class ViewDataRowItem
{
    public required EnrichedRowDto Row { get; init; }
    public required string PrimaryPreview { get; init; }
    public required int MatchedRelations { get; init; }
    public required int TotalRelations { get; init; }

    public static ViewDataRowItem FromDto(EnrichedRowDto row)
    {
        var preview = string.Join(
            " | ",
            row.Cells
                .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .Select(kv => $"{kv.Key}: {kv.Value}"));
        var total = row.LinkedData.Count;
        var matched = row.LinkedData.Values.Count(v => v.Matched);
        return new ViewDataRowItem
        {
            Row = row,
            PrimaryPreview = string.IsNullOrWhiteSpace(preview) ? "No primary columns" : preview,
            MatchedRelations = matched,
            TotalRelations = total,
        };
    }
}

public sealed class RelationDetailSection
{
    public required string Title { get; init; }
    public required bool IsMatched { get; init; }
    public required List<DisplayPair> Details { get; init; }
    public string MatchLabel => IsMatched ? "Matched" : "Not linked";
}
