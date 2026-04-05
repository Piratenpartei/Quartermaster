using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Roles;

[Table(TableName, IsColumnAttributeRequired = false)]
public class UserRoleAssignment {
    public const string TableName = "UserRoleAssignments";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? ChapterId { get; set; }
}
