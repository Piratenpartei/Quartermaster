using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;

namespace Quartermaster.Server.Members;

public class MemberListEndpoint : Endpoint<MemberSearchRequest, MemberSearchResponse> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;

    public MemberListEndpoint(MemberRepository memberRepo, ChapterRepository chapterRepo) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
    }

    public override void Configure() {
        Get("/api/members");
    }

    public override async Task HandleAsync(MemberSearchRequest req, CancellationToken ct) {
        var (items, totalCount) = _memberRepo.Search(req.Query, req.ChapterId, req.Page, req.PageSize);
        var chapters = _chapterRepo.GetAll().ToDictionary(c => c.Id, c => c.Name);

        var dtos = items.Select(m => new MemberDTO {
            Id = m.Id,
            MemberNumber = m.MemberNumber,
            FirstName = m.FirstName,
            LastName = m.LastName,
            PostCode = m.PostCode,
            City = m.City,
            ChapterId = m.ChapterId,
            ChapterName = m.ChapterId.HasValue && chapters.TryGetValue(m.ChapterId.Value, out var name) ? name : "",
            EntryDate = m.EntryDate,
            ExitDate = m.ExitDate,
            IsPending = m.IsPending,
            HasVotingRights = m.HasVotingRights
        }).ToList();

        await SendAsync(new MemberSearchResponse {
            Items = dtos,
            TotalCount = totalCount
        }, cancellation: ct);
    }
}
