namespace PartFinder.Core;

/// <summary>Merges inherited template fields with child overrides by field key.</summary>
public static class TemplateFieldMerge
{
    public sealed record FieldSnapshot(
        string Key,
        string Label,
        string Type,
        bool IsRequired,
        int DisplayOrder,
        string? LinkedTemplateId,
        string? LinkedDisplayFieldKey);

    public static IReadOnlyList<FieldSnapshot> MergeInheritedFields(
        IReadOnlyList<FieldSnapshot>? baseFields,
        IReadOnlyList<FieldSnapshot> childFields)
    {
        if (baseFields is null || baseFields.Count == 0)
        {
            return childFields;
        }

        var merged = new Dictionary<string, FieldSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in baseFields.OrderBy(f => f.DisplayOrder))
        {
            merged[field.Key] = field;
        }

        foreach (var field in childFields)
        {
            merged[field.Key] = field;
        }

        return merged.Values.OrderBy(f => f.DisplayOrder).ThenBy(f => f.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
