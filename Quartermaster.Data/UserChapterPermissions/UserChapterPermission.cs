using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserChapterPermissions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class UserChapterPermission {
    public const string TableName = "UserChapterPermissions";

    [PrimaryKey(Order = 0)]
    public Guid UserId { get; set; }
    [PrimaryKey(Order = 1)]
    public Guid ChapterId { get; set; }
    [PrimaryKey(Order = 2)]
    public Guid PermissionId { get; set; }
}