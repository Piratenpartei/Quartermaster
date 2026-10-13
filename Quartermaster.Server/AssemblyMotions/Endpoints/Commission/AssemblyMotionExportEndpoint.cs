using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionExportEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionExportDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionExportBuilder _builder;
    private readonly AuditLogRepository _auditLog;
    private readonly PermissionContext _perms;

    public AssemblyMotionExportEndpoint(AssemblyMotionPortalRepository portals, EventRepository events, AssemblyMotionAccess access,
        AssemblyMotionExportBuilder builder, AuditLogRepository auditLog, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _access = access;
        _builder = builder;
        _auditLog = auditLog;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/export");
    }

    public override async Task HandleAsync(AssemblyMotionPortalIdRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!_access.IsCommission(ev)) {
            AddError(I18nKey.Error.AssemblyMotion.NotCommission);
            await SendErrorsAsync(403, ct);
            return;
        }

        var export = _builder.Build(portal, ev, DateTime.UtcNow);
        _auditLog.Log("AssemblyMotionPortal", portal.Id, "Exported", null, null, $"motions={export.Motions.Count}");
        await SendAsync(export, cancellation: ct);
    }
}
