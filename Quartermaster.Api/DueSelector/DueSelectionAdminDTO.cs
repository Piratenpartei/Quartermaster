using System;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? EMail { get; set; }
    public decimal SelectedDue { get; set; }
    public decimal ReducedAmount { get; set; }
    public string ReducedJustification { get; set; } = "";
    public int SelectedValuation { get; set; }
    public int Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
