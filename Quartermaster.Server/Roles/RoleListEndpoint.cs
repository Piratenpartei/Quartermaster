using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleListEndpoint : EndpointWithoutRequest<List<RoleDTO>> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionContext _perms;

    public RoleListEndpoint(RoleRepository roleRepo, PermissionContext perms) {
        _roleRepo = roleRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/roles");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var roles = _roleRepo.GetAll();
        var dtos = roles.Select(r => new RoleDTO {
            Id = r.Id,
            Identifier = r.Identifier,
            Name = r.Name,
            Description = r.Description,
            Scope = r.Scope,
            IsSystem = r.IsSystem,
            Permissions = _roleRepo.GetPermissions(r.Id)
        }).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
