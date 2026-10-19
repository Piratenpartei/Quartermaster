using System;

namespace Quartermaster.Api.TravelExpenses;

public enum TravelExpenseStatusDTO {
    Draft = 0,
    Submitted = 1
}

/// <summary>One row in the "Meine Formulare" listing.</summary>
public class TravelExpenseListItemDTO {
    public Guid Id { get; set; }
    public TravelExpenseStatusDTO Status { get; set; }
    public string ChapterName { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTime? StartAt { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

public class TravelExpenseResponseDTO {
    public Guid Id { get; set; }
    public TravelExpenseStatusDTO Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public TravelExpenseInputDTO Input { get; set; } = new();
}
