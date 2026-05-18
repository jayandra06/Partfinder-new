namespace PartFinder.Core;

/// <summary>Explorer (reference data) template naming — shared by app and unit tests.</summary>
public static class ExplorerTemplateNames
{
    public const string TemplateId = "master-data";
    public const string DisplayName = "Explorer";
    public const string LegacyDisplayName = "Master Data";

    public static bool IsExplorerTemplateName(string? name) =>
        !string.IsNullOrWhiteSpace(name)
        && (string.Equals(name, DisplayName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, LegacyDisplayName, StringComparison.OrdinalIgnoreCase));
}
