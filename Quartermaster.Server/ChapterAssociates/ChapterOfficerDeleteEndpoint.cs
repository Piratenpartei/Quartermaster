using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.ChapterAssociates;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerDeleteRequest {
    public Guid MemberId { get; set; }
    public Guid ChapterId { get; set; }
}

public class ChapterOfficerDeleteEndpoint : Endpoint<ChapterOfficerDeleteRequest> {
    private readonly ChapterOfficerRepository _officerRepo;
    private readonly MemberRepository _memberRepo;
    private readonly PermissionContext _perms;

    public ChapterOfficerDeleteEndpoint(ChapterOfficerRepository officerRepo,
        MemberRepository memberRepo, PermissionContext perms) {
        _officerRepo = officerRepo;
        _memberRepo = memberRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/chapterofficers");
    }

    public override async Task HandleAsync(ChapterOfficerDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.Has(req.ChapterId, PermissionIdentifier.EditOfficers)) {
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
