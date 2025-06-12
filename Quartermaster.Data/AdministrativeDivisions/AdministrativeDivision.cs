using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AdministrativeDivisions;

[Table("AdministrativeDivisions", IsColumnAttributeRequired = false)]
public class AdministrativeDivision {
    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = "";
    public int Depth { get; set; }

    public int? AdminCode { get; set; }
    public string? PostCode { get; set; }
}