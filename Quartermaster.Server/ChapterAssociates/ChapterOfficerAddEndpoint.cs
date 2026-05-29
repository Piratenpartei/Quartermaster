using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddEndpoint : Endpoint<ChapterOfficerAddRequest> {
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly PermissionContext _perms;

    public ChapterOfficerAddEndpoint(ChapterOfficerRepository officerRepo,
        MemberRepository memberRepo, ChapterRepository chapterRepo, PermissionContext perms) {
        _officerRepo = officerRepo;
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
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
        // Cross-chapter IDOR guard: block lateral promotion (e.g. Bayern caller promoting a
        // Niedersachsen member). Allowed cases: the member's own chapter, or any ancestor of it
        // (PPDE root chapter has no direct local members — federal-only Direktmitglieder are
        // attributed to root by the import — so the request chapter may be the member's own
        // chapter or any chapter on the way up to the root).
        if (member.ChapterId == null
            || (member.ChapterId != req.ChapterId
                && !_chapterRepo.GetAncestorChainIds(member.ChapterId.Value).Contains(req.ChapterId))) {
            AddError(r => r.MemberId, I18nKey.Error.Chapter.Officer.MemberChapterMismatch);
            await SendErrorsAsync(cancellation: ct);
            return;
        }

        _officerRepo.Create(new ChapterOfficer {
            MemberId = req.MemberId,
            ChapterId = req.ChapterId,
            AssociateType = (ChapterOfficerType)req.AssociateType
        });

        // Officer permissions attach to the member's user account. A member with no user yet
        // (not logged in via SSO) gets the officer role automatically when SSO first links them
        // — see SsoLoginHelper.GrantDefaultPermissionsForAllChapters. Votes don't need a user;
        // they're recorded against the officer member directly.
        if (member.UserId.HasValue)
            _officerRepo.GrantDefaultPermissions(member.UserId.Value, req.ChapterId);

        await SendOkAsync(ct);
    }
}
