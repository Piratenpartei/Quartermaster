using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Users;

public class GrantGlobalPermissionRequest {
    public Guid UserId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class GrantGlobalPermissionEndpoint : Endpoint<GrantGlobalPermissionRequest> {
    private readonly UserGlobalPermissionRepository _globalPermRepo;
    private readonly PermissionRepository _permissionRepo;

    public GrantGlobalPermissionEndpoint(UserGlobalPermissionRepository globalPermRepo,
        PermissionRepository permissionRepo) {
        _globalPermRepo = globalPermRepo;
        _permissionRepo = permissionRepo;
    }

    public override void Configure() {
        Post("/api/users/{UserId}/permissions/global");
    }

    public override async Task HandleAsync(GrantGlobalPermissionRequest req, CancellationToken ct) {
        var callerId = EndpointAuthorizationHelper.GetUserId(User);
        if (callerId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(callerId.Value, PermissionIdentifier.CreateUser, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var permission = _permissionRepo.GetByIdentifier(req.PermissionIdentifier);
        if (permission == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        _globalPermRepo.AddForUser(req.UserId, permission);
        await SendOkAsync(ct);
    }
}
