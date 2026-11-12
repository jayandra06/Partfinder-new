using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PartFinder.Services;
using System.Collections.ObjectModel;
using Windows.Storage;

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
    private readonly MongoAlertsService _alertsService;
    private readonly IThemeService _themeService;
    private readonly DashboardViewModel _dashboard;
    private readonly CatalogBrowseSessionStore _catalogSession;

    private bool _suppressNavigation;
    private AppPage _lastSelectedPage = AppPage.Dashboard;

    public ShellViewModel(
        INavigationService navigationService,
        ILocalSetupContext setupContext,
        ITemplateSchemaService templateSchema,
        IAppStateStore appState,
        ICurrentUserAccessService access,
        AdminSessionStore adminSession,
        LocalProfileStore profile,
        MongoAlertsService alertsService,
        IThemeService themeService,
        DashboardViewModel dashboard)
    {
        _navigationService = navigationService;
        _setupContext = setupContext;
        _templateSchema = templateSchema;
        _appState = appState;
        _access = access;
        _adminSession = adminSession;
        _profile = profile;
        _alertsService = alertsService;
        _themeService = themeService;
        _dashboard = dashboard;
        _themeService.ThemeChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ThemeToggleGlyph));
            OnPropertyChanged(nameof(ThemeToggleTooltip));
        };
        _profile.ProfileChanged += OnProfileChanged;
        NavigationItems = new ObservableCollection<NavItemViewModel>();
    }

    public string ThemeToggleGlyph => _themeService.IsDark ? "\uE706" : "\uE708";

    public string ThemeToggleTooltip =>
        _themeService.IsDark ? "Switch to light theme" : "Switch to dark theme";

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

            foreach (var item in NavigationItems)
            {
                item.IsSelected = ReferenceEquals(item, value);
            }

            _lastSelectedPage = value.Page;
            if (!_suppressNavigation)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellViewModel] Navigating to {value.Page}");
                _navigationService.Navigate(value.Page);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ShellViewModel] Navigation to {value.Page} suppressed by flag.");
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
        set
        {
            if (SetProperty(ref _currentTenant, value))
            {
                OnPropertyChanged(nameof(CurrentTenantDisplay));
            }
        }
    }

    public string CurrentTenantDisplay =>
        string.IsNullOrWhiteSpace(_currentTenant) ? string.Empty : $"Org - {_currentTenant}";

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

    private string _currentUserDepartment = string.Empty;
    public string CurrentUserDepartment
    {
        get => _currentUserDepartment;
        set => SetProperty(ref _currentUserDepartment, value);
    }

    private string _currentOrgName = string.Empty;
    public string CurrentOrgName
    {
        get => _currentOrgName;
        set => SetProperty(ref _currentOrgName, value);
    }

    private string _currentOrgPlan = string.Empty;
    public string CurrentOrgPlan
    {
        get => _currentOrgPlan;
        set => SetProperty(ref _currentOrgPlan, value);
    }

    private ImageSource? _currentUserAvatarSource;
    public ImageSource? CurrentUserAvatarSource
    {
        get => _currentUserAvatarSource;
        set
        {
            if (SetProperty(ref _currentUserAvatarSource, value))
            {
                OnPropertyChanged(nameof(HasUserAvatar));
            }
        }
    }

    public bool HasUserAvatar => CurrentUserAvatarSource is not null;

    private bool _hasUnreadAlerts;
    public bool HasUnreadAlerts
    {
        get => _hasUnreadAlerts;
        set => SetProperty(ref _hasUnreadAlerts, value);
    }

    private int _unreadNotificationCount;
    public int UnreadNotificationCount
    {
        get => _unreadNotificationCount;
        private set
        {
            if (!SetProperty(ref _unreadNotificationCount, value))
                return;

            HasUnreadAlerts = value > 0;
            OnPropertyChanged(nameof(UnreadNotificationBadgeText));
        }
    }

    public string UnreadNotificationBadgeText =>
        UnreadNotificationCount > 99 ? "99+" : UnreadNotificationCount.ToString();

    public async Task RefreshAlertBadgeAsync()
    {
        try
        {
            UnreadNotificationCount = await _alertsService.GetUnreadCountAsync().ConfigureAwait(true);
        }
        catch
        {
            UnreadNotificationCount = 0;
        }
    }

    public async Task MarkNotificationsSeenAsync()
    {
        try
        {
            await _alertsService.MarkAllActiveAsReadAsync().ConfigureAwait(true);
        }
        catch
        {
            // ignore — badge refresh still reflects DB state
        }

        await RefreshAlertBadgeAsync().ConfigureAwait(true);
    }

    public async Task InitializeAsync()
    {
        _lastSelectedPage = AppPage.Dashboard;
        _setupContext.Refresh();
        _adminSession.Load();
        _profile.Load();

        // Set CurrentTenant: always show org code in top-left sidebar
        if (!string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            _appState.CurrentTenant = _setupContext.OrgCode!;
        }

        CurrentTenant = _appState.CurrentTenant;
        CurrentUserName = ResolveDisplayUser();
        CurrentUserDepartment = _profile.Department ?? string.Empty;

        // If OrgDisplayName was not set by MainWindow (e.g. after logout+restart),
        // fall back to OrgCode so sidebar and settings always show something meaningful.
        if (string.IsNullOrWhiteSpace(_appState.OrgDisplayName) &&
            !string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            _appState.OrgDisplayName = _setupContext.OrgCode!;
        }

        CurrentOrgName = _appState.OrgDisplayName;
        CurrentOrgPlan = _appState.OrgPlan;

        var avatarTask = LoadAvatarAsync(_profile.AvatarPath);
        var alertsTask = RefreshAlertBadgeAsync();
        var accessTask = _access.RefreshAsync();
        var masterTask = HasMasterDataTemplateAsync();
        _ = _dashboard.WarmUpAsync();
        await Task.WhenAll(avatarTask, alertsTask, accessTask, masterTask).ConfigureAwait(true);
        var hasMaster = await masterTask.ConfigureAwait(true);
        RebuildNavigationItems(hasMaster, _access.Capabilities);

        var startPage = ResolveStartPage(hasMaster, _access.Capabilities);
        var startItem = NavigationItems.FirstOrDefault(i => i.Page == startPage)
                        ?? NavigationItems.FirstOrDefault();
        if (startItem is null)
        {
            return;
        }

        startPage = startItem.Page;
        _lastSelectedPage = startPage;

        if (!_navigationService.IsFrameReady)
        {
            try
            {
                await _navigationService.WaitForFrameReadyAsync().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[ShellViewModel] Content frame not ready; home navigation skipped.");
                return;
            }
        }

        ApplyNavigationSelection(startItem, navigate: false);
        _navigationService.ResetTo(startPage);
    }

    /// <summary>Top-nav click: always updates selection and navigates (even if already selected).</summary>
    public void NavigateFromNavItem(NavItemViewModel item)
    {
        if (!item.IsEnabled)
        {
            return;
        }

        ApplyNavigationSelection(item, navigate: true);
    }

    private void ApplyNavigationSelection(NavItemViewModel item, bool navigate)
    {
        foreach (var navItem in NavigationItems)
        {
            navItem.IsSelected = ReferenceEquals(navItem, item);
        }

        _selectedNavigationItem = item;
        OnPropertyChanged(nameof(SelectedNavigationItem));
        _lastSelectedPage = item.Page;

        if (navigate && !_suppressNavigation)
        {
            _navigationService.Navigate(item.Page);
        }
    }

    private static AppPage ResolveStartPage(bool hasMasterData, UserAccessCapabilities c)
    {
        if (c.CanAccessDashboard)
        {
            return AppPage.Dashboard;
        }

        if (c.CanAccessTemplates)
        {
            return AppPage.Templates;
        }

        if ((c.CanAccessMasterData || c.CanAccessParts) && hasMasterData)
        {
            return AppPage.MasterData;
        }

        if (c.CanAccessSettings)
        {
            return AppPage.Settings;
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

        if (c.CanAccessDashboard)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Home",
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
                    Label = "Warehouse Management",
                    IconGlyph = "\uE179",
                    Page = AppPage.Inventory,
                    IsEnabled = true,
                });
        }

        if ((c.CanAccessMasterData || c.CanAccessParts) && hasMasterData)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Master Data",
                    IconGlyph = "\uF0E2",
                    Page = AppPage.MasterData,
                    IsEnabled = true,
                });
        }

        if (c.CanAccessTemplates)
        {
            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Analytics",
                    IconGlyph = "\uE8A5",
                    Page = AppPage.Templates,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Linked Templates",
                    IconGlyph = "\uE8A7",
                    Page = AppPage.LinkedTemplates,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Link Templates",
                    IconGlyph = "\uE72A",
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
                    IconGlyph = "\uEA8F",
                    Page = AppPage.Alerts,
                    IsEnabled = true,
                });

            NavigationItems.Add(
                new NavItemViewModel
                {
                    Label = "Activities",
                    IconGlyph = "\uE81C",
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
                    IconGlyph = "\uE716",
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

    }

    private async Task<bool> HasMasterDataTemplateAsync()
    {
        try
        {
            var list = await _templateSchema.GetTemplatesAsync().ConfigureAwait(false);
            return list.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void GoBack() => _navigationService.GoBack();

    [RelayCommand]
    private void ToggleTheme() => _themeService.Toggle();

    [RelayCommand]
    private void OpenAccountSettings() => _navigationService.Navigate(AppPage.Settings);

    [RelayCommand]
    private void SignOutSession()
    {
        _lastSelectedPage = AppPage.Dashboard;
        _navigationService.Clear();
        _adminSession.Clear();
        // Do NOT delete setup-state.json — it holds orgCode/dbUri needed after re-login
        _setupContext.Refresh();
        CurrentUserName = ResolveDisplayUser();
        if (App.MainAppWindow is MainWindow main)
        {
            main.ResetToSetup();
        }
        else
        {
            Application.Current.Exit();
        }
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
        CurrentUserDepartment = _profile.Department ?? string.Empty;
        _ = LoadAvatarAsync(_profile.AvatarPath);
    }

    private async Task LoadAvatarAsync(string? path)
    {
        // If user has a custom saved photo, load that
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using var stream = await file.OpenReadAsync();
                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                CurrentUserAvatarSource = bitmap;
                return;
            }
            catch { /* fall through to default */ }
        }

        // No custom photo — load default Assets/profile.jpg via stream so it's fully loaded before binding
        try
        {
            var defaultFile = await StorageFile.GetFileFromApplicationUriAsync(
                new Uri("ms-appx:///Assets/profile.jpg"));
            using var defaultStream = await defaultFile.OpenReadAsync();
            var defaultBitmap = new BitmapImage();
            await defaultBitmap.SetSourceAsync(defaultStream);
            CurrentUserAvatarSource = defaultBitmap;
        }
        catch
        {
            CurrentUserAvatarSource = null;
        }
    }
}
