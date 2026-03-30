using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Options;

[Table(TableName, IsColumnAttributeRequired = false)]
public class SystemOption {
    public const string TableName = "SystemOptions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Identifier { get; set; } = "";
    public string Value { get; set; } = "";
    public Guid? ChapterId { get; set; }
}
