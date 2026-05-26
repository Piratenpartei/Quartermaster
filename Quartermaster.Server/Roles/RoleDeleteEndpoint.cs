using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleDeleteRequest {
    public Guid Id { get; set; }
}

public class RoleDeleteEndpoint : Endpoint<RoleDeleteRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionContext _perms;

    public RoleDeleteEndpoint(RoleRepository roleRepo, PermissionContext perms) {
        _roleRepo = roleRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/roles/{Id}");
    }

    public override async Task HandleAsync(RoleDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var role = _roleRepo.Get(req.Id);
        if (role == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (role.IsSystem) {
            ThrowError(I18nKey.Error.User.Role.SystemNotDeletable);
            return;
        }

        var result = _roleRepo.Delete(req.Id);
        if (result == RoleRepository.RoleDeleteResult.HasAssignments) {
            ThrowError(I18nKey.Error.User.Role.HasActiveAssignments);
            return;
        }
        await SendOkAsync(ct);
    }
}
