using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

public class AdministrativeDivisionRootsEndpoint : EndpointWithoutRequest<List<AdministrativeDivisionDTO>> {
    private readonly AdministrativeDivisionRepository _repository;

    public AdministrativeDivisionRootsEndpoint(AdministrativeDivisionRepository repository) {
        _repository = repository;
    }

    public override void Configure() {
        Get("/api/administrativedivisions/roots");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var roots = _repository.GetRoots();
        var dtos = roots.Select(ad => new AdministrativeDivisionDTO {
            Id = ad.Id,
            ParentId = ad.ParentId,
            Name = ad.Name,
            Depth = ad.Depth,
            AdminCode = ad.AdminCode,
            PostCodes = ad.PostCodes
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
