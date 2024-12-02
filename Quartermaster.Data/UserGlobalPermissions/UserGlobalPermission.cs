using System;

namespace Quartermaster.Data.UserGlobalPermissions;

public class UserGlobalPermission {
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
}