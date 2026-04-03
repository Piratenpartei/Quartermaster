using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerDeleteRequest {
    public Guid MemberId { get; set; }
    public Guid ChapterId { get; set; }
}

public class ChapterOfficerDeleteEndpoint : Endpoint<ChapterOfficerDeleteRequest> {
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;

    public ChapterOfficerDeleteEndpoint(ChapterOfficerRepository officerRepo,
        UserChapterPermissionRepository chapterPermRepo, UserGlobalPermissionRepository globalPermRepo,
        ChapterRepository chapterRepo, MemberRepository memberRepo) {
        _officerRepo = officerRepo;
        _chapterPermRepo = chapterPermRepo;
        _globalPermRepo = globalPermRepo;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
    }

    public override void Configure() {
        Delete("/api/chapterofficers");
    }

    public override async Task HandleAsync(ChapterOfficerDeleteRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditOfficers, _globalPermRepo) &&
            !_chapterPermRepo.HasPermissionWithInheritance(userId.Value, req.ChapterId, PermissionIdentifier.EditOfficers, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var member = _memberRepo.Get(req.MemberId);
        if (member?.UserId.HasValue == true)
            _officerRepo.RevokeDefaultPermissions(member.UserId.Value, req.ChapterId);

        _officerRepo.Delete(req.MemberId, req.ChapterId);
        await SendOkAsync(ct);
    }
}
