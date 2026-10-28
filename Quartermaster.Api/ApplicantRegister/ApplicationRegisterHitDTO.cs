using System;

namespace Quartermaster.Api.ApplicantRegister;

/// <summary>A register entry an application matched, with the entry's kind, date and names.</summary>
public class ApplicationRegisterHitDTO {
    public Guid Id { get; set; }
    public Guid EntryId { get; set; }
    public ApplicantRegisterKind Kind { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public RegisterMatchFields MatchedFields { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
}
