using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleListEndpoint : EndpointWithoutRequest<List<RoleDTO>> {
    private readonly RoleRepository _roleRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public RoleListEndpoint(RoleRepository roleRepo, UserGlobalPermissionRepository globalPermRepo) {
        _roleRepo = roleRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/roles");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ManageRoles, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var roles = _roleRepo.GetAll();
        var dtos = roles.Select(r => RoleDtoBuilder.ToDto(r, _roleRepo.GetPermissions(r.Id))).ToList();
        await SendAsync(dtos, cancellation: ct);
    }
}
