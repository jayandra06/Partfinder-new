using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PartFinder.ViewModels;

public partial class UserManagementViewModel : PaginatedViewModelBase
{
    private readonly IOrgUserDirectoryService _users;
    private readonly ITemplateSchemaService _templates;
    private readonly ILocalSetupContext _setupContext;
    private readonly LocalProfileStore _profile;
    private readonly ActivityLogger _activityLogger;
    private bool _suppressGrantAllSync;

    public ObservableCollection<OrgAppUserSummary> Users { get; } = [];
    public ObservableCollection<TemplatePickItemViewModel> TemplateChoices { get; } = [];
    public ObservableCollection<TemplatePickItemViewModel> SelectedTemplateAccessItems { get; } = [];

    public string[] RoleOptions { get; } = ["Admin", "Employee"];

    [ObservableProperty]
    private string inviteName = string.Empty;

    [ObservableProperty]
    private string inviteEmail = string.Empty;

    [ObservableProperty]
    private string inviteRole = "Employee";

    [ObservableProperty]
    private bool invitePartsAllTemplates;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    // Granular Permissions (for Invite/Edit)
    [ObservableProperty] private bool inviteCanAddTemplate;
    [ObservableProperty] private bool inviteCanViewTemplate;
    [ObservableProperty] private bool inviteCanEditTemplate;
    [ObservableProperty] private bool inviteCanDeleteTemplate;

    [ObservableProperty] private bool inviteCanCopyMasterData;
    [ObservableProperty] private bool inviteCanViewMasterData;
    [ObservableProperty] private bool inviteCanEditMasterData;
    [ObservableProperty] private bool inviteCanAddMasterData;
    [ObservableProperty] private bool inviteCanDeleteMasterData;

    [ObservableProperty]
    private bool isEditMode;

    [ObservableProperty]
    private TemplatePickItemViewModel? templatePickerHighlight;

    private string? _editUserId;

    private List<OrgAppUserSummary> _allUsers = [];
    private readonly List<OrgAppUserSummary> _filteredUsers = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public string[] RoleFilters { get; } = ["All", "Admin", "Employee"];

    [ObservableProperty]
    private string selectedRoleFilter = "All";

    partial void OnSelectedRoleFilterChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilter();
    }

    public string[] StatusFilters { get; } = ["All Status", "Active", "Pending"];

    [ObservableProperty]
    private string selectedStatusFilter = "All Status";

    partial void OnSelectedStatusFilterChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilter();
    }

    private int _totalUsersCount;
    public int TotalUsersCount
    {
        get => _totalUsersCount;
        private set => SetProperty(ref _totalUsersCount, value);
    }

    private int _activeUsersCount;
    public int ActiveUsersCount
    {
        get => _activeUsersCount;
        private set => SetProperty(ref _activeUsersCount, value);
    }

    private int _adminUsersCount;
    public int AdminUsersCount
    {
        get => _adminUsersCount;
        private set => SetProperty(ref _adminUsersCount, value);
    }

    private int _pendingUsersCount;
    public int PendingUsersCount
    {
        get => _pendingUsersCount;
        private set => SetProperty(ref _pendingUsersCount, value);
    }

    private void NotifyKpiChanges()
    {
        TotalUsersCount = _allUsers.Count;
        ActiveUsersCount = _allUsers.Count(u => u.Status == "Active");
        AdminUsersCount = _allUsers.Count(u => u.Role == "Admin");
        PendingUsersCount = _allUsers.Count(u => u.Status == "Pending");
    }

    partial void OnSearchQueryChanged(string value)
    {
        ResetPaginationPage();
        ApplyFilter();
    }

    protected override void OnPaginationRefreshRequested() =>
        RefreshPage(Users, _filteredUsers);

    private void ApplyFilter()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var roleFilter = SelectedRoleFilter ?? "All";
        var statusFilter = SelectedStatusFilter ?? "All Status";

        _filteredUsers.Clear();
        foreach (var u in _allUsers)
        {
            var matchesQuery = string.IsNullOrEmpty(query)
                               || (u.Name != null && u.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                               || (u.Email != null && u.Email.Contains(query, StringComparison.OrdinalIgnoreCase));

            var matchesRole = string.Equals(roleFilter, "All", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(u.Role, roleFilter, StringComparison.OrdinalIgnoreCase);

            var matchesStatus = string.Equals(statusFilter, "All Status", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(u.Status, statusFilter, StringComparison.OrdinalIgnoreCase);

            if (matchesQuery && matchesRole && matchesStatus)
            {
                _filteredUsers.Add(u);
            }
        }

        CommitFilterPage(_filteredUsers, Users);
    }

    public bool IsEmployeeRole =>
        string.Equals(InviteRole, "Employee", StringComparison.OrdinalIgnoreCase);

    public bool IsAdminRole =>
        string.Equals(InviteRole, "Admin", StringComparison.OrdinalIgnoreCase);

    partial void OnInviteRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsEmployeeRole));
        OnPropertyChanged(nameof(IsAdminRole));
        OnPropertyChanged(nameof(CanPickSpecificTemplates));

        if (IsAdminRole)
        {
            ApplyAdminInviteDefaults();
        }
    }

    /// <summary>When false, template checklist is read-only (all templates granted).</summary>
    public bool CanPickSpecificTemplates => IsEmployeeRole && !InvitePartsAllTemplates;

    public bool HasTemplateChoices => TemplateChoices.Count > 0;

    public UserManagementViewModel(
        IOrgUserDirectoryService users,
        ITemplateSchemaService templates,
        ILocalSetupContext setupContext,
        LocalProfileStore profile,
        ActivityLogger activityLogger)
    {
        _users = users;
        _templates = templates;
        _setupContext = setupContext;
        _profile = profile;
        _activityLogger = activityLogger;
    }

    partial void OnInvitePartsAllTemplatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPickSpecificTemplates));

        if (_suppressGrantAllSync)
        {
            return;
        }

        if (value)
        {
            ApplySelectAllTemplates(updateGrantAllFlag: false);
        }

        RefreshSelectedTemplateAccessItems();
    }

    partial void OnTemplatePickerHighlightChanged(TemplatePickItemViewModel? value)
    {
        if (value is null)
        {
            return;
        }

        value.IsSelected = true;
        TemplatePickerHighlight = null;
        RefreshSelectedTemplateAccessItems();
        SyncGrantAllCheckboxFromSelection();
    }

    [RelayCommand]
    private void SelectAllTemplates()
    {
        ApplySelectAllTemplates(updateGrantAllFlag: true);
    }

    [RelayCommand]
    private void ClearTemplateSelection()
    {
        _suppressGrantAllSync = true;
        foreach (var template in TemplateChoices)
        {
            template.IsSelected = false;
        }

        InvitePartsAllTemplates = false;
        _suppressGrantAllSync = false;
        RefreshSelectedTemplateAccessItems();
    }

    [RelayCommand]
    private void RemoveTemplateAccess(TemplatePickItemViewModel item)
    {
        item.IsSelected = false;
        RefreshSelectedTemplateAccessItems();
        SyncGrantAllCheckboxFromSelection();
    }

    private void ApplySelectAllTemplates(bool updateGrantAllFlag)
    {
        _suppressGrantAllSync = true;
        foreach (var template in TemplateChoices)
        {
            template.IsSelected = true;
        }

        if (updateGrantAllFlag)
        {
            InvitePartsAllTemplates = true;
        }

        _suppressGrantAllSync = false;
        RefreshSelectedTemplateAccessItems();
    }

    private void SyncGrantAllCheckboxFromSelection()
    {
        if (TemplateChoices.Count == 0)
        {
            return;
        }

        var allSelected = TemplateChoices.All(x => x.IsSelected);
        if (allSelected == InvitePartsAllTemplates)
        {
            return;
        }

        _suppressGrantAllSync = true;
        InvitePartsAllTemplates = allSelected;
        _suppressGrantAllSync = false;
        OnPropertyChanged(nameof(CanPickSpecificTemplates));
    }

    private void RefreshSelectedTemplateAccessItems()
    {
        SelectedTemplateAccessItems.Clear();
        foreach (var item in TemplateChoices.Where(x => x.IsSelected).OrderBy(x => x.Name))
        {
            SelectedTemplateAccessItems.Add(item);
        }

        OnPropertyChanged(nameof(HasSelectedTemplateAccess));
    }

    public bool HasSelectedTemplateAccess => SelectedTemplateAccessItems.Count > 0;

    private void WireTemplateChoice(TemplatePickItemViewModel item)
    {
        item.PropertyChanged += OnTemplateChoicePropertyChanged;
    }

    private void ClearTemplateChoiceHandlers()
    {
        foreach (var item in TemplateChoices)
        {
            item.PropertyChanged -= OnTemplateChoicePropertyChanged;
        }
    }

    private void OnTemplateChoicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TemplatePickItemViewModel.IsSelected))
        {
            return;
        }

        RefreshSelectedTemplateAccessItems();
        SyncGrantAllCheckboxFromSelection();
    }

    private async Task LoadTemplateChoicesAsync(
        IReadOnlyList<string>? preselectedIds,
        CancellationToken cancellationToken)
    {
        ClearTemplateChoiceHandlers();
        TemplateChoices.Clear();
        SelectedTemplateAccessItems.Clear();

        var all = await _templates.GetTemplatesAsync(cancellationToken).ConfigureAwait(true);
        foreach (var t in all.OrderBy(x => x.Name))
        {
            if (string.Equals(
                    t.Id,
                    MongoTemplateSchemaService.MasterDataTemplateId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var item = new TemplatePickItemViewModel
            {
                TemplateId = t.Id,
                Name = t.Name,
                IsSelected = preselectedIds?.Contains(t.Id, StringComparer.OrdinalIgnoreCase) == true,
            };
            WireTemplateChoice(item);
            TemplateChoices.Add(item);
        }

        OnPropertyChanged(nameof(HasTemplateChoices));
        RefreshSelectedTemplateAccessItems();
    }

    public async Task LoadUsersAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            _setupContext.Refresh();
            var orgCode = _setupContext.OrgCode?.Trim();
            var merged = new Dictionary<string, OrgAppUserSummary>(StringComparer.OrdinalIgnoreCase);
            string? orgDisplayName = null;
            SetupStatusResult? status = null;

            if (!string.IsNullOrWhiteSpace(orgCode))
            {
                (status, _) = await SetupApiClient.StatusAsync(orgCode).ConfigureAwait(true);
                orgDisplayName = status?.OrganizationName?.Trim();
                if (string.IsNullOrWhiteSpace(orgDisplayName))
                {
                    orgDisplayName = null;
                }
            }

            void MergeUsers(IEnumerable<OrgAppUserSummary> users)
            {
                foreach (var user in users)
                {
                    if (string.IsNullOrWhiteSpace(user.Email))
                    {
                        continue;
                    }

                    var key = user.Email.Trim();
                    if (merged.TryGetValue(key, out var existing))
                    {
                        merged[key] = PickPreferredUser(existing, user, orgDisplayName, orgCode);
                    }
                    else
                    {
                        merged[key] = NormalizeUserDisplayName(user, orgDisplayName, orgCode);
                    }
                }
            }

            MergeUsers(await _users.ListUsersAsync().ConfigureAwait(true));

            if (!string.IsNullOrWhiteSpace(orgCode) && status is not null)
            {
                if (status.OrgAppUsers is { Count: > 0 })
                {
                    MergeUsers(SetupApiClient.MapOrgAppUserItems(status.OrgAppUsers));
                }

                var tenantUri = status?.OrgDatabaseUri?.Trim();
                if (!string.IsNullOrWhiteSpace(tenantUri))
                {
                    MergeUsers(await _users.ListUsersFromTenantUriAsync(tenantUri, orgCode)
                        .ConfigureAwait(true));
                }

                var (ok, apiErr, apiUsers) = await SetupApiClient.ListOrgUsersAsync(orgCode)
                    .ConfigureAwait(true);
                if (ok && apiUsers is not null)
                {
                    MergeUsers(apiUsers);
                }
                else if (!ok && !IsListUsersEndpointUnavailable(apiErr))
                {
                    StatusMessage = apiErr ?? string.Empty;
                }
            }

            _allUsers = merged.Values
                .Select(u => NormalizeUserDisplayName(u, orgDisplayName, orgCode))
                .OrderByDescending(u => u.InvitedAtUtc)
                .ToList();

            await ReconcileStoredUserNamesAsync(orgDisplayName, orgCode).ConfigureAwait(true);
            await ApplyLocalProfileDisplayNamesAsync().ConfigureAwait(true);
            NotifyKpiChanges();
            ApplyFilter();
            if (_allUsers.Count == 0)
            {
                _setupContext.Refresh();
                if (string.IsNullOrWhiteSpace(_setupContext.OrgCode))
                {
                    StatusMessage =
                        "Organisation is not configured on this device. Complete setup with your organisation code.";
                }
                else
                {
                    StatusMessage =
                        "No users found yet. Invite someone from Invite User, or check internet connection and LicenseApi:BaseUrl.";
                }
            }
            else
            {
                StatusMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsListUsersEndpointUnavailable(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return true;
        }

        return error.Contains("list-users", StringComparison.OrdinalIgnoreCase)
               || error.Contains("Cannot POST", StringComparison.OrdinalIgnoreCase)
               || error.Contains("404", StringComparison.OrdinalIgnoreCase)
               || error.Contains("Not Found", StringComparison.OrdinalIgnoreCase);
    }

    public async Task PrepareInviteDialogAsync(CancellationToken cancellationToken = default)
    {
        IsEditMode = false;
        _editUserId = null;
        InviteName = string.Empty;
        InviteEmail = string.Empty;
        InviteRole = "Employee";

        InviteCanAddTemplate = false;
        InviteCanViewTemplate = false;
        InviteCanEditTemplate = false;
        InviteCanDeleteTemplate = false;
        InviteCanCopyMasterData = false;
        InviteCanViewMasterData = false;
        InviteCanEditMasterData = false;
        InviteCanAddMasterData = false;
        InviteCanDeleteMasterData = false;

        await LoadTemplateChoicesAsync(preselectedIds: null, cancellationToken).ConfigureAwait(true);

        _suppressGrantAllSync = true;
        InvitePartsAllTemplates = false;
        _suppressGrantAllSync = false;
    }

    public async Task PrepareEditDialogAsync(OrgAppUserSummary user, CancellationToken cancellationToken = default)
    {
        IsEditMode = true;
        _editUserId = user.Id;
        InviteName = user.Name;
        InviteEmail = user.Email;
        InviteRole = user.Role;

        InviteCanAddTemplate = user.TemplatePermissions?.Add ?? false;
        InviteCanViewTemplate = user.TemplatePermissions?.View ?? false;
        InviteCanEditTemplate = user.TemplatePermissions?.Edit ?? false;
        InviteCanDeleteTemplate = user.TemplatePermissions?.Delete ?? false;

        InviteCanCopyMasterData = user.MasterDataPermissions?.Copy ?? false;
        InviteCanViewMasterData = user.MasterDataPermissions?.View ?? false;
        InviteCanEditMasterData = user.MasterDataPermissions?.Edit ?? false;
        InviteCanAddMasterData = user.MasterDataPermissions?.Add ?? false;
        InviteCanDeleteMasterData = user.MasterDataPermissions?.Delete ?? false;

        await LoadTemplateChoicesAsync(user.AllowedTemplateIds, cancellationToken).ConfigureAwait(true);

        _suppressGrantAllSync = true;
        InvitePartsAllTemplates = user.PartsAllTemplates;
        if (user.PartsAllTemplates)
        {
            ApplySelectAllTemplates(updateGrantAllFlag: false);
        }

        _suppressGrantAllSync = false;
        RefreshSelectedTemplateAccessItems();
    }

    public async Task<(string? Error, SetupInviteUserResponse? Result)> InviteAsync()
    {
        var name = InviteName.Trim();
        var email = InviteEmail.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return ("Enter a name.", null);
        }

        if (string.IsNullOrEmpty(email) || !email.Contains('@'))
        {
            return ("Enter a valid email address.", null);
        }

        var role = InviteRole.Trim();
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            return ("Select a role.", null);
        }

        _setupContext.Refresh();
        if (string.IsNullOrWhiteSpace(_setupContext.OrgCode))
        {
            return ("Organisation code is not available in local setup.", null);
        }

        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            ApplyAdminInviteDefaults();
        }

        var partsAll = isAdmin || InvitePartsAllTemplates;
        var allowed = TemplateChoices
            .Where(x => x.IsSelected)
            .Select(x => x.TemplateId)
            .ToList();
        if (!isAdmin && !partsAll && allowed.Count == 0)
        {
            return ("Select at least one part template, or enable \"Grant access to all templates\".", null);
        }

        var templatePerms = isAdmin ? CreateFullTemplatePermissions() : new TemplatePermissionsDto
        {
            Add = InviteCanAddTemplate,
            View = InviteCanViewTemplate,
            Edit = InviteCanEditTemplate,
            Delete = InviteCanDeleteTemplate,
        };

        var masterPerms = isAdmin ? CreateFullMasterDataPermissions() : new MasterDataPermissionsDto
        {
            Copy = InviteCanCopyMasterData,
            View = InviteCanViewMasterData,
            Edit = InviteCanEditMasterData,
            Add = InviteCanAddMasterData,
            Delete = InviteCanDeleteMasterData,
        };

        var (ok, err, result) = await SetupApiClient.InviteUserAsync(
            _setupContext.OrgCode!,
            name,
            email,
            role,
            partsAll,
            allowed,
            templatePerms,
            masterPerms).ConfigureAwait(true);
        if (!ok || result is null)
        {
            return (err ?? "Could not invite user.", null);
        }

        if (!string.IsNullOrWhiteSpace(result.TemporaryPassword))
        {
            try
            {
                var org = _setupContext.OrgCode?.Trim();
                string? tenantUri = null;
                if (!string.IsNullOrWhiteSpace(org))
                {
                    var (status, _) = await SetupApiClient.StatusAsync(org).ConfigureAwait(true);
                    tenantUri = status?.OrgDatabaseUri?.Trim();
                }

                if (!string.IsNullOrWhiteSpace(tenantUri) && !string.IsNullOrWhiteSpace(org))
                {
                    await _users.EnsureInvitedUserRecordForTenantUriAsync(
                        tenantUri,
                        org,
                        name,
                        email,
                        role,
                        partsAll,
                        allowed,
                        result.TemporaryPassword,
                        templatePerms,
                        masterPerms).ConfigureAwait(true);
                }
                else
                {
                    await _users.EnsureInvitedUserRecordAsync(
                        name,
                        email,
                        role,
                        partsAll,
                        allowed,
                        result.TemporaryPassword,
                        templatePerms,
                        masterPerms).ConfigureAwait(true);
                }
            }
            catch
            {
                // Duplicate or sync failures are non-fatal; list loads from tenant URI.
            }
        }

        await LoadUsersAsync().ConfigureAwait(true);
        _activityLogger.LogUserAction("User Invited", $"Invited \"{name}\" ({email}) as {role}");
        return (null, result);
    }

    public async Task<string?> SaveEditAsync()
    {
        if (string.IsNullOrEmpty(_editUserId))
        {
            return "No user selected for editing.";
        }

        if (_editUserId.StartsWith(MongoOrgUserDirectoryService.OrgAdminDirectoryIdPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "Organisation admin accounts cannot be edited here.";
        }

        var name = InviteName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return "Enter a name.";
        }

        var role = InviteRole.Trim();
        if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            return "Select a role.";
        }

        var isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            ApplyAdminInviteDefaults();
        }

        var partsAll = isAdmin || InvitePartsAllTemplates;
        var allowed = TemplateChoices
            .Where(x => x.IsSelected)
            .Select(x => x.TemplateId)
            .ToList();
        if (!isAdmin && !partsAll && allowed.Count == 0)
        {
            return "Select at least one part template, or enable \"Grant access to all templates\".";
        }

        var templatePerms = isAdmin ? CreateFullTemplatePermissions() : new TemplatePermissionsDto
        {
            Add = InviteCanAddTemplate,
            View = InviteCanViewTemplate,
            Edit = InviteCanEditTemplate,
            Delete = InviteCanDeleteTemplate,
        };

        var masterPerms = isAdmin ? CreateFullMasterDataPermissions() : new MasterDataPermissionsDto
        {
            Copy = InviteCanCopyMasterData,
            View = InviteCanViewMasterData,
            Edit = InviteCanEditMasterData,
            Add = InviteCanAddMasterData,
            Delete = InviteCanDeleteMasterData,
        };

        var ok = await _users.UpdateUserAsync(
            _editUserId,
            name,
            role,
            partsAll,
            allowed,
            templatePerms,
            masterPerms).ConfigureAwait(true);

        if (!ok)
        {
            return "Could not update user.";
        }

        await LoadUsersAsync().ConfigureAwait(true);
        _activityLogger.LogUserAction("User Updated", $"Updated user \"{name}\" ({InviteEmail})");
        return null;
    }

    public async Task<string?> DeleteUserAsync(OrgAppUserSummary user)
    {
        if (user.IsOrgAdminDirectoryEntry)
        {
            return "Organisation admin accounts cannot be removed here.";
        }

        var ok = await _users.DeleteUserAsync(user.Id).ConfigureAwait(true);
        if (!ok)
        {
            return "Could not delete user.";
        }

        await LoadUsersAsync().ConfigureAwait(true);
        _activityLogger.LogUserAction("User Deleted", $"Deleted user \"{user.Name}\" ({user.Email})");
        return null;
    }

    private void ApplyAdminInviteDefaults()
    {
        InviteCanAddTemplate = true;
        InviteCanViewTemplate = true;
        InviteCanEditTemplate = true;
        InviteCanDeleteTemplate = true;
        InviteCanCopyMasterData = true;
        InviteCanViewMasterData = true;
        InviteCanEditMasterData = true;
        InviteCanAddMasterData = true;
        InviteCanDeleteMasterData = true;

        _suppressGrantAllSync = true;
        InvitePartsAllTemplates = true;
        _suppressGrantAllSync = false;
        ApplySelectAllTemplates(updateGrantAllFlag: false);
        RefreshSelectedTemplateAccessItems();
    }

    private static TemplatePermissionsDto CreateFullTemplatePermissions() => new()
    {
        Add = true,
        View = true,
        Edit = true,
        Delete = true,
    };

    private static MasterDataPermissionsDto CreateFullMasterDataPermissions() => new()
    {
        Copy = true,
        View = true,
        Edit = true,
        Add = true,
        Delete = true,
    };

    private async Task ApplyLocalProfileDisplayNamesAsync()
    {
        _profile.Load();
        var savedName = _profile.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(savedName))
        {
            return;
        }

        _setupContext.Refresh();
        var sessionEmail = _setupContext.AdminEmail?.Trim();
        if (string.IsNullOrWhiteSpace(sessionEmail))
        {
            return;
        }

        for (var i = 0; i < _allUsers.Count; i++)
        {
            var user = _allUsers[i];
            if (!string.Equals(user.Email.Trim(), sessionEmail, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(user.Name, savedName, StringComparison.Ordinal))
            {
                continue;
            }

            _allUsers[i] = WithName(user, savedName);
            await _users.UpdateOrgAdminDisplayNameAsync(user.Email, savedName).ConfigureAwait(true);
        }
    }

    private async Task ReconcileStoredUserNamesAsync(string? orgDisplayName, string? orgCode)
    {
        for (var i = 0; i < _allUsers.Count; i++)
        {
            var user = _allUsers[i];
            if (user.IsOrgAdminDirectoryEntry)
            {
                var resolvedAdmin = ResolvePersonName(user, orgDisplayName, orgCode);
                if (!IsPlaceholderName(user.Name, orgDisplayName, orgCode)
                    || string.Equals(resolvedAdmin, user.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                var syncedAdmin = await _users.UpdateOrgAdminDisplayNameAsync(user.Email, resolvedAdmin)
                    .ConfigureAwait(true);
                if (syncedAdmin)
                {
                    _allUsers[i] = WithName(user, resolvedAdmin);
                }

                continue;
            }

            var resolved = ResolvePersonName(user, orgDisplayName, orgCode);
            if (string.Equals(resolved, user.Name, StringComparison.Ordinal)
                || !IsPlaceholderName(user.Name, orgDisplayName, orgCode))
            {
                continue;
            }

            var ok = await _users.UpdateUserAsync(
                user.Id,
                resolved,
                user.Role,
                user.PartsAllTemplates,
                user.AllowedTemplateIds,
                user.TemplatePermissions,
                user.MasterDataPermissions).ConfigureAwait(true);

            if (ok)
            {
                _allUsers[i] = WithName(user, resolved);
            }
        }
    }

    private static OrgAppUserSummary PickPreferredUser(
        OrgAppUserSummary existing,
        OrgAppUserSummary incoming,
        string? orgDisplayName,
        string? orgCode)
    {
        var existingGood = !IsPlaceholderName(existing.Name, orgDisplayName, orgCode);
        var incomingGood = !IsPlaceholderName(incoming.Name, orgDisplayName, orgCode);
        var newer = incoming.InvitedAtUtc >= existing.InvitedAtUtc ? incoming : existing;
        var bestName = existingGood ? existing.Name.Trim()
            : incomingGood ? incoming.Name.Trim()
            : ResolvePersonName(
                !string.IsNullOrWhiteSpace(incoming.Name) ? incoming : existing,
                orgDisplayName,
                orgCode);

        return WithName(newer, bestName);
    }

    private static OrgAppUserSummary NormalizeUserDisplayName(
        OrgAppUserSummary user,
        string? orgDisplayName,
        string? orgCode)
    {
        var name = ResolvePersonName(user, orgDisplayName, orgCode);
        return string.Equals(name, user.Name, StringComparison.Ordinal) ? user : WithName(user, name);
    }

    private static OrgAppUserSummary WithName(OrgAppUserSummary user, string name) =>
        new()
        {
            Id = user.Id,
            Name = name,
            Email = user.Email,
            Role = user.Role,
            PartsAllTemplates = user.PartsAllTemplates,
            AllowedTemplateIds = user.AllowedTemplateIds,
            InvitedAtUtc = user.InvitedAtUtc,
            TemplatePermissions = user.TemplatePermissions,
            MasterDataPermissions = user.MasterDataPermissions,
            Status = user.Status,
            IsOrgAdminDirectoryEntry = user.IsOrgAdminDirectoryEntry,
        };

    private static bool IsPlaceholderName(string? name, string? orgDisplayName, string? orgCode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return true;
        }

        var n = name.Trim();
        if (string.Equals(n, "Organisation Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "Organization Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(orgDisplayName)
            && string.Equals(n, $"{orgDisplayName.Trim()} Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(orgDisplayName)
            && string.Equals(n, orgDisplayName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(orgCode)
            && string.Equals(n, orgCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string ResolvePersonName(OrgAppUserSummary user, string? orgDisplayName, string? orgCode)
    {
        var email = user.Email?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(email)
            && string.Equals(user.Name.Trim(), email, StringComparison.OrdinalIgnoreCase))
        {
            var fromEmail = DisplayNameFromEmail(email);
            return string.IsNullOrWhiteSpace(fromEmail) ? email : fromEmail;
        }

        if (!IsPlaceholderName(user.Name, orgDisplayName, orgCode))
        {
            return user.Name.Trim();
        }

        var derived = DisplayNameFromEmail(email);
        return string.IsNullOrWhiteSpace(derived) ? email : derived;
    }

    private static string DisplayNameFromEmail(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@');
        if (at <= 0)
        {
            return trimmed;
        }

        var local = trimmed[..at].Replace('.', ' ').Replace('_', ' ').Replace('-', ' ');
        while (local.Contains("  ", StringComparison.Ordinal))
        {
            local = local.Replace("  ", " ", StringComparison.Ordinal);
        }

        return string.IsNullOrWhiteSpace(local)
            ? trimmed
            : System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(local.Trim());
    }
}
