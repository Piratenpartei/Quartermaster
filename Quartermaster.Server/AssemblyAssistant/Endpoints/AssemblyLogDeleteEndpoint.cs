using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblyLogDeleteRequest {
    public Guid SessionId { get; set; }
}

/// <summary>Deletes the minutes aid of an ended session once the minutes are signed; the session row stays with its delete date.</summary>
public class AssemblyLogDeleteEndpoint : Endpoint<AssemblyLogDeleteRequest> {
    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionRepository _sessions;

    public AssemblyLogDeleteEndpoint(PermissionContext perms, EventRepository events, AssemblySessionRepository sessions) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
    }

    public override void Configure() {
        Post("/api/assembly/sessions/{SessionId}/log/delete");
    }

    public override async Task HandleAsync(AssemblyLogDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var session = _sessions.Get(req.SessionId);
        if (session == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = AssemblySessionGuard.Check(_perms, _events, session.EventId, PermissionIdentifier.AssemblyChair);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }
        if (session.EndedAt == null)
            ThrowError(I18nKey.Ui.AssemblyLog.Error.SessionRunning, 409);

        _sessions.DeleteLog(session.Id, DateTime.UtcNow);
        await SendOkAsync(ct);
    }
}
