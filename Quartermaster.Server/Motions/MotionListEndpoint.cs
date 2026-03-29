using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Motions;

public class MotionListEndpoint : Endpoint<MotionListRequest, MotionListResponse> {
    private readonly MotionRepository _motionRepo;
    private readonly ChapterRepository _chapterRepo;

    public MotionListEndpoint(MotionRepository motionRepo, ChapterRepository chapterRepo) {
        _motionRepo = motionRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/motions");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MotionListRequest req, CancellationToken ct) {
        MotionApprovalStatus? status = req.ApprovalStatus.HasValue
            ? (MotionApprovalStatus)req.ApprovalStatus.Value
            : null;

        var (items, totalCount) = _motionRepo.List(
            req.ChapterId, status, req.IncludeNonPublic, req.Page, req.PageSize);

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
