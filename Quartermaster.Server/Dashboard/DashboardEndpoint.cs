using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Events;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Events;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Dashboard;

public class DashboardEndpoint : EndpointWithoutRequest<DashboardDTO> {
    private const int WidgetLimit = 10;

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MotionRepository _motionRepo;
    private readonly EventRepository _eventRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly PermissionContext _perms;

    public DashboardEndpoint(
        MembershipApplicationRepository applicationRepo,
        DueSelectionRepository dueSelectionRepo,
        MotionRepository motionRepo,
        EventRepository eventRepo,
        ChapterRepository chapterRepo,
        UserChapterPermissionRepository chapterPermRepo,
        PermissionContext perms) {
        _applicationRepo = applicationRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _motionRepo = motionRepo;
        _eventRepo = eventRepo;
        _chapterRepo = chapterRepo;
        _chapterPermRepo = chapterPermRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/dashboard");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = _perms.UserId;
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);
        var dto = new DashboardDTO();

        if (userId != null) {
            dto.PendingApplications = FetchApplications(chapters);
            dto.PendingDueSelections = FetchDueSelections();
            dto.OpenMotions = FetchMotions(chapters);
        }

        dto.UpcomingEvents = FetchUpcomingEvents(userId, chapters);

        await SendAsync(dto, cancellation: ct);
    }

    private DashboardSection<DashboardApplicationDTO>? FetchApplications(Dictionary<Guid, string> chapters) {
        var chapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewApplications);

        if (chapterIds is { Count: 0 })
            return null;

        var scopeIds = chapterIds ?? chapters.Keys.ToList();
        var (items, total) = _applicationRepo.List(scopeIds, ApplicationStatus.Pending, 1, WidgetLimit);

        return new DashboardSection<DashboardApplicationDTO> {
            TotalCount = total,
            Items = items.Select(a => new DashboardApplicationDTO {
                Id = a.Id,
                FirstName = a.FirstName,
                LastName = a.LastName,
                ChapterName = a.ChapterId.HasValue && chapters.TryGetValue(a.ChapterId.Value, out var n) ? n : "",
                SubmittedAt = a.SubmittedAt.ToDtoUtc()
            }).ToList()
        };
    }

    private DashboardSection<DashboardDueSelectionDTO>? FetchDueSelections() {
        var chapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewDueSelections);

        if (chapterIds is { Count: 0 })
            return null;

        var (items, total) = _dueSelectionRepo.List(DueSelectionStatus.Pending, 1, WidgetLimit, chapterIds);

        return new DashboardSection<DashboardDueSelectionDTO> {
            TotalCount = total,
            Items = items.Select(d => new DashboardDueSelectionDTO {
                Id = d.Id,
                FirstName = d.FirstName,
                LastName = d.LastName,
                SelectedDue = d.SelectedDue
            }).ToList()
        };
    }

    private DashboardSection<DashboardMotionDTO>? FetchMotions(Dictionary<Guid, string> chapters) {
        var chapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewMotions);

        if (chapterIds is { Count: 0 })
            return null;

        var (items, total) = _motionRepo.ListOpen(chapterIds, WidgetLimit);

        return new DashboardSection<DashboardMotionDTO> {
            TotalCount = total,
            Items = items.Select(m => new DashboardMotionDTO {
                Id = m.Id,
                Title = m.Title,
                ChapterName = chapters.TryGetValue(m.ChapterId, out var n) ? n : "",
                CreatedAt = m.CreatedAt.ToDtoUtc()
            }).ToList()
        };
    }

    private List<DashboardEventDTO> FetchUpcomingEvents(Guid? userId, Dictionary<Guid, string> chapters) {
        var allowedVisibilities = GetAllowedEventVisibilities(userId);
        var events = _eventRepo.GetUpcoming(allowedVisibilities, null, WidgetLimit);

        return events.Select(e => new DashboardEventDTO {
            Id = e.Id,
            PublicName = e.PublicName,
            ChapterName = chapters.TryGetValue(e.ChapterId, out var name) ? name : "",
            EventDate = e.EventDate.ToDtoDate()
        }).ToList();
    }

    private List<EventVisibility> GetAllowedEventVisibilities(Guid? userId) {
        if (userId == null)
            return new List<EventVisibility> { EventVisibility.Public };

        var hasViewGlobal = _perms.HasGlobal(PermissionIdentifier.ViewEvents);
        var hasViewInAnyChapter = _chapterPermRepo.GetAllForUser(userId.Value)
            .Any(kvp => kvp.Value.Contains(PermissionIdentifier.ViewEvents));

        if (hasViewGlobal || hasViewInAnyChapter)
            return new List<EventVisibility> {
                EventVisibility.Public, EventVisibility.MembersOnly, EventVisibility.Private
            };

        return new List<EventVisibility> { EventVisibility.Public, EventVisibility.MembersOnly };
    }
}
