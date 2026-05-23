using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Roles;

[Table(TableName, IsColumnAttributeRequired = false)]
public class RolePermission {
    public const string TableName = "RolePermissions";

    [PrimaryKey(Order = 0)]
    public Guid RoleId { get; set; }
    [PrimaryKey(Order = 1)]
    public string PermissionIdentifier { get; set; } = "";
}
