using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AdministrativeDivisions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberAdminDivisionUpdateRequest {
    public Guid Id { get; set; }
    public Guid? ResidenceAdministrativeDivisionId { get; set; }
}

public class MemberAdminDivisionUpdateEndpoint : Endpoint<MemberAdminDivisionUpdateRequest> {
    private readonly MemberRepository _memberRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly UserChapterPermissionRepository _chapterPermRepo;
    private readonly ChapterRepository _chapterRepo;
    private readonly DbContext _context;

    public MemberAdminDivisionUpdateEndpoint(
        MemberRepository memberRepo,
        AdministrativeDivisionRepository adminDivRepo,
        UserGlobalPermissionRepository globalPermRepo,
        UserChapterPermissionRepository chapterPermRepo,
        ChapterRepository chapterRepo,
        DbContext context) {
        _memberRepo = memberRepo;
        _adminDivRepo = adminDivRepo;
        _globalPermRepo = globalPermRepo;
        _chapterPermRepo = chapterPermRepo;
        _chapterRepo = chapterRepo;
        _context = context;
    }

    public override void Configure() {
        Put("/api/members/{Id}/admindivision");
    }

    public override async Task HandleAsync(MemberAdminDivisionUpdateRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var member = _memberRepo.Get(req.Id);
        if (member == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!member.ChapterId.HasValue) {
            if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.EditMembers, _globalPermRepo)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (!EndpointAuthorizationHelper.HasPermission(userId.Value, member.ChapterId.Value, PermissionIdentifier.EditMembers, _globalPermRepo, _chapterPermRepo, _chapterRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (req.ResidenceAdministrativeDivisionId.HasValue) {
            var div = _adminDivRepo.Get(req.ResidenceAdministrativeDivisionId.Value);
            if (div == null) {
                ThrowError(I18nKey.Error.Member.AdminDivision.NotFound);
                return;
            }
        }

        _context.Members
            .Where(m => m.Id == req.Id)
            .Set(m => m.ResidenceAdministrativeDivisionId, req.ResidenceAdministrativeDivisionId)
            .Update();

        await SendOkAsync(ct);
    }
}
