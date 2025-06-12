using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserGlobalPermissions;

[Table("UserGlobalPermissions", IsColumnAttributeRequired = false)]
public class UserGlobalPermission {
    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
}