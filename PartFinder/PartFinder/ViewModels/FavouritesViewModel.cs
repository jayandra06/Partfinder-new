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
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;

    public FavouritesViewModel(IFavouriteStore favouriteStore, ITemplateSchemaService templateSchema)
    {
        _favouriteStore = favouriteStore;
        _templateSchema = templateSchema;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        FavouriteTemplates.CollectionChanged += OnFavouriteTemplatesCollectionChanged;
        _favouriteStore.FavouritesChanged += OnFavouritesChanged;
    }

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<FavouriteCardViewModel> FavouriteTemplates { get; } = [];

    // ── Derived state ─────────────────────────────────────────────────────────

    public bool IsEmpty => FavouriteTemplates.Count == 0;

    public bool CanGoPrevious => ActiveIndex > 0;

    public bool CanGoNext => ActiveIndex < FavouriteTemplates.Count - 1;

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
        if (CanGoNext)
        {
            ActiveIndex++;
        }
    }

    [RelayCommand]
    private void GoPrevious()
    {
        if (CanGoPrevious)
        {
            ActiveIndex--;
        }
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
            var card = FavouriteTemplates.FirstOrDefault(c => c.Template.Id == templateId);
            if (card is not null)
            {
                card.IsFavourite = true;
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

    public Task LoadAsync(IReadOnlyList<PartTemplateDefinition> allTemplates)
    {
        var favouriteIds = _favouriteStore.GetAll();

        FavouriteTemplates.Clear();

        foreach (var id in favouriteIds)
        {
            var template = allTemplates.FirstOrDefault(
                t => string.Equals(t.Id, id, StringComparison.Ordinal));

            if (template is not null)
            {
                FavouriteTemplates.Add(new FavouriteCardViewModel(template, isFavourite: true));
            }
        }

        ActiveIndex = 0;
        NotifyAllState();

        return Task.CompletedTask;
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
        foreach (var card in FavouriteTemplates)
        {
            card.IsFavourite = favouriteIds.Contains(card.Template.Id);
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
