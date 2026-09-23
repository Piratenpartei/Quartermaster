using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.NewsPosts.Programme;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class ProgrammeSourceDeleteEndpoint : EndpointWithoutRequest {
    private readonly ProgrammeRepository _programmeRepo;
    private readonly ProgrammeSyncService _syncService;
    private readonly PermissionContext _perms;

    public ProgrammeSourceDeleteEndpoint(ProgrammeRepository programmeRepo, ProgrammeSyncService syncService, PermissionContext perms) {
        _programmeRepo = programmeRepo;
        _syncService = syncService;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/newsposts/sources/{Id}");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageNewsPosts)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _programmeRepo.DeleteSource(Route<Guid>("Id"));
        _syncService.LoadIndex();
        await SendNoContentAsync(ct);
    }
}
