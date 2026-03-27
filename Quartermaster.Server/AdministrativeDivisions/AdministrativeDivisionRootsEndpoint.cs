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
        await SendAsync(roots.Select(ad => ad.ToDto()).ToList(), cancellation: ct);
    }
}
