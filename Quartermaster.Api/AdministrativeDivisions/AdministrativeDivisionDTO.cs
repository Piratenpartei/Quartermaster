using System;

namespace Quartermaster.Api.AdministrativeDivisions;

public class AdministrativeDivisionDTO {
    public Guid Id { get; set; }
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public int Depth { get; set; }
    public string? AdminCode { get; set; }
    public string? PostCodes { get; set; }

    /// <summary>
    /// A single representative post code for this division. For municipalities (whose raw
    /// <see cref="PostCodes"/> is a Landkreis-wide aggregate) this is resolved from the
    /// same-named child locality; for leaf localities it's their own first code.
    /// </summary>
    public string? PrimaryPostCode { get; set; }
}
