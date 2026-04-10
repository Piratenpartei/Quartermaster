using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleDeleteRequest {
    public Guid Id { get; set; }
}

public class RoleDeleteEndpoint : Endpoint<RoleDeleteRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public RoleDeleteEndpoint(RoleRepository roleRepo, UserGlobalPermissionRepository globalPermRepo) {
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Delete("/api/roles/{Id}");
    }

    public override async Task HandleAsync(RoleDeleteRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ManageRoles, _globalPermRepo)) {
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

        _roleRepo.Delete(req.Id);
        await SendOkAsync(ct);
    }
}
