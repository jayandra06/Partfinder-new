using Microsoft.UI.Xaml;

namespace PartFinder.Services;

public interface IAppStateStore
{
    string CurrentTenant { get; set; }
    string CurrentUserName { get; set; }
    ElementTheme CurrentTheme { get; set; }
    string OrgDisplayName { get; set; }
    string OrgPlan { get; set; }
    string OrgType { get; set; }

    /// <summary>When navigating to Templates from dashboard quick actions (e.g. create new).</summary>
    string? TemplatesPendingAction { get; set; }

    /// <summary>When navigating to Analytics, open this organisation template in the composer.</summary>
    string? TemplatesPendingOpenTemplateId { get; set; }

    /// <summary>When opening <see cref="TemplatesPendingOpenTemplateId"/>, open read-only workspace instead of edit.</summary>
    bool TemplatesPendingOpenViewOnly { get; set; }
}
