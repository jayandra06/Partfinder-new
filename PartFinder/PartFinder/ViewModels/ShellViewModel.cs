using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class ShellViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;

    public ShellViewModel(INavigationService navigationService, IAppStateStore appStateStore)
    {
        _navigationService = navigationService;
        CurrentTenant = appStateStore.CurrentTenant;
        CurrentUserName = appStateStore.CurrentUserName;

        NavigationItems =
        [
            new NavItemViewModel { Label = "Dashboard", IconGlyph = "\uE80F", Page = AppPage.Dashboard },
            new NavItemViewModel { Label = "Parts", IconGlyph = "\uE716", Page = AppPage.Parts },
            new NavItemViewModel { Label = "Templates", IconGlyph = "\uE8A5", Page = AppPage.Templates }
        ];

        SelectedNavigationItem = NavigationItems[0];
    }

    public ObservableCollection<NavItemViewModel> NavigationItems { get; }

    private NavItemViewModel? _selectedNavigationItem;
    public NavItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (SetProperty(ref _selectedNavigationItem, value) && value is not null)
            {
                _navigationService.Navigate(value.Page);
            }
        }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    private string _currentTenant = string.Empty;
    public string CurrentTenant
    {
        get => _currentTenant;
        set => SetProperty(ref _currentTenant, value);
    }

    private string _currentUserName = string.Empty;
    public string CurrentUserName
    {
        get => _currentUserName;
        set => SetProperty(ref _currentUserName, value);
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
