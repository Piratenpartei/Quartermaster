using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;
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

        var member = _memberRepo.Get(req.MemberId);
        if (member == null) {
            AddError(r => r.MemberId, I18nKey.Error.Chapter.Officer.MemberNotFound);
            await SendErrorsAsync(cancellation: ct);
            return;
        }
        // Cross-chapter IDOR guard: a caller with EditOfficers on chapter X must not be able
        // to promote a member belonging to chapter Y — doing so would also grant that member's
        // user account the officer role for chapter X via GrantDefaultPermissions below.
        if (member.ChapterId != req.ChapterId) {
            AddError(r => r.MemberId, I18nKey.Error.Chapter.Officer.MemberChapterMismatch);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        _officerRepo.Create(new ChapterOfficer {
            MemberId = req.MemberId,
            ChapterId = req.ChapterId,
            AssociateType = (ChapterOfficerType)req.AssociateType
        });

        if (member.UserId.HasValue)
            _officerRepo.GrantDefaultPermissions(member.UserId.Value, req.ChapterId);

        await SendOkAsync(ct);
    }
}
