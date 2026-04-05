using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Roles;

[Table(TableName, IsColumnAttributeRequired = false)]
public class RolePermission {
    public const string TableName = "RolePermissions";

    public Guid RoleId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}
