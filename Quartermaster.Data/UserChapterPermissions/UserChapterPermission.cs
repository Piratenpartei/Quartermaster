using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserChapterPermissions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class UserChapterPermission {
    public const string TableName = "UserChapterPermissions";

    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public Guid PermissionId { get; set; }
}