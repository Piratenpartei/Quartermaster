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
}

public enum RoleScope {
    Global = 0,
    ChapterScoped = 1
}
