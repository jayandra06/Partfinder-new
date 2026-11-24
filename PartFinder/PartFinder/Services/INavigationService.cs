using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Services;

public interface INavigationService
{
    void Initialize(Frame frame);
    bool IsFrameReady { get; }
    Task WaitForFrameReadyAsync(CancellationToken cancellationToken = default);
    bool Navigate(AppPage page, object? parameter = null);
    /// <summary>Drops the previous page and shows <paramref name="page"/> with an empty back stack.</summary>
    bool ResetTo(AppPage page);
    void Clear();
    bool CanGoBack { get; }
    void GoBack();
}
