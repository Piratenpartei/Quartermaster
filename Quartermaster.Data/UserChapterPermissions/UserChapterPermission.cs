using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.UserChapterPermissions;

[Table("UserChapterPermissions", IsColumnAttributeRequired = false)]
public class UserChapterPermission {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public Guid PermissionId { get; set; }
}