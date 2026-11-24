using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>Encodes master-data column provenance in <see cref="TemplateFieldDefinition.ValidationPattern"/>.</summary>
public static class MasterDataSourceFieldEncoding
{
    public const string SourcePrefix = "pfsrc|";

    public static bool TryParse(string? validationPattern, out string catalogId, out string fieldKey)
    {
        catalogId = string.Empty;
        fieldKey = string.Empty;
        if (string.IsNullOrWhiteSpace(validationPattern)
            || !validationPattern.StartsWith(SourcePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = validationPattern[SourcePrefix.Length..].Split('|', 2);
        if (parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1]))
        {
            return false;
        }

        catalogId = parts[0];
        fieldKey = parts[1];
        return true;
    }

    public static bool IsSourcedFromCatalog(TemplateFieldDefinition field, string masterDataCatalogId) =>
        TryParse(field.ValidationPattern, out var catalogId, out _)
        && string.Equals(catalogId, masterDataCatalogId, StringComparison.Ordinal);
}
