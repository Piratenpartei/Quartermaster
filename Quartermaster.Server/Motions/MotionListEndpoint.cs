using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionListEndpoint : Endpoint<MotionListRequest, MotionListResponse> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;

    public MotionListEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        UserGlobalPermissionRepository globalPermRepo, UserChapterPermissionRepository chapterPermRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
    }

    public override void Configure() {
        Get("/api/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionListRequest req, CancellationToken ct) {
        MotionApprovalStatus? status = req.ApprovalStatus.HasValue
            ? (MotionApprovalStatus)req.ApprovalStatus.Value
            : null;

        // Determine which non-public motions the caller may see.
        // Anonymous users or users without ViewMotions never see non-public motions.
        var includeNonPublic = false;
        List<Guid>? nonPublicChapterIds = null;

        if (req.IncludeNonPublic) {
            var userId = EndpointAuthorizationHelper.GetUserId(User);
            if (userId != null) {
                nonPublicChapterIds = EndpointAuthorizationHelper.GetPermittedChapterIds(
                    userId.Value, PermissionIdentifier.ViewMotions,
                    _globalPermRepo, _chapterPermRepo, _chapterRepo);
                // null means global permission (all chapters), non-empty means specific chapters
                includeNonPublic = nonPublicChapterIds == null || nonPublicChapterIds.Count > 0;
            }
        }

        var (items, totalCount) = _motionRepo.List(
            req.ChapterId, status, includeNonPublic, req.Page, req.PageSize,
            nonPublicChapterIds);

        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(m => new MotionDTO {
            Id = m.Id,
            ChapterId = m.ChapterId,
            ChapterName = chapters.ContainsKey(m.ChapterId) ? chapters[m.ChapterId] : "",
            AuthorName = m.AuthorName,
            Title = m.Title,
            IsPublic = m.IsPublic,
            ApprovalStatus = (int)m.ApprovalStatus,
            IsRealized = m.IsRealized,
            CreatedAt = m.CreatedAt,
            ResolvedAt = m.ResolvedAt
        }).ToList();

        await SendAsync(new MotionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
