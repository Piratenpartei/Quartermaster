using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleAssignmentDeleteRequest {
    public Guid Id { get; set; }
}

public class RoleAssignmentDeleteEndpoint : Endpoint<RoleAssignmentDeleteRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionContext _perms;

    public RoleAssignmentDeleteEndpoint(RoleRepository roleRepo, PermissionContext perms) {
        _roleRepo = roleRepo;
        _perms = perms;
    }

    public override void Configure() {
        Delete("/api/roleassignments/{Id}");
    }

    public override async Task HandleAsync(RoleAssignmentDeleteRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        _roleRepo.RevokeAssignment(req.Id);
        await SendOkAsync(ct);
    }
}
