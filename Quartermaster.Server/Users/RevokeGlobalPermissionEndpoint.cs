using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class RevokeGlobalPermissionEndpoint : Endpoint<RevokeGlobalPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly PermissionContext _perms;

    public RevokeGlobalPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        PermissionRepository permissionRepo, PermissionContext perms) {
        _globalPermRepo = globalPermRepo;
        _permissionRepo = permissionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/users/{UserId}/permissions/global");
    }

    public override async Task HandleAsync(RevokeGlobalPermissionRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.CreateUser)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var permission = _permissionRepo.GetByIdentifier(req.PermissionIdentifier);
        if (permission == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _globalPermRepo.RemoveForUser(req.UserId, permission);
        await SendOkAsync(ct);
    }
}
