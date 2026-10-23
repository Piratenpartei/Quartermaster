using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdministrativeDivisionSearchEndpoint : Endpoint<AdministrativeDivisionSearchRequest, AdministrativeDivisionSearchResponse> {
    private readonly AdministrativeDivisionRepository _repository;
    private readonly AdministrativeDivisionSearchIndex _searchIndex;

    public AdministrativeDivisionSearchEndpoint(AdministrativeDivisionRepository repository, AdministrativeDivisionSearchIndex searchIndex) {
        _repository = repository;
        _searchIndex = searchIndex;
    }

    public override void Configure() {
        Get("/api/administrativedivisions/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AdministrativeDivisionSearchRequest req, CancellationToken ct) {
        var (items, matchedNames, totalCount) = Find(req);

        // A municipality's own PostCodes column is a Landkreis-wide aggregate (unusable as "the"
        // post code). Resolve a representative one from its child localities, batched in one query.
        var children = _repository.GetChildrenForParents(items.Select(i => i.Id).ToList());
        var childrenByParent = children
            .Where(c => c.ParentId.HasValue)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        await SendAsync(new AdministrativeDivisionSearchResponse {
            Items = items.Select(ad => new AdministrativeDivisionDTO {
                Id = ad.Id,
                ParentId = ad.ParentId,
                Name = ad.Name,
                Depth = ad.Depth,
                AdminCode = ad.AdminCode,
                PostCodes = ad.PostCodes,
                PrimaryPostCode = ResolvePrimaryPostCode(ad, childrenByParent),
                MatchedName = matchedNames.GetValueOrDefault(ad.Id)
            }).ToList(),
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }

    /// <summary>Names go through the search index; an empty query or a post code still searches the database.</summary>
    private (List<AdministrativeDivision> Items, Dictionary<Guid, string?> MatchedNames, int TotalCount) Find(AdministrativeDivisionSearchRequest req) {
        if (string.IsNullOrWhiteSpace(req.Query) || !req.Query.Any(char.IsLetter)) {
            var (found, count) = _repository.Search(req.Query, req.Page, req.PageSize);
            return (found, [], count);
        }

        var result = _searchIndex.Search(req.Query, req.Page, req.PageSize);
        var byId = _repository.GetMany(result.Items.Select(h => h.Id)).ToDictionary(d => d.Id);
        var items = result.Items
            .Where(h => byId.ContainsKey(h.Id))
            .Select(h => byId[h.Id])
            .ToList();
        return (items, result.Items.ToDictionary(h => h.Id, h => h.MatchedName), result.TotalCount);
    }

    private static string? ResolvePrimaryPostCode(
        AdministrativeDivision division, Dictionary<Guid, List<AdministrativeDivision>> childrenByParent) {
        if (childrenByParent.TryGetValue(division.Id, out var kids) && kids.Count > 0) {
            // Prefer the same-named child (the "Kernort"), otherwise any child.
            var source = kids.FirstOrDefault(k => k.Name == division.Name) ?? kids[0];
            return FirstPostCode(source.PostCodes);
        }
        return FirstPostCode(division.PostCodes);
    }

    private static string? FirstPostCode(string? postCodes) {
        if (string.IsNullOrWhiteSpace(postCodes))
            return null;
        return postCodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
    }
}
