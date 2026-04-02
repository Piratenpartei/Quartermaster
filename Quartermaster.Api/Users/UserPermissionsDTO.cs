using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Users;

public class UserPermissionsDTO {
    public List<string> GlobalPermissions { get; set; } = new();
    public Dictionary<string, List<string>> ChapterPermissions { get; set; } = new();
}

public class PermissionGrantRequest {
    public string PermissionIdentifier { get; set; } = "";
}

public class ChapterPermissionGrantRequest {
    public Guid ChapterId { get; set; }
    public string PermissionIdentifier { get; set; } = "";
}
