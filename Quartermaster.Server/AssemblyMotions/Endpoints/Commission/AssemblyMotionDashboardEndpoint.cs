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

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionDashboardEndpoint : Endpoint<AssemblyMotionDashboardRequest, AssemblyMotionDashboardResponse> {
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly EventRepository _events;
    private readonly AssemblyMotionAccess _access;
    private readonly PermissionContext _perms;

    public AssemblyMotionDashboardEndpoint(AssemblyMotionPortalRepository portals, AssemblyMotionRepository motions,
        AssemblyMotionInquiryRepository inquiries, EventRepository events, AssemblyMotionAccess access, PermissionContext perms) {
        _portals = portals;
        _motions = motions;
        _inquiries = inquiries;
        _events = events;
        _access = access;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/assemblymotions/portals/{PortalId}/dashboard");
    }

    public override async Task HandleAsync(AssemblyMotionDashboardRequest req, CancellationToken ct) {
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

        var nowUtc = DateTime.UtcNow;
        var types = _portals.GetTypes(portal.Id).ToDictionary(t => t.Id);
        var search = req.Search?.Trim();

        var all = _motions.ListSummariesByPortal(portal.Id)
            .Where(m => types.ContainsKey(m.TypeId))
            .ToList();

        var openInquiries = _inquiries.CountOpen(all.Select(m => m.Id).ToList());

        var filtered = all
            .Where(m => req.TypeId == null || m.TypeId == req.TypeId)
            .Where(m => req.Status == null || m.Status == req.Status)
            .Where(m => req.HasOpenInquiry == null || (openInquiries.GetValueOrDefault(m.Id) > 0) == req.HasOpenInquiry)
            .Where(m => req.WikiSyncState == null || m.WikiSyncState == req.WikiSyncState)
            .Where(m => req.WikiFailed != true || IsWikiFailure(m))
            .Where(m => req.ForumSyncState == null || m.ForumSyncState == req.ForumSyncState)
            .Where(m => req.ForumFailed != true || m.ForumSyncState == ForumSyncState.Failed)
            .Where(m => AssemblyMotionSearch.Matches(m, types[m.TypeId], search))
            .OrderBy(m => types[m.TypeId].SortOrder)
            .ThenBy(m => m.Number == null)
            .ThenBy(m => m.Number)
            .ThenBy(m => m.CreatedAt)
            .ToList();

        var page = filtered
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToList();
        var submitters = _motions.GetSubmitters(page.Select(m => m.Id).ToList());

        await SendAsync(new AssemblyMotionDashboardResponse {
            Items = page
                .Select(m => ToItem(m, portal, types[m.TypeId], submitters.GetValueOrDefault(m.Id)?.Count ?? 0, openInquiries.GetValueOrDefault(m.Id), nowUtc))
                .ToList(),
            TotalCount = filtered.Count,
            Page = req.Page,
            PageSize = req.PageSize,
            FailedWikiSyncCount = all.Count(IsWikiFailure),
            WikiMirrorEnabled = portal.WikiMirrorEnabled,
            FailedForumSyncCount = all.Count(m => m.ForumSyncState == ForumSyncState.Failed),
            ForumMirrorEnabled = portal.ForumMirrorEnabled,
            OpenSlidesMeetingConfigured = portal.OpenSlidesMeetingId != null
        }, cancellation: ct);
    }

    private static bool IsWikiFailure(AssemblyMotion motion) {
        return motion.WikiSyncState == WikiSyncState.Failed || motion.WikiSyncState == WikiSyncState.ProtectionFailed;
    }

    private static AssemblyMotionDashboardItemDTO ToItem(AssemblyMotion motion, AssemblyMotionPortal portal,
        AssemblyMotionType type, int submitterCount, int openInquiryCount, DateTime nowUtc) {
        var deadline = AssemblyMotionDeadlines.Effective(portal, type, motion.IsCompetingMotion);
        return new AssemblyMotionDashboardItemDTO {
            Id = motion.Id,
            DisplayNumber = motion.Number == null ? null : AssemblyMotionNumber.Format(type.Code, motion.Number.Value),
            NumberLabel = AssemblyMotionNumber.Label(type.Code, motion.Number, motion.ReferenceNumber),
            Title = motion.Title,
            TypeCode = type.Code,
            Status = motion.Status,
            IsInRevision = motion.IsInRevision,
            Result = motion.Result,
            SubmitterCount = submitterCount,
            MinSubmitters = type.MinSubmitters,
            OpenInquiryCount = openInquiryCount,
            EffectiveDeadline = deadline.ToDtoUtc(),
            DeadlinePassed = AssemblyMotionDeadlines.IsPast(deadline, nowUtc),
            WikiSyncState = motion.WikiSyncState,
            ForumSyncState = motion.ForumSyncState,
            Revision = motion.Revision,
            OpenSlidesPushedRevision = motion.OpenSlidesPushedRevision
        };
    }
}
