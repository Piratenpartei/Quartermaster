using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberDetailRequest {
    public Guid Id { get; set; }
}

public class MemberDetailEndpoint : Endpoint<MemberDetailRequest, MemberDetailDTO> {
    private readonly MemberRepository _memberRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly PermissionContext _perms;

    public MemberDetailEndpoint(
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        PermissionContext perms) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/members/{Id}");
    }

    public override async Task HandleAsync(MemberDetailRequest req, CancellationToken ct) {
        var member = _memberRepo.Get(req.Id);
        if (member == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        if (!member.ChapterId.HasValue) {
            if (!_perms.HasGlobal(PermissionIdentifier.ViewAllMembers)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (!_perms.Has(member.ChapterId.Value, PermissionIdentifier.ViewAllMembers, PermissionIdentifier.ViewMembers)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapter = member.ChapterId.HasValue ? _chapterRepo.Get(member.ChapterId.Value) : null;
        var div = member.ResidenceAdministrativeDivisionId.HasValue
            ? _adminDivRepo.Get(member.ResidenceAdministrativeDivisionId.Value)
            : null;

        var dto = member.ToDetailDto(chapter?.Name ?? "");
        dto.ResidenceAdministrativeDivisionName = div?.Name ?? "";
        dto.IsAdminDivisionOrphaned = div?.IsOrphaned ?? false;
        await SendAsync(dto, cancellation: ct);
    }
}
