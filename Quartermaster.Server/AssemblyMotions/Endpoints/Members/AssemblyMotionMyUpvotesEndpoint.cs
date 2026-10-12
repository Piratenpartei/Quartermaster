using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionMyUpvotesEndpoint : Endpoint<AssemblyMotionPortalIdRequest, List<Guid>> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly PermissionContext _perms;

    public AssemblyMotionMyUpvotesEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionUpvoteRepository upvotes,
        EventRepository events, AssemblyMotionEligibility eligibility, PermissionContext perms) {
        _portals = portals;
        _upvotes = upvotes;
        _events = events;
        _eligibility = eligibility;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/my-upvotes");
    }

    public override async Task HandleAsync(AssemblyMotionPortalIdRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var member = _eligibility.Check(_perms.UserId, ev, DateTime.UtcNow).Member;
        if (member == null) {
            AddError(I18nKey.Error.AssemblyMotion.NotEligible);
            await SendErrorsAsync(403, ct);
            return;
        }

        await SendAsync(_upvotes.MotionIdsForMember(portal.Id, member.Id), cancellation: ct);
    }
}
