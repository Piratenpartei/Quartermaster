using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserGlobalPermissions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class UserGlobalPermission {
    public const string TableName = "UserGlobalPermissions";

    [PrimaryKey(Order = 0)]
    public Guid UserId { get; set; }
    [PrimaryKey(Order = 1)]
    public Guid PermissionId { get; set; }
}