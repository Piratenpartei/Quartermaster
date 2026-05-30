using System;

namespace Quartermaster.Api.DueSelector;

public class DueSelectionAdminDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public decimal SelectedDue { get; set; }
    public decimal ReducedAmount { get; set; }
    public string ReducedJustification { get; set; } = "";
    public SelectedValuation SelectedValuation { get; set; }
    public DueSelectionStatus Status { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
