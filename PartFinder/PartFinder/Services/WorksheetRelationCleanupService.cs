using PartFinder.Models;

namespace PartFinder.Services;

/// <summary>Deletes worksheet relation pairs (forward + reverse) via the license API.</summary>
public sealed class WorksheetRelationCleanupService
{
    private readonly BackendApiClient _api;

    public WorksheetRelationCleanupService(BackendApiClient api)
    {
        _api = api;
    }

    public async Task<(bool Ok, string? Error)> DeleteRelationLinkAsync(
        WorksheetRelationDto relation,
        IReadOnlyList<WorksheetRelationDto>? allRelations = null,
        CancellationToken ct = default)
    {
        var relations = allRelations;
        if (relations is null)
        {
            var (loadOk, loadErr, data) = await _api.GetRelationsAsync(ct).ConfigureAwait(false);
            if (!loadOk)
            {
                return (false, loadErr ?? "Could not load relations.");
            }

            relations = data;
        }

        var relationId = NormalizeRelationId(relation.Id);
        var (ok, error) = await _api.DeleteRelationAsync(relationId, ct).ConfigureAwait(false);
        if (!ok)
        {
            return (false, error ?? "Failed to delete relation.");
        }

        var reverse = WorksheetRelationBidirectional.FindRelationBetween(
            relations,
            relation.LookupTemplateId,
            relation.PrimaryTemplateId);
        if (reverse is not null)
        {
            var reverseId = NormalizeRelationId(reverse.Id);
            if (!string.Equals(reverseId, relationId, StringComparison.OrdinalIgnoreCase))
            {
                _ = await _api.DeleteRelationAsync(reverseId, ct).ConfigureAwait(false);
            }
        }

        return (true, null);
    }

    public async Task DeleteAllRelationsInvolvingTemplateAsync(string templateId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return;
        }

        var (ok, _, relations) = await _api.GetRelationsAsync(ct).ConfigureAwait(false);
        if (!ok || relations.Count == 0)
        {
            return;
        }

        var processedPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var relation in relations)
        {
            if (relation.Id.EndsWith(":mirror", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var involves = string.Equals(relation.PrimaryTemplateId, templateId, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(relation.LookupTemplateId, templateId, StringComparison.OrdinalIgnoreCase);
            if (!involves)
            {
                continue;
            }

            var pairKey = string.Join(
                "|",
                new[] { relation.PrimaryTemplateId, relation.LookupTemplateId }
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            if (!processedPairs.Add(pairKey))
            {
                continue;
            }

            _ = await DeleteRelationLinkAsync(relation, relations, ct).ConfigureAwait(false);
        }
    }

    private static string NormalizeRelationId(string id) =>
        id.EndsWith(":mirror", StringComparison.OrdinalIgnoreCase)
            ? id[..^":mirror".Length]
            : id;
}
