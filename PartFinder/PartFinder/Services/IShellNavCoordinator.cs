namespace PartFinder.Services;

/// <summary>
/// Allows feature pages to refresh shell navigation (e.g. after saving Master Data template).
/// </summary>
public interface IShellNavCoordinator
{
    /// <param name="openMasterDataPage">After the first Master Data template is saved, navigate to Master Data.</param>
    Task NotifyTemplatesChangedAsync(bool openMasterDataPage = false);
}
