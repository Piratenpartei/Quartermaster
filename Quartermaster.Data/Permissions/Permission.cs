using System;

namespace Quartermaster.Data.Permissions;

public class Permission {
    public Guid Id { get; set; }
    public string Identifier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Global { get; set; }
}