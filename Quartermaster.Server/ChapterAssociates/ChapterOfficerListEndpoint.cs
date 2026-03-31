using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Data.ChapterAssociates;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerListEndpoint : Endpoint<ChapterOfficerSearchRequest, ChapterOfficerSearchResponse> {
    private readonly ChapterOfficerRepository _officerRepo;

    public ChapterOfficerListEndpoint(ChapterOfficerRepository officerRepo) {
        _officerRepo = officerRepo;
    }

    public override void Configure() {
        Get("/api/chapterofficers");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChapterOfficerSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _officerRepo.SearchAll(req.Query, req.ChapterId, req.Page, req.PageSize);

        await SendAsync(new ChapterOfficerSearchResponse {
            Items = items.Select(x => new ChapterOfficerDTO {
                MemberId = x.Officer.MemberId,
                MemberNumber = x.Member.MemberNumber,
                MemberFirstName = x.Member.FirstName,
                MemberLastName = x.Member.LastName,
                ChapterId = x.Officer.ChapterId,
                ChapterName = x.Chapter.Name,
                AssociateType = (int)x.Officer.AssociateType
            }).ToList(),
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
