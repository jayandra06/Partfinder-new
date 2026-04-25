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

    public bool IsEmployeeRole =>
        string.Equals(InviteRole, "Employee", StringComparison.OrdinalIgnoreCase);

    public bool CanPickSpecificTemplates => IsEmployeeRole && !InvitePartsAllTemplates;
    public bool HasTemplateChoices => TemplateChoices.Count > 0;

    public UserManagementViewModel(
        IOrgUserDirectoryService users,
        ITemplateSchemaService templates,
        ILocalSetupContext setupContext)
    {
        _users = users;
        _templates = templates;
        _setupContext = setupContext;
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
            Users.Clear();
            foreach (var u in list)
            {
                Users.Add(u);
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

    public async Task PrepareInviteDialogAsync(CancellationToken cancellationToken = default)
    {
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
        return (null, result);
    }
}
