using PartFinder.Models;

namespace PartFinder.Services;

public sealed class InviteUserResult
{
    public required string Email { get; init; }
    public required string OrganizationCode { get; init; }
    public required string TemporaryPassword { get; init; }
    public bool EmailSent { get; init; }
    public string? EmailError { get; init; }
}

public interface IOrgUserDirectoryService
{
    Task<IReadOnlyList<OrgAppUserSummary>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<OrgAppUserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<InviteUserResult> InviteUserAsync(
        string name,
        string email,
        string role,
        bool partsAllTemplates,
        IReadOnlyList<string> allowedTemplateIds,
        CancellationToken cancellationToken = default);

    Task<bool> ValidateInviteCredentialsAsync(
        string email,
        string temporaryPassword,
        CancellationToken cancellationToken = default);
}
