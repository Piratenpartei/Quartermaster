using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Permissions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Permission {
    public const string TableName = "Permissions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Identifier { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Global { get; set; }
}