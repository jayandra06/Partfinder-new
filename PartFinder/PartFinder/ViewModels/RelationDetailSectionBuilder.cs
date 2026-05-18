using PartFinder.Services;

namespace PartFinder.ViewModels;

internal static class RelationDetailSectionBuilder
{
    public static RelationDetailSection FromEnrichedRelation(
        string relationKey,
        EnrichedRelationDto relation,
        WorksheetRelationDto? meta = null,
        string? lookupTemplateName = null)
    {
        var title = string.IsNullOrWhiteSpace(relation.MenuLabel) ? relationKey : relation.MenuLabel;
        var matchGroups = BuildMatchGroups(relation);
        var details = matchGroups.Count > 0
            ? matchGroups[0].Details
            : [new DisplayPair("Status", relation.Matched ? "Matched" : "No match")];

        return new RelationDetailSection
        {
            Title = title,
            IsMatched = relation.Matched,
            MatchCount = relation.MatchCount > 0 ? relation.MatchCount : (relation.Matched ? 1 : 0),
            Details = details,
            MatchGroups = matchGroups,
            RelationId = meta?.Id ?? relationKey,
            LookupTemplateId = meta?.LookupTemplateId ?? string.Empty,
            LookupTemplateName = lookupTemplateName ?? meta?.Name ?? string.Empty,
        };
    }

    private static List<RelationMatchGroup> BuildMatchGroups(EnrichedRelationDto relation)
    {
        if (relation.Matches.Count > 0)
        {
            var groups = new List<RelationMatchGroup>(relation.Matches.Count);
            for (var i = 0; i < relation.Matches.Count; i++)
            {
                var match = relation.Matches[i];
                var details = match.DisplayValues
                    .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => new DisplayPair(kv.Key, kv.Value))
                    .ToList();
                if (details.Count == 0)
                {
                    details.Add(new DisplayPair("Status", "Empty row"));
                }

                groups.Add(new RelationMatchGroup
                {
                    Title = relation.Matches.Count > 1 ? $"Match {i + 1} of {relation.Matches.Count}" : "Match",
                    Details = details,
                });
            }

            return groups;
        }

        if (!relation.Matched)
        {
            return [];
        }

        var fallback = relation.DisplayValues
            .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => new DisplayPair(kv.Key, kv.Value))
            .ToList();
        if (fallback.Count == 0)
        {
            return [];
        }

        return
        [
            new RelationMatchGroup
            {
                Title = "Match",
                Details = fallback,
            },
        ];
    }
}
