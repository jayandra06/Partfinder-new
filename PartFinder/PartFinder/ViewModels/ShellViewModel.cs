using CommunityToolkit.Mvvm.Input;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class ShellViewModel : ViewModelBase, IShellNavCoordinator
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSetupContext _setupContext;
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IAppStateStore _appState;

    private bool _suppressNavigation;
    private AppPage _lastSelectedPage = AppPage.Templates;

    public ShellViewModel(
        INavigationService navigationService,
        ILocalSetupContext setupContext,
        ITemplateSchemaService templateSchema,
        IAppStateStore appState)
    {
        _navigationService = navigationService;
        _setupContext = setupContext;
        _templateSchema = templateSchema;
        _appState = appState;
        NavigationItems = new ObservableCollection<NavItemViewModel>();
    }

    public ObservableCollection<NavItemViewModel> NavigationItems { get; }

    private NavItemViewModel? _selectedNavigationItem;
    public NavItemViewModel? SelectedNavigationItem
    {
        get => _selectedNavigationItem;
        set
        {
            if (value is not null && !value.IsEnabled)
            {
                return;
            }

            if (!SetProperty(ref _selectedNavigationItem, value) || value is null)
            {
                return;
            }

            _lastSelectedPage = value.Page;
            if (!_suppressNavigation)
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

    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
            {
                OnPropertyChanged(nameof(SidebarToggleGlyph));
            }
        }
    }

    /// <summary>Chevron left when expanded (collapse); chevron right when collapsed (expand).</summary>
    public string SidebarToggleGlyph => IsSidebarCollapsed ? "\uE76B" : "\uE76C";

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
        SyncNavItemLabelsWithSidebar();
    }

    private void SyncNavItemLabelsWithSidebar()
    {
        foreach (var item in NavigationItems)
        {
            item.ShowNavLabel = !IsSidebarCollapsed;
        }
    }

    public async Task InitializeAsync()
    {
        _setupContext.Refresh();
        if (!string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            _appState.CurrentTenant = $"Org {_setupContext.OrgCode}";
        }

        CurrentTenant = _appState.CurrentTenant;
        CurrentUserName = _appState.CurrentUserName;

        var hasMaster = await HasMasterDataTemplateAsync().ConfigureAwait(true);
        RebuildNavigationItems(hasMaster);

        var startPage = hasMaster ? AppPage.Dashboard : AppPage.Templates;
        var startItem = NavigationItems.First(i => i.Page == startPage);
        _lastSelectedPage = startPage;

        _suppressNavigation = true;
        SelectedNavigationItem = startItem;
        _suppressNavigation = false;
        _navigationService.Navigate(startPage);
    }

    public async Task NotifyTemplatesChangedAsync(bool openMasterDataPage = false)
    {
        var hasMaster = await HasMasterDataTemplateAsync().ConfigureAwait(true);
        RebuildNavigationItems(hasMaster);

        NavItemViewModel? pick;
        if (openMasterDataPage && hasMaster)
        {
            pick = NavigationItems.FirstOrDefault(i => i.Page == AppPage.MasterData);
        }
        else
        {
            pick = NavigationItems.FirstOrDefault(i => i.Page == _lastSelectedPage)
                   ?? NavigationItems.FirstOrDefault();
        }

        if (pick is null)
        {
            return;
        }

        _suppressNavigation = true;
        SelectedNavigationItem = pick;
        _suppressNavigation = false;
        _lastSelectedPage = pick.Page;
        _navigationService.Navigate(pick.Page);
    }

    private void RebuildNavigationItems(bool hasMasterData)
    {
        NavigationItems.Clear();
        if (!hasMasterData)
        {
            // Master Data template not created yet: still show main areas so Settings and navigation work.
            // Templates remains the place to create the required "Master Data" template when DB is configured.
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Templates",
                    IconGlyph = "\uE8A5",
                    Page = AppPage.Templates,
                    IsEnabled = true,
                });
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Dashboard",
                    IconGlyph = "\uE80F",
                    Page = AppPage.Dashboard,
                    IsEnabled = true,
                });
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Parts",
                    IconGlyph = "\uE716",
                    Page = AppPage.Parts,
                    IsEnabled = true,
                });
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Settings",
                    IconGlyph = "\uE713",
                    Page = AppPage.Settings,
                    IsEnabled = true,
                });
            SyncNavItemLabelsWithSidebar();
            return;
        }

        NavigationItems.Add(
            new NavItemViewModel
            {
                Label = "Master Data",
                IconGlyph = "\uE8F1",
                Page = AppPage.MasterData,
                IsEnabled = true,
            });
        NavigationItems.Add(
            new NavItemViewModel
            {
                Label = "Dashboard",
                IconGlyph = "\uE80F",
                Page = AppPage.Dashboard,
                IsEnabled = true,
            });
        NavigationItems.Add(
            new NavItemViewModel
            {
                Label = "Parts",
                IconGlyph = "\uE716",
                Page = AppPage.Parts,
                IsEnabled = true,
            });
        NavigationItems.Add(
            new NavItemViewModel
            {
                Label = "Templates",
                IconGlyph = "\uE8A5",
                Page = AppPage.Templates,
                IsEnabled = true,
            });
        NavigationItems.Add(
            new NavItemViewModel
            {
                Label = "Settings",
                IconGlyph = "\uE713",
                Page = AppPage.Settings,
                IsEnabled = true,
            });
        SyncNavItemLabelsWithSidebar();
    }

    private async Task<bool> HasMasterDataTemplateAsync()
    {
        try
        {
            var list = await _templateSchema.GetTemplatesAsync().ConfigureAwait(false);
            return list.Any(
                t => string.Equals(
                    t.Name,
                    MongoTemplateSchemaService.MasterDataTemplateName,
                    StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();
}
