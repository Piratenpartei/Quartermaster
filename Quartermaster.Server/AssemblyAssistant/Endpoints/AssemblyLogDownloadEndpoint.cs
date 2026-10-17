using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data.AssemblyAssistant;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyAssistant.Export;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblyLogDownloadRequest {
    public Guid SessionId { get; set; }

    [QueryParam]
    public string? Format { get; set; }
}

public class AssemblyLogDownloadEndpoint : Endpoint<AssemblyLogDownloadRequest> {
    private readonly PermissionContext _perms;
    private readonly EventRepository _events;
    private readonly AssemblySessionRepository _sessions;
    private readonly AssemblyLogExport _export;

    public AssemblyLogDownloadEndpoint(PermissionContext perms, EventRepository events, AssemblySessionRepository sessions, AssemblyLogExport export) {
        _perms = perms;
        _events = events;
        _sessions = sessions;
        _export = export;
    }

    public override void Configure() {
        Get("/api/assembly/sessions/{SessionId}/log");
    }

    public override async Task HandleAsync(AssemblyLogDownloadRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var session = _sessions.Get(req.SessionId);
        if (session == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var status = AssemblySessionGuard.CheckView(_perms, _events, session.EventId);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }
        if (session.LogDeletedAt != null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var eventName = _events.Get(session.EventId)?.PublicName ?? "";
        var entries = _sessions.GetLog(session.Id);
        if (string.Equals(req.Format, "markdown", StringComparison.OrdinalIgnoreCase)) {
            var markdown = Encoding.UTF8.GetBytes(_export.ToMarkdown(eventName, session, entries));
            await SendBytesAsync(markdown, AssemblyLogExport.FileName(eventName, session, "md"), "text/markdown; charset=utf-8", cancellation: ct);
            return;
        }

        await SendBytesAsync(_export.ToCsv(entries), AssemblyLogExport.FileName(eventName, session, "csv"), "text/csv; charset=utf-8", cancellation: ct);
    }
}
