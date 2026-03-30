using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Options;

[Table(TableName, IsColumnAttributeRequired = false)]
public class OptionDefinition {
    public const string TableName = "OptionDefinitions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Identifier { get; set; } = "";
    public string FriendlyName { get; set; } = "";
    public OptionDataType DataType { get; set; }
    public bool IsOverridable { get; set; }
    public string TemplateModels { get; set; } = "";
}

public enum OptionDataType {
    String,
    Number,
    Template
}
