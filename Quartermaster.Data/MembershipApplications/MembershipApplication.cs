using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.MembershipApplications;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MembershipApplication {
    public const string TableName = "MembershipApplications";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

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
    public Guid? AddressAdministrativeDivisionId { get; set; }

    // Chapter
    public Guid? ChapterId { get; set; }

    // Dues (references the DueSelection created alongside)
    public Guid? DueSelectionId { get; set; }

    // Declarations
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    // Entry date
    public DateTime EntryDate { get; set; }
    public DateTime SubmittedAt { get; set; }

    // Processing
    public ApplicationStatus Status { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}

public enum ApplicationStatus {
    Pending,
    Approved,
    Rejected
}
