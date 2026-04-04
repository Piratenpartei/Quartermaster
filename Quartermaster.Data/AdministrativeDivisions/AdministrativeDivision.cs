using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AdministrativeDivisions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AdministrativeDivision {
    public const string TableName = "AdministrativeDivisions";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = "";
    public int Depth { get; set; }

    public string? AdminCode { get; set; }
    public string? PostCodes { get; set; }
    public bool IsOrphaned { get; set; }
}