using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddEndpoint : Endpoint<ChapterOfficerAddRequest> {
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly MemberRepository _memberRepo;
    private readonly PermissionContext _perms;

    public ChapterOfficerAddEndpoint(ChapterOfficerRepository officerRepo,
        MemberRepository memberRepo, PermissionContext perms) {
        _officerRepo = officerRepo;
        _memberRepo = memberRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/chapterofficers");
    }

    public override async Task HandleAsync(ChapterOfficerAddRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.EditOfficers)) {
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
