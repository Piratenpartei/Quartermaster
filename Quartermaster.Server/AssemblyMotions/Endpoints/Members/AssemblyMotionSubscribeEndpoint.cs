using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionSubscribeEndpoint : Endpoint<AssemblyMotionIdRequest> {
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly EventRepository _events;
    private readonly AssemblyMotionSubscriptionRepository _subscriptions;
    private readonly PermissionContext _perms;

    public AssemblyMotionSubscribeEndpoint(AssemblyMotionRepository motions, AssemblyMotionPortalRepository portals, EventRepository events,
        AssemblyMotionSubscriptionRepository subscriptions, PermissionContext perms) {
        _motions = motions;
        _portals = portals;
        _events = events;
        _subscriptions = subscriptions;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{Id}/subscription");
    }

    public override async Task HandleAsync(AssemblyMotionIdRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var motion = _motions.Get(req.Id);
        var portal = motion == null ? null : _portals.Get(motion.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (motion == null || ev == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        // Visibility first, so the answer for a motion of an invisible event says nothing about its status.
        if (!AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var userId = _perms.UserId.Value;
        var problem = AssemblyMotionSubscriptionRules.Problem(motion, ev, _motions.IsCreatedByUser(motion, userId));
        if (problem != null) {
            if (problem.Value.ErrorKey != null)
                AddError(problem.Value.ErrorKey);
            await SendErrorsAsync(problem.Value.StatusCode, ct);
            return;
        }

        _subscriptions.Subscribe(motion.Id, userId, DateTime.UtcNow);
        await SendNoContentAsync(ct);
    }
}
