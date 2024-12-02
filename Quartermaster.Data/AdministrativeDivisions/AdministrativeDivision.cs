using System;

namespace Quartermaster.Data.AdministrativeDivisions;

public class AdministrativeDivision {
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }

    public string Name { get; set; } = "";
    public int Depth { get; set; }

    public int? AdminCode { get; set; }
    public string? PostCode { get; set; }
}