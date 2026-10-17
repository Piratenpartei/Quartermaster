using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySessionStartRequest {
    public Guid EventId { get; set; }
}

public class AssemblySessionStartEndpoint : Endpoint<AssemblySessionStartRequest, AssemblyCommandResult> {
    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionHost _sessions;

    public AssemblySessionStartEndpoint(PermissionContext perms, EventRepository events, AssemblySessionHost sessions) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
    }

    public override void Configure() {
        Post("/api/assembly/{EventId}/session/start");
    }

    public override async Task HandleAsync(AssemblySessionStartRequest req, CancellationToken ct) {
        var status = AssemblySessionGuard.Check(_perms, _events, req.EventId, PermissionIdentifier.AssemblyChair);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        var result = await _sessions.StartSessionAsync(req.EventId, _perms.UserId!.Value, ct);
        await SendAsync(result, result.Accepted ? 200 : 409, ct);
    }
}
