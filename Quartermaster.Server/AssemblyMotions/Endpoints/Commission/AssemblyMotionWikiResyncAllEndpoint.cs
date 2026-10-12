using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.AssemblyMotions.Wiki;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionWikiResyncAllEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionWikiResyncAllDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionWikiResync _resync;
    private readonly PermissionContext _perms;

    public AssemblyMotionWikiResyncAllEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionWikiResync resync, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _access = access;
        _resync = resync;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/wiki/resync");
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

        if (!portal.WikiMirrorEnabled) {
            AddError(I18nKey.Error.AssemblyMotion.WikiMirrorDisabled);
            await SendErrorsAsync(409, ct);
            return;
        }

        var queued = _resync.QueueAll(portal.Id, DateTime.UtcNow);
        await SendAsync(new AssemblyMotionWikiResyncAllDTO { Queued = queued }, cancellation: ct);
    }
}
