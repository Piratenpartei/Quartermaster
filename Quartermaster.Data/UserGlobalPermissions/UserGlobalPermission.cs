using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserGlobalPermissions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class UserGlobalPermission {
    public const string TableName = "UserGlobalPermissions";

    public Guid UserId { get; set; }
    public Guid PermissionId { get; set; }
}