using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class ShellViewModel : ViewModelBase, IShellNavCoordinator
{
    private readonly INavigationService _navigationService;
    private readonly ILocalSetupContext _setupContext;
    private readonly ITemplateSchemaService _templateSchema;
    private readonly IAppStateStore _appState;
    private readonly ICurrentUserAccessService _access;
    private readonly AdminSessionStore _adminSession;
    private readonly LocalProfileStore _profile;

    private bool _suppressNavigation;
    private AppPage _lastSelectedPage = AppPage.Templates;

    public ShellViewModel(
        INavigationService navigationService,
        ILocalSetupContext setupContext,
        ITemplateSchemaService templateSchema,
        IAppStateStore appState,
        ICurrentUserAccessService access,
        AdminSessionStore adminSession,
        LocalProfileStore profile)
    {
        _navigationService = navigationService;
        _setupContext = setupContext;
        _templateSchema = templateSchema;
        _appState = appState;
        _access = access;
        _adminSession = adminSession;
        _profile = profile;
        _profile.ProfileChanged += OnProfileChanged;
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
        set
        {
            if (SetProperty(ref _currentUserName, value))
            {
                OnPropertyChanged(nameof(CurrentUserInitial));
            }
        }
    }

    public string CurrentUserInitial => string.IsNullOrWhiteSpace(CurrentUserName)
        ? "?"
        : CurrentUserName.Trim()[0].ToString().ToUpperInvariant();

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
        _adminSession.Load();
        _profile.Load();
        if (!string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            _appState.CurrentTenant = $"Org {_setupContext.OrgCode}";
        }

        CurrentTenant = _appState.CurrentTenant;
        CurrentUserName = ResolveDisplayUser();

        await _access.RefreshAsync().ConfigureAwait(true);
        var hasMaster = await HasMasterDataTemplateAsync().ConfigureAwait(true);
        RebuildNavigationItems(hasMaster, _access.Capabilities);

        var startPage = ResolveStartPage(hasMaster, _access.Capabilities);
        var startItem = NavigationItems.First(i => i.Page == startPage);
        _lastSelectedPage = startPage;

        _suppressNavigation = true;
        SelectedNavigationItem = startItem;
        _suppressNavigation = false;
        _navigationService.Navigate(startPage);
    }

    private static AppPage ResolveStartPage(bool hasMasterData, UserAccessCapabilities c)
    {
        if (!c.CanAccessUserManagement)
        {
            return AppPage.MasterData;
        }

        if (!hasMasterData)
        {
            return AppPage.Templates;
        }

        return AppPage.Dashboard;
    }

    public async Task NotifyTemplatesChangedAsync(bool openMasterDataPage = false)
    {
        await _access.RefreshAsync().ConfigureAwait(true);
        var hasMaster = await HasMasterDataTemplateAsync().ConfigureAwait(true);
        RebuildNavigationItems(hasMaster, _access.Capabilities);

        NavItemViewModel? pick;
        if (openMasterDataPage && hasMaster && _access.Capabilities.CanAccessMasterData)
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

    private void RebuildNavigationItems(bool hasMasterData, UserAccessCapabilities c)
    {
        NavigationItems.Clear();

        if ((c.CanAccessMasterData || c.CanAccessParts) && hasMasterData)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Master Data",
                    IconGlyph = "\uE8F1",
                    Page = AppPage.MasterData,
                    IsEnabled = true,
                });
        }

        if (c.CanAccessDashboard)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Dashboard",
                    IconGlyph = "\uE80F",
                    Page = AppPage.Dashboard,
                    IsEnabled = true,
                });
        }

        if (c.CanAccessTemplates)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Inventory",
                    IconGlyph = "\uE80A",
                    Page = AppPage.Inventory,
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
                    Label = "Worksheet Relations",
                    IconGlyph = "\uE8EC",
                    Page = AppPage.WorksheetRelations,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "QR Codes",
                    IconGlyph = "\uED14",
                    Page = AppPage.QrCodeManager,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Alerts",
                    IconGlyph = "\uE7E7",
                    Page = AppPage.Alerts,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Activities",
                    IconGlyph = "\uE823",
                    Page = AppPage.Audit,
                    IsEnabled = true,
                });
        }

        if (c.CanAccessUserManagement)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "User Management",
                    IconGlyph = "\uE77B",
                    Page = AppPage.UserManagement,
                    IsEnabled = true,
                });
        }

        if (c.CanAccessSettings)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Settings",
                    IconGlyph = "\uE713",
                    Page = AppPage.Settings,
                    IsEnabled = true,
                });
        }

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

    [RelayCommand]
    private void OpenAccountSettings() => _navigationService.Navigate(AppPage.Settings);

    [RelayCommand]
    private void SignOutSession()
    {
        _adminSession.Clear();
        SetupPaths.ClearAllSetupStateFiles();
        _setupContext.Refresh();
        CurrentUserName = ResolveDisplayUser();
        Application.Current.Exit();
    }

    private string ResolveDisplayUser()
    {
        var localName = _profile.DisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(localName))
        {
            return localName;
        }

        if (!string.IsNullOrWhiteSpace(_adminSession.Email))
        {
            return _adminSession.Email!;
        }

        if (!string.IsNullOrWhiteSpace(_setupContext.AdminEmail))
        {
            return _setupContext.AdminEmail!;
        }

        return _appState.CurrentUserName;
    }

    private void OnProfileChanged()
    {
        _profile.Load();
        CurrentUserName = ResolveDisplayUser();
    }
}
