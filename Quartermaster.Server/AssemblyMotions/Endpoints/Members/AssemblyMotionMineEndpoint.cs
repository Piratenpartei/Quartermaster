using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionMineEndpoint : Endpoint<AssemblyMotionPortalIdRequest, List<AssemblyMotionMineItemDTO>> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly PermissionContext _perms;

    public AssemblyMotionMineEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        AssemblyMotionInquiryRepository inquiries, EventRepository events, AssemblyMotionEligibility eligibility, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _inquiries = inquiries;
        _events = events;
        _eligibility = eligibility;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/mine");
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

        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var motions = _motions.ListForMember(portal.Id, member.Id)
            .Where(m => types.ContainsKey(m.TypeId))
            .ToList();
        var openInquiries = _inquiries.CountOpen(motions.Select(m => m.Id).ToList());
        var items = motions
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AssemblyMotionMineItemDTO {
                Id = m.Id,
                DisplayNumber = m.Number == null ? null : AssemblyMotionNumber.Format(types[m.TypeId].Code, m.Number.Value),
                NumberLabel = AssemblyMotionNumber.Label(types[m.TypeId].Code, m.Number, m.ReferenceNumber),
                Title = m.Title,
                Status = m.Status,
                IsAuthor = m.CreatedByMemberId == member.Id,
                OpenInquiryCount = openInquiries.GetValueOrDefault(m.Id)
            })
            .ToList();

        await SendAsync(items, cancellation: ct);
    }
}
