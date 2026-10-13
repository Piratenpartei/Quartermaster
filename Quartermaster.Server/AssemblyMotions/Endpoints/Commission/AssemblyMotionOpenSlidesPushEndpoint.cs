using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.AssemblyMotions.OpenSlides;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionOpenSlidesPushEndpoint : Endpoint<OpenSlidesPushRequest, OpenSlidesPushResultDTO> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly AssemblyMotionOpenSlidesPush _push;
    private readonly PermissionContext _perms;

    public AssemblyMotionOpenSlidesPushEndpoint(AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionAccess access, AssemblyMotionOpenSlidesPush push, PermissionContext perms) {
        _portals = portals;
        _events = events;
        _access = access;
        _push = push;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/assemblymotions/portals/{PortalId}/openslides/push");
    }

    public override async Task HandleAsync(OpenSlidesPushRequest req, CancellationToken ct) {
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

        if (!_push.IsConfigured()) {
            AddError(I18nKey.Error.AssemblyMotion.OpenSlidesNotConfigured);
            await SendErrorsAsync(409, ct);
            return;
        }

        var outcome = await _push.PushAsync(portal, req.IncludeSubmitted, DateTime.UtcNow, ct);
        if (outcome.Result == null) {
            AddError(outcome.ErrorKey!);
            await SendErrorsAsync(409, ct);
            return;
        }

        await SendAsync(outcome.Result, cancellation: ct);
    }
}
