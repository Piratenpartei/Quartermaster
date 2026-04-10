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

        await SendAsync(new AdministrativeDivisionSearchResponse {
            Items = items.Select(ad => new AdministrativeDivisionDTO {
                Id = ad.Id,
                ParentId = ad.ParentId,
                Name = ad.Name,
                Depth = ad.Depth,
                AdminCode = ad.AdminCode,
                PostCodes = ad.PostCodes
            }).ToList(),
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
