using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Permissions;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Roles;

public class RoleCreateEndpoint : Endpoint<RoleCreateRequest, RoleDTO> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly PermissionContext _perms;

    public RoleCreateEndpoint(RoleRepository roleRepo, PermissionRepository permissionRepo,
        PermissionContext perms) {
        _roleRepo = roleRepo;
        _permissionRepo = permissionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/roles");
    }

    public override async Task HandleAsync(RoleCreateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Name)) {
            ThrowError(I18nKey.Error.User.Role.NameRequired);
            return;
        }

        if (!Enum.IsDefined(req.Scope)) {
            ThrowError(I18nKey.Error.User.Role.ScopeInvalid);
            return;
        }

        var scopeError = RolePermissionScopeValidator.Validate(req.Permissions, req.Scope, _permissionRepo);
        if (scopeError != null) {
            ThrowError(scopeError);
            return;
        }

        var role = new Role {
            Id = Guid.NewGuid(),
            Identifier = $"custom_{Guid.NewGuid():N}",
            Name = req.Name.Trim(),
            Description = req.Description ?? "",
            Scope = req.Scope,
            IsSystem = false
        };
        _roleRepo.Create(role);
        _roleRepo.SetPermissions(role.Id, req.Permissions);

        await SendAsync(new RoleDTO {
            Id = role.Id,
            Identifier = role.Identifier,
            Name = role.Name,
            Description = role.Description,
            Scope = role.Scope,
            IsSystem = role.IsSystem,
            Permissions = req.Permissions
        }, cancellation: ct);
    }
}
