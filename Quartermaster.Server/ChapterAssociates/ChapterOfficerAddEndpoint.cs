using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddEndpoint : Endpoint<ChapterOfficerAddRequest> {
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;

    public ChapterOfficerAddEndpoint(ChapterOfficerRepository officerRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo, MemberRepository memberRepo) {
        _officerRepo = officerRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
    }

    public override void Configure() {
        Post("/api/chapterofficers");
    }

    public override async Task HandleAsync(ChapterOfficerAddRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasPermission(userId.Value, req.ChapterId, PermissionIdentifier.EditOfficers, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _officerRepo.Create(new ChapterOfficer {
            MemberId = req.MemberId,
            ChapterId = req.ChapterId,
            AssociateType = (ChapterOfficerType)req.AssociateType
        });

        var member = _memberRepo.Get(req.MemberId);
        if (member?.UserId.HasValue == true)
            _officerRepo.GrantDefaultPermissions(member.UserId.Value, req.ChapterId);

        await SendOkAsync(ct);
    }
}
