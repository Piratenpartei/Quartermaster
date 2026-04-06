using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Meetings;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Meetings;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Meetings;

public class MeetingListEndpoint : Endpoint<MeetingListRequest, MeetingListResponse> {
    private readonly MeetingRepository _meetingRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly RoleRepository _roleRepo;
    private readonly DbContext _db;

    public MeetingListEndpoint(
        MeetingRepository meetingRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        RoleRepository roleRepo,
        DbContext db) {
        _meetingRepo = meetingRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _roleRepo = roleRepo;
        _db = db;
    }

    public override void Configure() {
        Get("/api/meetings");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MeetingListRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);

        if (userId == null) {
            // Anonymous: only Public meetings.
            var (items, total) = _meetingRepo.List(
                req.ChapterId, req.Status, MeetingVisibility.Public,
                req.DateFrom, req.DateTo, null, req.Page, req.PageSize);
            await SendOkResponse(items, total, req, ct);
            return;
        }

        // Authenticated user: union of
        //   (a) meetings in chapters where user has ViewMeetings (inheritance-aware), filtered by visibility rules
        //   (b) private meetings in chapters where user has direct officer/delegate role
        // plus global-ViewMeetings = sees everything.
        var hasGlobalView = EndpointAuthorizationHelper.HasGlobalPermission(
            userId.Value, PermissionIdentifier.ViewMeetings, _globalPermRepo);

        if (hasGlobalView) {
            var (items, total) = _meetingRepo.List(
                req.ChapterId, req.Status, req.Visibility,
                req.DateFrom, req.DateTo, null, req.Page, req.PageSize);
            await SendOkResponse(items, total, req, ct);
            return;
        }

        var permittedChapterIds = GetUserVisibleChapterIds(userId.Value);
        var directRoleChapterIds = GetUserDirectRoleChapterIds(userId.Value);

        if (permittedChapterIds.Count == 0 && directRoleChapterIds.Count == 0) {
            // No perms at all → only public meetings visible.
            var (publicItems, publicTotal) = _meetingRepo.List(
                req.ChapterId, req.Status, MeetingVisibility.Public,
                req.DateFrom, req.DateTo, null, req.Page, req.PageSize);
            await SendOkResponse(publicItems, publicTotal, req, ct);
            return;
        }

        // Fetch an inner query: meetings in permitted chapters, honoring visibility rules.
        // Use a direct query because we need OR logic between public-in-permitted and
        // private-in-direct-role chapters.
        var meetings = QueryVisibleMeetings(req, permittedChapterIds, directRoleChapterIds);
        var totalCount = meetings.Count;
        var paged = meetings
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .ToList();
        await SendOkResponse(paged, totalCount, req, ct);
    }

    private HashSet<Guid> GetUserVisibleChapterIds(Guid userId) {
        // Chapters where the user has ViewMeetings (direct or inherited).
        var allPerms = _chapterPermRepo.GetAllForUser(userId);
        var directChapters = allPerms
            .Where(kvp => kvp.Value.Contains(PermissionIdentifier.ViewMeetings))
            .Select(kvp => kvp.Key)
            .ToList();

        var result = new HashSet<Guid>();
        foreach (var chapterId in directChapters) {
            foreach (var descendant in _chapterRepo.GetDescendantIds(chapterId))
                result.Add(descendant);
        }
        return result;
    }

    private HashSet<Guid> GetUserDirectRoleChapterIds(Guid userId) {
        // Chapters with a direct ChapterOfficer or GeneralChapterDelegate role assignment.
        var assignments = _roleRepo.GetAssignmentsForUser(userId);
        var roleIds = assignments.Where(a => a.ChapterId.HasValue).Select(a => a.RoleId).ToHashSet();
        var roles = _db.Roles.Where(r => roleIds.Contains(r.Id)).ToList();
        var privilegedRoleIds = roles
            .Where(r => r.Identifier == PermissionIdentifier.SystemRole.ChapterOfficer
                     || r.Identifier == PermissionIdentifier.SystemRole.GeneralChapterDelegate)
            .Select(r => r.Id)
            .ToHashSet();
        return assignments
            .Where(a => a.ChapterId.HasValue && privilegedRoleIds.Contains(a.RoleId))
            .Select(a => a.ChapterId!.Value)
            .ToHashSet();
    }

    private List<Meeting> QueryVisibleMeetings(
        MeetingListRequest req,
        HashSet<Guid> viewMeetingChapters,
        HashSet<Guid> directRoleChapters) {

        var q = _db.Meetings.Where(m => m.DeletedAt == null);

        if (req.ChapterId.HasValue)
            q = q.Where(m => m.ChapterId == req.ChapterId.Value);
        if (req.Status.HasValue)
            q = q.Where(m => m.Status == req.Status.Value);
        if (req.Visibility.HasValue)
            q = q.Where(m => m.Visibility == req.Visibility.Value);
        if (req.DateFrom.HasValue)
            q = q.Where(m => m.MeetingDate >= req.DateFrom.Value);
        if (req.DateTo.HasValue)
            q = q.Where(m => m.MeetingDate <= req.DateTo.Value);

        var viewList = viewMeetingChapters.ToList();
        var directList = directRoleChapters.ToList();

        // Visibility rules:
        //  - Public meetings visible to anyone (including the permitted-but-no-direct-role case)
        //  - Private meetings visible only if user has a direct role on that exact chapter
        //  - ViewMeetings permission on a chapter grants visibility to Public meetings there
        //    (private still gated on direct role)
        var filtered = q.Where(m =>
            m.Visibility == MeetingVisibility.Public
                && (viewList.Contains(m.ChapterId) || directList.Contains(m.ChapterId))
            || m.Visibility == MeetingVisibility.Private
                && directList.Contains(m.ChapterId)
        );

        return filtered
            .OrderByDescending(m => m.MeetingDate)
            .ThenByDescending(m => m.CreatedAt)
            .ToList();
    }

    private async Task SendOkResponse(
        List<Meeting> items, int totalCount, MeetingListRequest req, CancellationToken ct) {
        var chapterIds = items.Select(m => m.ChapterId).Distinct().ToList();
        var chapterNames = _chapterRepo.GetAll()
            .Where(c => chapterIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.Name);

        var meetingIds = items.Select(m => m.Id).ToList();
        var counts = _db.AgendaItems
            .Where(a => meetingIds.Contains(a.MeetingId))
            .GroupBy(a => a.MeetingId)
            .Select(g => new { MeetingId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.MeetingId, x => x.Count);

        var dtos = items.Select(m => MeetingDtoBuilder.BuildMeetingDTO(
            m,
            chapterNames.TryGetValue(m.ChapterId, out var n) ? n : "",
            counts.TryGetValue(m.Id, out var c) ? c : 0
        )).ToList();

        await SendAsync(new MeetingListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
