using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Changelog;

namespace Quartermaster.Server.Changelog;

public class ChangelogEndpoint : EndpointWithoutRequest<IReadOnlyList<ChangelogEntryDTO>> {
    private readonly ChangelogService _service;

    public ChangelogEndpoint(ChangelogService service) {
        _service = service;
    }

    public override void Configure() {
        Get("/api/changelog");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        await SendAsync(_service.GetAll(), cancellation: ct);
    }
}
