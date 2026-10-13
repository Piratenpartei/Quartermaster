using System;
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

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Public;

public class AssemblyMotionRankingEndpoint : Endpoint<AssemblyMotionPortalIdRequest, AssemblyMotionRankingResponse> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionUpvoteRepository _upvotes;
    private readonly EventRepository _events;
    private readonly PermissionContext _perms;

    public AssemblyMotionRankingEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        AssemblyMotionUpvoteRepository upvotes, EventRepository events, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _upvotes = upvotes;
        _events = events;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/ranking");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AssemblyMotionPortalIdRequest req, CancellationToken ct) {
        var portal = _portals.Get(req.PortalId);
        var ev = portal == null ? null : _events.Get(portal.EventId);
        if (portal == null || ev == null || !AssemblyMotionEventVisibility.IsVisible(ev, _perms)) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!AssemblyMotionUpvoteService.IsRankingPublic(portal, DateTime.UtcNow)) {
            AddError(I18nKey.Error.AssemblyMotion.RankingNotAvailable);
            await SendErrorsAsync(409, ct);
            return;
        }

        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var entries = AssemblyMotionRanking.Compute(_motions.ListSummariesByPortal(portal.Id), types, _upvotes.CountsForPortal(portal.Id));

        await SendAsync(new AssemblyMotionRankingResponse {
            Items = entries.Select(ToItem).ToList(),
            EndsAt = portal.PrioritizationEndsAt!.Value.ToDtoUtc()
        }, cancellation: ct);
    }

    private static AssemblyMotionRankingItemDTO ToItem(AssemblyMotionRankingEntry entry) {
        return new() {
            Rank = entry.Rank,
            Id = entry.Motion.Id,
            DisplayNumber = AssemblyMotionNumber.Format(entry.Type.Code, entry.Motion.Number!.Value),
            Title = entry.Motion.Title,
            TypeId = entry.Type.Id,
            TypeCode = entry.Type.Code,
            TypeName = entry.Type.Name,
            TypeSortOrder = entry.Type.SortOrder,
            UpvoteCount = entry.UpvoteCount,
            Status = entry.Motion.Status
        };
    }
}
