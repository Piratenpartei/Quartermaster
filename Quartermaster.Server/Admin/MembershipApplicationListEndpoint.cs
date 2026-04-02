using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Admin;

public class MembershipApplicationListEndpoint
    : Endpoint<MembershipApplicationListRequest, MembershipApplicationListResponse> {

    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly ChapterRepository _chapterRepo;

    public MembershipApplicationListEndpoint(
        MembershipApplicationRepository applicationRepo,
        ChapterRepository chapterRepo) {
        _applicationRepo = applicationRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/admin/membershipapplications");
    }

    public override async Task HandleAsync(MembershipApplicationListRequest req, CancellationToken ct) {
        var chapterIds = req.ChapterId.HasValue
            ? _chapterRepo.GetDescendantIds(req.ChapterId.Value)
            : new System.Collections.Generic.List<System.Guid>();

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
