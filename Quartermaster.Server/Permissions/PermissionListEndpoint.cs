using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Permissions;
using Quartermaster.Data.Permissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Permissions;

public class PermissionListEndpoint : EndpointWithoutRequest<List<PermissionDTO>> {
    private readonly PermissionRepository _permissionRepo;
    private readonly PermissionContext _perms;

    public PermissionListEndpoint(PermissionRepository permissionRepo,
        PermissionContext perms) {
        _permissionRepo = permissionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/permissions");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ViewUsers)) {
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
