using System;

namespace Quartermaster.Api.Permissions;

public class PermissionDTO {
    public Guid Id { get; set; }
    public string Identifier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Global { get; set; }
}
