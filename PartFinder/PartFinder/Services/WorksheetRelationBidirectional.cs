namespace PartFinder.Services;

/// <summary>Mirror worksheet links so browsing works from both primary and lookup templates.</summary>
public static class WorksheetRelationBidirectional
{
    public static SaveWorksheetRelationRequest BuildReverseRequest(
        SaveWorksheetRelationRequest forward,
        string primaryTemplateName,
        string lookupTemplateName)
    {
        var matchLabels = forward.MatchKeys
            .Select(k => k.SourceColumn)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var displayOnPrimary = forward.MatchKeys
            .Select(k => k.SourceColumn)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SaveWorksheetRelationRequest
        {
            PrimaryTemplateId = forward.LookupTemplateId,
            LookupTemplateId = forward.PrimaryTemplateId,
            Name = $"{lookupTemplateName} to {primaryTemplateName}",
            TriggerColumn = matchLabels.FirstOrDefault() ?? forward.TriggerColumn,
            MatchKeys = matchLabels
                .Select(k => new RelationMatchPairDto { SourceColumn = k, TargetColumn = k })
                .ToList(),
            DisplayColumns = displayOnPrimary.Count > 0 ? displayOnPrimary : new List<string>(forward.DisplayColumns),
            MenuLabel = $"{primaryTemplateName} info",
        };
    }

    public static IReadOnlyList<WorksheetRelationDto> GetOutboundRelations(
        string templateId,
        IReadOnlyList<WorksheetRelationDto> allRelations)
    {
        if (string.IsNullOrWhiteSpace(templateId) || allRelations.Count == 0)
        {
            return [];
        }

        var outbound = new List<WorksheetRelationDto>();
        var lookupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relation in allRelations)
        {
            if (!string.Equals(relation.PrimaryTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var enriched = CloneRelation(relation);
            EnsureMatchKeysFromTrigger(enriched);
            MergeOppositeRelationKeys(enriched, allRelations);
            outbound.Add(enriched);
            lookupIds.Add(relation.LookupTemplateId);
        }

        foreach (var relation in allRelations)
        {
            if (string.Equals(relation.PrimaryTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(relation.LookupTemplateId, templateId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lookupIds.Contains(relation.PrimaryTemplateId))
            {
                continue;
            }

            var mirrored = MirrorAsPrimary(relation, templateId);
            EnsureMatchKeysFromTrigger(mirrored);
            MergeOppositeRelationKeys(mirrored, allRelations);
            outbound.Add(mirrored);
            lookupIds.Add(relation.PrimaryTemplateId);
        }

        return outbound;
    }

    public static IReadOnlyList<string> GetMatchKeyColumnLabels(WorksheetRelationDto relation)
    {
        EnsureMatchKeysFromTrigger(relation);
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(relation.TriggerColumn))
        {
            labels.Add(relation.TriggerColumn.Trim());
        }

        foreach (var pair in relation.MatchKeys)
        {
            if (!string.IsNullOrWhiteSpace(pair.SourceColumn))
            {
                labels.Add(pair.SourceColumn.Trim());
            }

            if (!string.IsNullOrWhiteSpace(pair.TargetColumn))
            {
                labels.Add(pair.TargetColumn.Trim());
            }
        }

        return labels.ToList();
    }

    public static void EnsureMatchKeysFromTrigger(WorksheetRelationDto relation)
    {
        if (string.IsNullOrWhiteSpace(relation.TriggerColumn))
        {
            return;
        }

        var trigger = relation.TriggerColumn.Trim();
        if (relation.MatchKeys.Count == 0)
        {
            relation.MatchKeys =
            [
                new RelationMatchPairDto { SourceColumn = trigger, TargetColumn = trigger },
            ];
            return;
        }

        if (!relation.MatchKeys.Any(m =>
                string.Equals(m.SourceColumn, trigger, StringComparison.OrdinalIgnoreCase)))
        {
            relation.MatchKeys.Add(new RelationMatchPairDto { SourceColumn = trigger, TargetColumn = trigger });
        }
    }

    private static void MergeOppositeRelationKeys(
        WorksheetRelationDto outbound,
        IReadOnlyList<WorksheetRelationDto> allRelations)
    {
        var opposite = allRelations.FirstOrDefault(r =>
            !RelationIdsEqual(r.Id, outbound.Id)
            && string.Equals(r.PrimaryTemplateId, outbound.LookupTemplateId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.LookupTemplateId, outbound.PrimaryTemplateId, StringComparison.OrdinalIgnoreCase));
        if (opposite is null)
        {
            return;
        }

        EnsureMatchKeysFromTrigger(opposite);
        foreach (var pair in opposite.MatchKeys)
        {
            AddSymmetricKeyIfMissing(outbound, pair.SourceColumn);
            AddSymmetricKeyIfMissing(outbound, pair.TargetColumn);
        }

        AddSymmetricKeyIfMissing(outbound, opposite.TriggerColumn);
    }

    private static void AddSymmetricKeyIfMissing(WorksheetRelationDto relation, string? columnLabel)
    {
        if (string.IsNullOrWhiteSpace(columnLabel))
        {
            return;
        }

        var label = columnLabel.Trim();
        if (relation.MatchKeys.Any(m =>
                string.Equals(m.SourceColumn, label, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        relation.MatchKeys.Add(new RelationMatchPairDto { SourceColumn = label, TargetColumn = label });
    }

    private static bool RelationIdsEqual(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var baseA = StripMirrorSuffix(a);
        var baseB = StripMirrorSuffix(b);
        return string.Equals(baseA, baseB, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripMirrorSuffix(string id) =>
        id.EndsWith(":mirror", StringComparison.OrdinalIgnoreCase)
            ? id[..^":mirror".Length]
            : id;

    private static WorksheetRelationDto CloneRelation(WorksheetRelationDto source) =>
        new()
        {
            Id = source.Id,
            PrimaryTemplateId = source.PrimaryTemplateId,
            LookupTemplateId = source.LookupTemplateId,
            Name = source.Name,
            TriggerColumn = source.TriggerColumn,
            MenuLabel = source.MenuLabel,
            MatchKeys = source.MatchKeys
                .Select(m => new RelationMatchPairDto
                {
                    SourceColumn = m.SourceColumn,
                    TargetColumn = m.TargetColumn,
                })
                .ToList(),
            DisplayColumns = source.DisplayColumns.ToList(),
        };

    public static WorksheetRelationDto MirrorAsPrimary(WorksheetRelationDto relation, string newPrimaryTemplateId)
    {
        var matchKeys = relation.MatchKeys.Count > 0
            ? relation.MatchKeys
                .Select(p => new RelationMatchPairDto
                {
                    SourceColumn = p.TargetColumn,
                    TargetColumn = p.SourceColumn,
                })
                .ToList()
            :
            [
                new RelationMatchPairDto
                {
                    SourceColumn = relation.TriggerColumn,
                    TargetColumn = relation.TriggerColumn,
                },
            ];

        var trigger = matchKeys
            .Select(m => m.SourceColumn)
            .FirstOrDefault(c =>
                !string.IsNullOrWhiteSpace(c)
                && string.Equals(c, relation.TriggerColumn, StringComparison.OrdinalIgnoreCase))
            ?? matchKeys.FirstOrDefault()?.SourceColumn
            ?? string.Empty;

        var display = relation.MatchKeys
            .Select(m => m.SourceColumn)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (display.Count == 0 && relation.DisplayColumns.Count > 0)
        {
            display = relation.DisplayColumns.ToList();
        }

        return new WorksheetRelationDto
        {
            Id = $"{relation.Id}:mirror",
            PrimaryTemplateId = newPrimaryTemplateId,
            LookupTemplateId = relation.PrimaryTemplateId,
            Name = $"{relation.Name} (reverse)",
            TriggerColumn = trigger,
            MatchKeys = matchKeys,
            DisplayColumns = display,
            MenuLabel = string.Empty,
        };
    }

    public static WorksheetRelationDto? FindRelationBetween(
        IReadOnlyList<WorksheetRelationDto> all,
        string primaryTemplateId,
        string lookupTemplateId) =>
        all.FirstOrDefault(r =>
            string.Equals(r.PrimaryTemplateId, primaryTemplateId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(r.LookupTemplateId, lookupTemplateId, StringComparison.OrdinalIgnoreCase));
}
