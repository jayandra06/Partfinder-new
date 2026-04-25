namespace PartFinder.Services;

public sealed class SetupStatusResult
{
    public bool Valid { get; set; }
    public string? Reason { get; set; }
    public string? Message { get; set; }
    public string? OrganizationName { get; set; }
    public string? OrgCode { get; set; }
    public string? Status { get; set; }
    public string? ValidUntil { get; set; }
    public int? MaxUsers { get; set; }
    public int? MaxParts { get; set; }
    public bool? HasOrgDatabase { get; set; }
    public string? OrgDatabaseUri { get; set; }
    public string? OrgAdminStatus { get; set; }
    public bool? ServerReachedDatabase { get; set; }
    public bool? RequiresInviteLogin { get; set; }
    public string? FirstAdminEmail { get; set; }

    /** When reason is PLATFORM_MAINTENANCE, ISO end of maintenance window. */
    public string? MaintenanceUntil { get; set; }
}