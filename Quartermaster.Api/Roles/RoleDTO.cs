using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Roles;

public class RoleDTO {
    public Guid Id { get; set; }
    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Scope { get; set; }
    public bool IsSystem { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RoleCreateRequest {
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Scope { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class RoleUpdateRequest {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Permissions { get; set; } = new();
}

public class UserRoleAssignmentDTO {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = "";
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public int RoleScope { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ChapterName { get; set; }
}

public class RoleAssignmentCreateRequest {
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public Guid? ChapterId { get; set; }
}
