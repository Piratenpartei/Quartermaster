using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.NewsPosts;
using Quartermaster.Data.NewsPosts;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.NewsPosts.Endpoints;

public class ProgrammeSourceListEndpoint : EndpointWithoutRequest<List<ProgrammeSourceDTO>> {
    private readonly ProgrammeRepository _programmeRepo;
    private readonly PermissionContext _perms;

    public ProgrammeSourceListEndpoint(ProgrammeRepository programmeRepo, PermissionContext perms) {
        _programmeRepo = programmeRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/newsposts/sources");
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

        var sources = _programmeRepo.GetSources()
            .Select(s => new ProgrammeSourceDTO {
                Id = s.Id,
                WikiPageTitle = s.WikiPageTitle,
                LastSyncedAt = s.LastSyncedAt == null
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(s.LastSyncedAt.Value, DateTimeKind.Utc)),
                LastError = s.LastError,
                SectionCount = _programmeRepo.CountSections(s.Id)
            })
            .ToList();

        await SendAsync(sources, cancellation: ct);
    }
}
