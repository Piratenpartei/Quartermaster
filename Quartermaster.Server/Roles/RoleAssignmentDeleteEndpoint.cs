using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleAssignmentDeleteRequest {
    public Guid Id { get; set; }
}

public class RoleAssignmentDeleteEndpoint : Endpoint<RoleAssignmentDeleteRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public RoleAssignmentDeleteEndpoint(RoleRepository roleRepo, UserGlobalPermissionRepository globalPermRepo) {
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Delete("/api/roleassignments/{Id}");
    }

    public override async Task HandleAsync(RoleAssignmentDeleteRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ManageRoles, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _roleRepo.RevokeAssignment(req.Id);
        await SendOkAsync(ct);
    }
}
