using System;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationDetailDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public DueSelectionAdminDTO? DueSelection { get; set; }
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";
    public DateOnly EntryDate { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public Guid? LinkedMotionId { get; set; }
    public int? MemberNumber { get; set; }
    public DateTimeOffset? WelcomeSentAt { get; set; }
}
