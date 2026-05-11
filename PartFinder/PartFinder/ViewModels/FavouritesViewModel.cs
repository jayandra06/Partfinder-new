using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace PartFinder.ViewModels;

public partial class FavouritesViewModel : ViewModelBase
{
    private readonly IFavouriteStore _favouriteStore;
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IMasterDataRecordsService _records;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    // Kept so OnFavouritesChanged can add newly-starred templates without a full reload
    private IReadOnlyList<PartTemplateDefinition> _allTemplates = Array.Empty<PartTemplateDefinition>();

    public FavouritesViewModel(IFavouriteStore favouriteStore, ITemplateSchemaService templateSchema,
        IMasterDataRecordsService records)
    {
        _favouriteStore = favouriteStore;
        _templateSchema = templateSchema;
        _records = records;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        FavouriteTemplates.CollectionChanged += OnFavouriteTemplatesCollectionChanged;
        _favouriteStore.FavouritesChanged += OnFavouritesChanged;
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<FavouriteCardViewModel> FavouriteTemplates { get; } = [];

    // ── Derived state ─────────────────────────────────────────────────────────

    public bool IsEmpty => FavouriteTemplates.Count == 0;

    // Always enabled when there are at least 2 cards — navigation is circular
    public bool CanGoPrevious => FavouriteTemplates.Count > 1;

    public bool CanGoNext => FavouriteTemplates.Count > 1;

    public string IndexLabel =>
        FavouriteTemplates.Count == 0
            ? "0 / 0"
            : $"{ActiveIndex + 1} / {FavouriteTemplates.Count}";

    // ── ActiveIndex ───────────────────────────────────────────────────────────

    private int _activeIndex;

    public int ActiveIndex
    {
        get => _activeIndex;
        set
        {
            var clamped = FavouriteTemplates.Count == 0
                ? 0
                : Math.Clamp(value, 0, FavouriteTemplates.Count - 1);

            if (!SetProperty(ref _activeIndex, clamped))
            {
                return;
            }

            NotifyNavigationState();
        }
    }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised when the user requests to edit a template.
    /// TemplatesPage subscribes and delegates to TemplatesViewModel.
    /// </summary>
    public event EventHandler<string>? EditTemplateRequested;

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void GoNext()
    {
        if (FavouriteTemplates.Count < 2) return;
        // Wrap around: last → first
        ActiveIndex = (ActiveIndex + 1) % FavouriteTemplates.Count;
    }

    [RelayCommand]
    private void GoPrevious()
    {
        if (FavouriteTemplates.Count < 2) return;
        // Wrap around: first → last
        ActiveIndex = (ActiveIndex - 1 + FavouriteTemplates.Count) % FavouriteTemplates.Count;
    }

    [RelayCommand]
    public async Task ToggleFavouriteAsync(string templateId)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return;
        }

        await _favouriteStore.ToggleAsync(templateId).ConfigureAwait(true);

        // If unstarred, remove card from carousel
        if (!_favouriteStore.IsFavourite(templateId))
        {
            var card = FavouriteTemplates.FirstOrDefault(c => c.Template.Id == templateId);
            if (card is not null)
            {
                var removedIndex = FavouriteTemplates.IndexOf(card);
                FavouriteTemplates.Remove(card);

                if (FavouriteTemplates.Count > 0)
                {
                    ActiveIndex = Math.Clamp(removedIndex, 0, FavouriteTemplates.Count - 1);
                }
                else
                {
                    ActiveIndex = 0;
                }
            }
        }
        else
        {
            // Starred — update existing card or add new one
            var card = FavouriteTemplates.FirstOrDefault(c => c.Template.Id == templateId);
            if (card is not null)
            {
                card.IsFavourite = true;
            }
            else
            {
                var template = _allTemplates.FirstOrDefault(t => string.Equals(t.Id, templateId, StringComparison.Ordinal));
                if (template is not null)
                {
                    FavouriteTemplates.Add(new FavouriteCardViewModel(template, isFavourite: true));
                    ActiveIndex = FavouriteTemplates.Count - 1;
                }
            }
        }
    }

    // Manual command property for ToggleFavouriteAsync
    public IAsyncRelayCommand<string?> ToggleFavouriteAsyncCommand =>
        new AsyncRelayCommand<string?>((templateId) => ToggleFavouriteAsync(templateId ?? string.Empty));

    [RelayCommand]
    private async Task DeleteTemplateAsync(string templateId)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            return;
        }

        await _templateSchema.DeleteTemplateAsync(templateId).ConfigureAwait(true);

        var card = FavouriteTemplates.FirstOrDefault(c => c.Template.Id == templateId);
        if (card is not null)
        {
            var removedIndex = FavouriteTemplates.IndexOf(card);
            FavouriteTemplates.Remove(card);

            if (FavouriteTemplates.Count > 0)
            {
                ActiveIndex = Math.Clamp(removedIndex, 0, FavouriteTemplates.Count - 1);
            }
            else
            {
                ActiveIndex = 0;
            }
        }
    }

    // Manual command property for DeleteTemplateAsync
    public IAsyncRelayCommand<string?> DeleteTemplateAsyncCommand =>
        new AsyncRelayCommand<string?>((templateId) => DeleteTemplateAsync(templateId ?? string.Empty));

    [RelayCommand]
    private void BeginEditTemplate(string templateId)
    {
        if (!string.IsNullOrEmpty(templateId))
        {
            EditTemplateRequested?.Invoke(this, templateId);
        }
    }

    // ── Load ──────────────────────────────────────────────────────────────────

    public async Task LoadAsync(IReadOnlyList<PartTemplateDefinition> allTemplates)
    {
        _allTemplates = allTemplates;

        var favouriteIds = _favouriteStore.GetAll();

        FavouriteTemplates.Clear();

        foreach (var id in favouriteIds)
        {
            var template = allTemplates.FirstOrDefault(
                t => string.Equals(t.Id, id, StringComparison.Ordinal));

            if (template is not null)
            {
                // Fetch actual data rows for this template
                IReadOnlyList<MasterDataRowRecord> rows;
                try
                {
                    rows = await _records.GetRowsAsync(template.Id).ConfigureAwait(true);
                }
                catch
                {
                    rows = Array.Empty<MasterDataRowRecord>();
                }

                FavouriteTemplates.Add(new FavouriteCardViewModel(template, isFavourite: true, records: rows));
            }
        }

        ActiveIndex = 0;
        NotifyAllState();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void OnFavouriteTemplatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyAllState();
    }

    private void OnFavouritesChanged(object? sender, EventArgs e)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => OnFavouritesChanged(sender, e));
            return;
        }

        var favouriteIds = _favouriteStore.GetAll();

        // Remove cards that are no longer favourites
        var toRemove = FavouriteTemplates
            .Where(card => !favouriteIds.Contains(card.Template.Id))
            .ToList();

        foreach (var card in toRemove)
        {
            FavouriteTemplates.Remove(card);
        }

        // Add newly-starred templates
        foreach (var id in favouriteIds)
        {
            var existingCard = FavouriteTemplates.FirstOrDefault(c => c.Template.Id == id);
            if (existingCard is not null)
            {
                existingCard.IsFavourite = true;
                continue;
            }

            // New favourite — find template and add card
            var template = _allTemplates.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.Ordinal));
            if (template is not null)
            {
                IReadOnlyList<MasterDataRowRecord> rows;
                try { rows = _records.GetRowsAsync(template.Id).GetAwaiter().GetResult(); }
                catch { rows = Array.Empty<MasterDataRowRecord>(); }
                FavouriteTemplates.Add(new FavouriteCardViewModel(template, isFavourite: true, records: rows));
            }
        }

        // Clamp active index if collection changed
        if (FavouriteTemplates.Count > 0)
        {
            ActiveIndex = Math.Clamp(ActiveIndex, 0, FavouriteTemplates.Count - 1);
        }
        else
        {
            ActiveIndex = 0;
        }
    }

    private void NotifyNavigationState()
    {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IndexLabel));
    }

    private void NotifyAllState()
    {
        OnPropertyChanged(nameof(IsEmpty));
        NotifyNavigationState();
    }
}
