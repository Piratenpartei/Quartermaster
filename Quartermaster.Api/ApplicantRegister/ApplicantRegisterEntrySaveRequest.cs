using System;

namespace Quartermaster.Api.ApplicantRegister;

public class ApplicantRegisterEntrySaveRequest {
    public Guid Id { get; set; }
    public ApplicantRegisterKind Kind { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly? DateOfBirth { get; set; }
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public DateOnly OccurredOn { get; set; }
    public string? Note { get; set; }
    public Guid? ChapterId { get; set; }
}
