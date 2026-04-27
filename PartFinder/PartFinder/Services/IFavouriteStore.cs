namespace PartFinder.Services;

public interface IFavouriteStore
{
    /// <summary>Raised whenever the in-memory set changes (star or unstar).</summary>
    event EventHandler FavouritesChanged;

    /// <summary>Load favourites from disk into the in-memory set.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns true if the given template ID is currently starred.</summary>
    bool IsFavourite(string templateId);

    /// <summary>Add or remove the template ID from the favourites set and persist.</summary>
    Task ToggleAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>Returns a snapshot of all currently starred template IDs.</summary>
    IReadOnlySet<string> GetAll();
}
