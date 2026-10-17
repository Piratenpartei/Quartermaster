using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySessionListRequest {
    public Guid EventId { get; set; }
}

public class AssemblySessionListEndpoint : Endpoint<AssemblySessionListRequest, List<AssemblySessionSummaryDTO>> {
    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionRepository _sessions;

    public AssemblySessionListEndpoint(PermissionContext perms, EventRepository events, AssemblySessionRepository sessions) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
    }

    public override void Configure() {
        Get("/api/assembly/{EventId}/sessions");
    }

    public override async Task HandleAsync(AssemblySessionListRequest req, CancellationToken ct) {
        var status = AssemblySessionGuard.CheckView(_perms, _events, req.EventId);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        var sessions = _sessions.ListByEvent(req.EventId);
        var counts = _sessions.CountEntries(sessions.Select(s => s.Id).ToList());
        await SendAsync(sessions
            .Select(s => new AssemblySessionSummaryDTO {
                Id = s.Id,
                StartedAt = s.StartedAt,
                EndedAt = s.EndedAt,
                LogDeletedAt = s.LogDeletedAt,
                EntryCount = counts.GetValueOrDefault(s.Id)
            })
            .ToList(), cancellation: ct);
    }
}
