namespace PartFinder.Services;

public interface ICurrentUserAccessService
{
    UserAccessCapabilities Capabilities { get; }

    /// <summary>Resolves the signed-in / setup email and loads org user record from Mongo.</summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>Filters templates for the Parts page (Master Data template excluded for employees).</summary>
    IReadOnlyList<Models.PartTemplateDefinition> FilterTemplatesForParts(
        IReadOnlyList<Models.PartTemplateDefinition> templates);
}
