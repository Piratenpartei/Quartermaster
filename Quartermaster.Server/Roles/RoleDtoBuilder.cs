using System.Collections.Generic;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;

namespace Quartermaster.Server.Roles;

public static class RoleDtoBuilder {
    public static RoleDTO ToDto(Role r, List<string> permissions) => new() {
        Id = r.Id,
        Identifier = r.Identifier,
        Name = r.Name,
        Description = r.Description,
        Scope = (int)r.Scope,
        IsSystem = r.IsSystem,
        Permissions = permissions
    };
}
