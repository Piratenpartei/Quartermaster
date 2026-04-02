using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Permissions;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Permissions;

public class PermissionListEndpoint : EndpointWithoutRequest<List<PermissionDTO>> {
    private readonly PermissionRepository _permissionRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public PermissionListEndpoint(PermissionRepository permissionRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _permissionRepo = permissionRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Get("/api/permissions");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ViewUsers, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var permissions = _permissionRepo.GetAll();
        var dtos = permissions.Select(p => new PermissionDTO {
            Id = p.Id,
            Identifier = p.Identifier,
            DisplayName = p.DisplayName,
            Global = p.Global
        }).ToList();

        await SendAsync(dtos, cancellation: ct);
    }
}
