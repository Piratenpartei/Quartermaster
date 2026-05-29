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

    public AdministrativeDivisionSearchEndpoint(AdministrativeDivisionRepository repository) {
        _repository = repository;
    }

    public override void Configure() {
        Get("/api/administrativedivisions/search");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AdministrativeDivisionSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _repository.Search(req.Query, req.Page, req.PageSize);

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
                PrimaryPostCode = ResolvePrimaryPostCode(ad, childrenByParent)
            }).ToList(),
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
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
