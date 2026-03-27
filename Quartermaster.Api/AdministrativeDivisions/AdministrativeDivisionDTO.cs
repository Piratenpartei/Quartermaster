using System;

namespace Quartermaster.Api.AdministrativeDivisions;

public class AdministrativeDivisionDTO {
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public int Depth { get; set; }
    public string? AdminCode { get; set; }
    public string? PostCodes { get; set; }
}
