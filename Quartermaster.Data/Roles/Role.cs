using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Roles;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Role {
    public const string TableName = "Roles";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Identifier { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public RoleScope Scope { get; set; }
    public bool IsSystem { get; set; }

    /// <summary>
    /// When true (default), permissions granted via this role to a chapter also apply to
    /// descendant chapters via the view-permission inheritance mechanism.
    /// When false, permissions only apply to the exact chapter the role is assigned to —
    /// used for delegate-style roles where access shouldn't cascade into child chapters.
    /// </summary>
    public bool InheritsToChildren { get; set; } = true;
}

public enum RoleScope {
    Global = 0,
    ChapterScoped = 1
}
