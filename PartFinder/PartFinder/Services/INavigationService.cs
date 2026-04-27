using Microsoft.UI.Xaml.Controls;

namespace PartFinder.Services;

public interface INavigationService
{
    void Initialize(Frame frame);
    bool Navigate(AppPage page, object? parameter = null);
    bool CanGoBack { get; }
    void GoBack();
}
