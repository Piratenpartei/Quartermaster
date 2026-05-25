using System;

namespace Quartermaster.Api.Users;

public class GrantGlobalPermissionRequest {
    public Guid UserId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class GrantChapterPermissionRequest {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class RevokeGlobalPermissionRequest {
    public Guid UserId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}

public class RevokeChapterPermissionRequest {
    public Guid UserId { get; set; }
    public Guid ChapterId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}
