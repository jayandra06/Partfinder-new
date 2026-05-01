using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PartFinder.Models;
using PartFinder.Services;
using System.Collections.ObjectModel;

namespace PartFinder.ViewModels;

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly IOrgUserDirectoryService _users;
    private readonly ITemplateSchemaService _templates;
    private readonly ILocalSetupContext _setupContext;
    private readonly ActivityLogger _activityLogger;

    public ObservableCollection<OrgAppUserSummary> Users { get; } = [];
    public ObservableCollection<TemplatePickItemViewModel> TemplateChoices { get; } = [];

    public string[] RoleOptions { get; } = ["Admin", "Employee"];

    [ObservableProperty]
    private string inviteName = string.Empty;

    [ObservableProperty]
    private string inviteEmail = string.Empty;

    [ObservableProperty]
    private string inviteRole = "Employee";

    [ObservableProperty]
    private bool invitePartsAllTemplates = true;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isEditMode;

    private string? _editUserId;

    private List<OrgAppUserSummary> _allUsers = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    public string[] RoleFilters { get; } = ["All", "Admin", "Employee"];

    [ObservableProperty]
    private string selectedRoleFilter = "All";

    partial void OnSelectedRoleFilterChanged(string value)
    {
        ApplyFilter();
    }

    public string[] StatusFilters { get; } = ["All Status", "Active", "Pending"];

    [ObservableProperty]
    private string selectedStatusFilter = "All Status";

    partial void OnSelectedStatusFilterChanged(string value)
    {
        ApplyFilter();
    }

    public int TotalUsersCount => _allUsers.Count;
    public int ActiveUsersCount => _allUsers.Count(u => u.Status == "Active");
    public int AdminUsersCount => _allUsers.Count(u => u.Role == "Admin");
    public int PendingUsersCount => _allUsers.Count(u => u.Status == "Pending");

    private void NotifyKpiChanges()
    {
        OnPropertyChanged(nameof(TotalUsersCount));
        OnPropertyChanged(nameof(ActiveUsersCount));
        OnPropertyChanged(nameof(AdminUsersCount));
        OnPropertyChanged(nameof(PendingUsersCount));
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Users.Clear();
        var query = SearchQuery?.Trim() ?? string.Empty;
        var roleFilter = SelectedRoleFilter ?? "All";
        var statusFilter = SelectedStatusFilter ?? "All Status";

        foreach (var u in _allUsers)
        {
            bool matchesQuery = string.IsNullOrEmpty(query) || 
                (u.Name != null && u.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (u.Email != null && u.Email.Contains(query, StringComparison.OrdinalIgnoreCase));

            bool matchesRole = string.Equals(roleFilter, "All", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(u.Role, roleFilter, StringComparison.OrdinalIgnoreCase);

            bool matchesStatus = string.Equals(statusFilter, "All Status", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(u.Status, statusFilter, StringComparison.OrdinalIgnoreCase);

            if (matchesQuery && matchesRole && matchesStatus)
            {
                Users.Add(u);
            }
        }
    }

    public bool IsEmployeeRole =>
        string.Equals(InviteRole, "Employee", StringComparison.OrdinalIgnoreCase);

    public bool CanPickSpecificTemplates => IsEmployeeRole && !InvitePartsAllTemplates;
    public bool HasTemplateChoices => TemplateChoices.Count > 0;

    public UserManagementViewModel(
        IOrgUserDirectoryService users,
        ITemplateSchemaService templates,
        ILocalSetupContext setupContext,
        ActivityLogger activityLogger)
    {
        _users = users;
        _templates = templates;
        _setupContext = setupContext;
        _activityLogger = activityLogger;
    }

    partial void OnInviteRoleChanged(string value)
    {
        OnPropertyChanged(nameof(IsEmployeeRole));
        OnPropertyChanged(nameof(CanPickSpecificTemplates));
    }

    partial void OnInvitePartsAllTemplatesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanPickSpecificTemplates));
    }

    [RelayCommand]
    private void SelectAllTemplates()
    {
        foreach (var template in TemplateChoices)
        {
            template.IsSelected = true;
        }
    }

    [RelayCommand]
    private void ClearTemplateSelection()
    {
        foreach (var template in TemplateChoices)
        {
            template.IsSelected = false;
        }
    }

    public async Task LoadUsersAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var list = await _users.ListUsersAsync().ConfigureAwait(true);
            _allUsers = list.ToList();
            NotifyKpiChanges();
            ApplyFilter();
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

    public async Task PrepareInviteDialogAsync(CancellationToken cancellationToken = default)
    {
        IsEditMode = false;
        _editUserId = null;
        InviteName = string.Empty;
        InviteEmail = string.Empty;
        InviteRole = "Employee";
        InvitePartsAllTemplates = true;
        TemplateChoices.Clear();

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

            TemplateChoices.Add(
                new TemplatePickItemViewModel
                {
                    TemplateId = t.Id,
                    Name = t.Name,
                });
        }

        OnPropertyChanged(nameof(HasTemplateChoices));
    }

    public async Task PrepareEditDialogAsync(OrgAppUserSummary user, CancellationToken cancellationToken = default)
    {
        IsEditMode = true;
        _editUserId = user.Id;
        InviteName = user.Name;
        InviteEmail = user.Email;
        InviteRole = user.Role;
        InvitePartsAllTemplates = user.PartsAllTemplates;
        TemplateChoices.Clear();

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

            TemplateChoices.Add(
                new TemplatePickItemViewModel
                {
                    TemplateId = t.Id,
                    Name = t.Name,
                    IsSelected = user.AllowedTemplateIds.Contains(t.Id, StringComparer.OrdinalIgnoreCase)
                });
        }

        OnPropertyChanged(nameof(HasTemplateChoices));
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
            return ("Organization code is not available in local setup.", null);
        }

        var partsAll = InvitePartsAllTemplates;
        var allowed = TemplateChoices
            .Where(x => x.IsSelected)
            .Select(x => x.TemplateId)
            .ToList();
        if (!partsAll && allowed.Count == 0)
        {
            return ("Select at least one part template, or enable \"All part templates\".", null);
        }

        var (ok, err, result) = await SetupApiClient.InviteUserAsync(
            _setupContext.OrgCode!,
            name,
            email,
            role,
            partsAll,
            allowed).ConfigureAwait(true);
        if (!ok || result is null)
        {
            return (err ?? "Could not invite user.", null);
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

        var partsAll = InvitePartsAllTemplates;
        var allowed = TemplateChoices
            .Where(x => x.IsSelected)
            .Select(x => x.TemplateId)
            .ToList();
        if (!partsAll && allowed.Count == 0 && string.Equals(role, "Employee", StringComparison.OrdinalIgnoreCase))
        {
            return "Select at least one part template, or enable \"All part templates\".";
        }

        var ok = await _users.UpdateUserAsync(
            _editUserId,
            name,
            role,
            partsAll,
            allowed).ConfigureAwait(true);
            
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
        var ok = await _users.DeleteUserAsync(user.Id).ConfigureAwait(true);
        if (!ok)
        {
            return "Could not delete user.";
        }

        await LoadUsersAsync().ConfigureAwait(true);
        _activityLogger.LogUserAction("User Deleted", $"Deleted user \"{user.Name}\" ({user.Email})");
        return null;
    }
}
