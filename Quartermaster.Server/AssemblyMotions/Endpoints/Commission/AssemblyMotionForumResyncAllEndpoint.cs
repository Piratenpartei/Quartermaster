using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;
using Quartermaster.Server.AssemblyMotions.Forum;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionForumResyncAllEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionForumResyncAllDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionForumResync _resync;
    private readonly PermissionContext _perms;

    public AssemblyMotionForumResyncAllEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionForumResync resync, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _access = access;
        _resync = resync;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/forum/resync");
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

        if (!portal.ForumMirrorEnabled) {
            AddError(I18nKey.Error.AssemblyMotion.ForumMirrorDisabled);
            await SendErrorsAsync(409, ct);
            return;
        }

        var queued = _resync.QueueAll(portal.Id, DateTime.UtcNow);
        await SendAsync(new AssemblyMotionForumResyncAllDTO { Queued = queued }, cancellation: ct);
    }
}
