using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyAssistant.Session;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySessionEndRequest {
    public Guid EventId { get; set; }
}

public class AssemblySessionEndEndpoint : Endpoint<AssemblySessionEndRequest, AssemblyCommandResult> {
    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionHost _sessions;

    public AssemblySessionEndEndpoint(PermissionContext perms, EventRepository events, AssemblySessionHost sessions) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
    }

    public override void Configure() {
        Post("/api/assembly/{EventId}/session/end");
    }

    public override async Task HandleAsync(AssemblySessionEndRequest req, CancellationToken ct) {
        var status = AssemblySessionGuard.Check(_perms, _events, req.EventId, PermissionIdentifier.AssemblyChair);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }
        if (_sessions.EventId != req.EventId) {
            await SendAsync(AssemblyCommandResult.Rejected(I18nKey.Ui.AssemblyConsole.Reject.NoSession), 409, ct);
            return;
        }

        var result = await _sessions.EndSessionAsync(_perms.UserId!.Value, ct);
        await SendAsync(result, result.Accepted ? 200 : 409, ct);
    }
}
