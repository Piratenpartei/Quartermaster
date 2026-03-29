using System;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationDetailDTO {
    public Guid Id { get; set; }

    // Personal data
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "";
    public string EMail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    // Address
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";

    // Chapter
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";

    // Dues
    public DueSelectionAdminDTO? DueSelection { get; set; }

    // Declarations
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    // Dates
    public DateTime EntryDate { get; set; }
    public DateTime SubmittedAt { get; set; }

    // Processing
    public int Status { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
