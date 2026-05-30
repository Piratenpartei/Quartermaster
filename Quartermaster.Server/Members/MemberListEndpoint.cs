using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberListEndpoint : Endpoint<MemberSearchRequest, MemberSearchResponse> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public MemberListEndpoint(
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        PermissionContext perms) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/members");
    }

    public override async Task HandleAsync(MemberSearchRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var allowedChapterIds = _perms.GetPermittedChapterIds(
            PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers);
        if (allowedChapterIds is { Count: 0 }) {
            await SendForbiddenAsync(ct);
            return;
        }

        var (items, totalCount) = _memberRepo.Search(req.Query, req.ChapterId, req.Page, req.PageSize, allowedChapterIds, req.OrphanedOnly);
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
            EntryDate = m.EntryDate.ToDtoDate(),
            ExitDate = m.ExitDate.ToDtoDate(),
            IsPending = m.IsPending,
            HasVotingRights = m.HasVotingRights
        }).ToList();

        await SendAsync(new MemberSearchResponse {
            Items = dtos,
            TotalCount = totalCount,
            Page = req.Page,
            PageSize = req.PageSize
        }, cancellation: ct);
    }
}
