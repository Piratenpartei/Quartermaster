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

public class RoleUpdateEndpoint : Endpoint<RoleUpdateRequest> {
    private readonly RoleRepository _roleRepo;
    private readonly PermissionRepository _permissionRepo;
    private readonly PermissionContext _perms;

    public RoleUpdateEndpoint(RoleRepository roleRepo, PermissionRepository permissionRepo,
        PermissionContext perms) {
        _roleRepo = roleRepo;
        _permissionRepo = permissionRepo;
        _perms = perms;
    }

    public override void Configure() {
        Put("/api/roles/{Id}");
    }

    public override async Task HandleAsync(RoleUpdateRequest req, CancellationToken ct) {
        if (_perms.UserId == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (!_perms.HasGlobal(PermissionIdentifier.ManageRoles)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var role = _roleRepo.Get(req.Id);
        if (role == null) {
            await SendNotFoundAsync(ct);
            return;
        }
        if (role.IsSystem) {
            ThrowError(I18nKey.Error.User.Role.SystemNotEditable);
            return;
        }
        if (string.IsNullOrWhiteSpace(req.Name)) {
            ThrowError(I18nKey.Error.User.Role.NameRequired);
            return;
        }

        var scopeError = RolePermissionScopeValidator.Validate(req.Permissions, role.Scope, _permissionRepo);
        if (scopeError != null) {
            ThrowError(scopeError);
            return;
        }

        role.Name = req.Name.Trim();
        role.Description = req.Description ?? "";
        _roleRepo.Update(role);
        _roleRepo.SetPermissions(role.Id, req.Permissions);

        await SendOkAsync(ct);
    }
}
