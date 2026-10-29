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
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Motions;

public class MotionListEndpoint : Endpoint<MotionListRequest, MotionListResponse> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public MotionListEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo,
        PermissionContext perms) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
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
            if (_perms.UserId != null) {
                nonPublicChapterIds = _perms.GetPermittedChapterIds(PermissionIdentifier.ViewMotions);
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
            ApprovalStatus = m.ApprovalStatus,
            IsRealized = m.IsRealized,
            CreatedAt = m.CreatedAt.ToDtoUtc(),
            ResolvedAt = m.ResolvedAt.ToDtoUtc()
        }).ToList();

        if (_perms.UserId != null)
            FillNotices(items, dtos);

        await SendAsync(new MotionListResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }

    private void FillNotices(IReadOnlyList<Motion> motions, List<MotionDTO> dtos) {
        var targetMotionIds = motions.Where(m => m.HandedOverToMotionId != null).Select(m => m.HandedOverToMotionId!.Value).ToList();
        var targetChapterIds = _motionRepo.GetChapterIds(targetMotionIds);
        var chapterIds = motions.Select(m => m.OnBehalfOfChapterId).OfType<Guid>().Concat(targetChapterIds.Values).Distinct().ToList();
        var names = _chapterRepo.GetNames(chapterIds);
        foreach (var motion in motions) {
            var dto = dtos.First(d => d.Id == motion.Id);
            dto.OnBehalfOfChapterId = motion.OnBehalfOfChapterId;
            dto.OnBehalfOfChapterName = names.GetValueOrDefault(motion.OnBehalfOfChapterId ?? Guid.Empty);
            dto.OnBehalfOfReason = motion.OnBehalfOfReason;
            dto.HandedOverToMotionId = motion.HandedOverToMotionId;
            if (motion.HandedOverToMotionId != null && targetChapterIds.TryGetValue(motion.HandedOverToMotionId.Value, out var targetChapterId))
                dto.HandedOverToChapterName = names.GetValueOrDefault(targetChapterId);
        }
    }
}
