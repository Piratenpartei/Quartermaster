using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionCollectingListEndpoint : Endpoint<AssemblyMotionCollectingListRequest, AssemblyMotionCollectingListResponse> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly EventRepository _events;
    private readonly AssemblyMotionEligibility _eligibility;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionCollectingListEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        EventRepository events, AssemblyMotionEligibility eligibility, AssemblyMotionAccess access, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _events = events;
        _eligibility = eligibility;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/collecting");
    }

    public override async Task HandleAsync(AssemblyMotionCollectingListRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        var eligibility = _eligibility.Check(_perms.UserId, ev, DateTime.UtcNow);
        if (!eligibility.IsEligible && !_access.IsCommission(ev)) {
            AddError(I18nKey.Error.AssemblyMotion.NotEligible);
            await SendErrorsAsync(403, ct);
            return;
        }

        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var collecting = _motions.ListByPortal(portal.Id)
            .Where(m => m.Status == AssemblyMotionStatus.CollectingCoSubmitters && m.Number == null)
            .Where(m => types.ContainsKey(m.TypeId))
            .OrderBy(m => types[m.TypeId].SortOrder)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var page = collecting
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToList();
        var submitters = _motions.GetSubmitters(page.Select(m => m.Id).ToList());

        await SendAsync(new AssemblyMotionCollectingListResponse {
            Items = page
                .Select(m => ToItem(m, portal, types[m.TypeId], submitters.GetValueOrDefault(m.Id)?.Count ?? 0))
                .ToList(),
            TotalCount = collecting.Count,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }

    private static AssemblyMotionCollectingItemDTO ToItem(AssemblyMotion motion, AssemblyMotionPortal portal, AssemblyMotionType type, int submitterCount) {
        var item = AssemblyMotionPublicView.BuildListItem(motion, type);
        return new AssemblyMotionCollectingItemDTO {
            Id = item.Id,
            TypeId = item.TypeId,
            TypeCode = item.TypeCode,
            TypeName = item.TypeName,
            DisplayNumber = item.DisplayNumber,
            Title = item.Title,
            Status = item.Status,
            IsInRevision = item.IsInRevision,
            Result = item.Result,
            SubmittedAt = item.SubmittedAt,
            SubmitterCount = submitterCount,
            MinSubmitters = type.MinSubmitters,
            EffectiveDeadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion).ToDtoUtc()
        };
    }
}
