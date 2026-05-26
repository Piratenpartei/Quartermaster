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
using Quartermaster.Data.Members;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Members;

public class MemberAdminDivisionUpdateRequest {
    public Guid Id { get; set; }
    public Guid? ResidenceAdministrativeDivisionId { get; set; }
}

public class MemberAdminDivisionUpdateEndpoint : Endpoint<MemberAdminDivisionUpdateRequest> {
    private readonly MemberRepository _memberRepo;
    private readonly AdministrativeDivisionRepository _adminDivRepo;
    private readonly DbContext _context;
    private readonly PermissionContext _perms;

    public MemberAdminDivisionUpdateEndpoint(
        MemberRepository memberRepo,
        AdministrativeDivisionRepository adminDivRepo,
        DbContext context,
        PermissionContext perms) {
        _memberRepo = memberRepo;
        _adminDivRepo = adminDivRepo;
        _context = context;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/members/{Id}/admindivision");
    }

    public override async Task HandleAsync(MemberAdminDivisionUpdateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var member = _memberRepo.Get(req.Id);
        if (member == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        if (!member.ChapterId.HasValue) {
            if (!_perms.HasGlobal(PermissionIdentifier.EditMembers)) {
                await SendForbiddenAsync(ct);
                return;
            }
        } else if (!_perms.Has(member.ChapterId.Value, PermissionIdentifier.EditMembers)) {
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
