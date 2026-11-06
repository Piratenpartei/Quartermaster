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
    private readonly DetailPageAccess _pageAccess;

    public MemberDetailEndpoint(
        MemberRepository memberRepo,
        ChapterRepository chapterRepo,
        AdministrativeDivisionRepository adminDivRepo,
        PermissionContext perms,
        DetailPageAccess pageAccess) {
        _memberRepo = memberRepo;
        _chapterRepo = chapterRepo;
        _adminDivRepo = adminDivRepo;
        _perms = perms;
        _pageAccess = pageAccess;
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

        if (!_pageAccess.CanViewMember(member)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var chapter = member.ChapterId.HasValue ? _chapterRepo.Get(member.ChapterId.Value) : null;
        var div = member.ResidenceAdministrativeDivisionId.HasValue
            ? _adminDivRepo.Get(member.ResidenceAdministrativeDivisionId.Value)
            : null;

        var dto = member.ToDetailDto(chapter?.Name ?? "");
        dto.ResidenceAdministrativeDivisionName = div?.Name ?? "";
        dto.ResidenceAdministrativeDivisionAdminCode = div?.AdminCode;
        dto.IsAdminDivisionOrphaned = div?.IsOrphaned ?? false;
        if (member.UserId.HasValue && _pageAccess.CanViewUsers())
            dto.ViewableUserId = member.UserId;
        await SendAsync(dto, cancellation: ct);
    }
}
