using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleCreateEndpoint : Endpoint<RoleCreateRequest, RoleDTO> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly UserGlobalPermissionRepository _globalPermRepo;

    public RoleCreateEndpoint(RoleRepository roleRepo, PermissionRepository permissionRepo,
        UserGlobalPermissionRepository globalPermRepo) {
        _roleRepo = roleRepo;
        _permissionRepo = permissionRepo;
        _globalPermRepo = globalPermRepo;
    }

    public override void Configure() {
        Post("/api/roles");
    }

    public override async Task HandleAsync(RoleCreateRequest req, CancellationToken ct) {
        var userId = EndpointAuthorizationHelper.GetUserId(User);
        if (userId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!EndpointAuthorizationHelper.HasGlobalPermission(userId.Value, PermissionIdentifier.ManageRoles, _globalPermRepo)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Name)) {
            ThrowError(I18nKey.Error.User.Role.NameRequired);
            return;
        }

        if (req.Scope != 0 && req.Scope != 1) {
            ThrowError(I18nKey.Error.User.Role.ScopeInvalid);
            return;
        }

        var scopeError = RolePermissionScopeValidator.Validate(req.Permissions, (RoleScope)req.Scope, _permissionRepo);
        if (scopeError != null) {
            ThrowError(scopeError);
            return;
        }

        var role = new Role {
            Id = Guid.NewGuid(),
            Identifier = $"custom_{Guid.NewGuid():N}",
            Name = req.Name.Trim(),
            Description = req.Description ?? "",
            Scope = (RoleScope)req.Scope,
            IsSystem = false
        };
        _roleRepo.Create(role);
        _roleRepo.SetPermissions(role.Id, req.Permissions);

        await SendAsync(RoleDtoBuilder.ToDto(role, req.Permissions), cancellation: ct);
    }
}
