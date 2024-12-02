using System;

namespace Quartermaster.Data.UserChapterPermissions;

public class UserChapterPermission {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public Guid PermissionId { get; set; }
}