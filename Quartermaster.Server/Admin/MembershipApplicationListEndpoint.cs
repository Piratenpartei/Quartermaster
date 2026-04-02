using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationListEndpoint
    : Endpoint<MembershipApplicationListRequest, MembershipApplicationListResponse> {

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MembershipApplicationListEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/admin/membershipapplications");
    }

    public override async Task HandleAsync(MembershipApplicationListRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = EndpointAuthorizationHelper.GetPermittedChapterIds(
            userId.Value, PermissionIdentifier.ViewApplications, _globalPermRepo, _chapterPermRepo, _chapterRepo);
        if (allowedChapterIds is { Count: 0 }) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapterIds = req.ChapterId.HasValue
            ? _chapterRepo.GetDescendantIds(req.ChapterId.Value)
            : new List<Guid>();

        // Intersect user-specified chapter filter with auth-permitted chapters
        if (allowedChapterIds != null) {
            var allowed = new HashSet<Guid>(allowedChapterIds);
            if (chapterIds.Count > 0)
                chapterIds = chapterIds.Where(id => allowed.Contains(id)).ToList();
            else
                chapterIds = allowedChapterIds;
        }

        ApplicationStatus? status = req.Status.HasValue
            ? (ApplicationStatus)req.Status.Value
            : null;

        var (items, totalCount) = _applicationRepo.List(chapterIds, status, req.Page, req.PageSize);

        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(a => new MembershipApplicationAdminDTO {
            Id = a.Id,
            FirstName = a.FirstName,
            LastName = a.LastName,
            EMail = a.EMail,
            AddressCity = a.AddressCity,
            ChapterId = a.ChapterId,
            ChapterName = a.ChapterId.HasValue && chapters.ContainsKey(a.ChapterId.Value)
                ? chapters[a.ChapterId.Value] : "",
            Status = (int)a.Status,
            SubmittedAt = a.SubmittedAt,
            ProcessedAt = a.ProcessedAt
        }).ToList();

        await SendAsync(new MembershipApplicationListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
