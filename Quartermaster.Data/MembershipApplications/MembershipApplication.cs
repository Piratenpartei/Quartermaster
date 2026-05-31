using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Api.MembershipApplications;

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
    public string Email { get; set; } = "";
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
    public DateTime? AnonymizedAt { get; set; }

    // Manual member activation
    public int? MemberNumber { get; set; }
    public DateTime? WelcomeSentAt { get; set; }

    public static MembershipApplication FromDto(MembershipApplicationDTO dto, Guid? dueSelectionId, DateTime nowUtc) => new() {
        FirstName = dto.FirstName,
        LastName = dto.LastName,
        DateOfBirth = dto.DateOfBirth.ToStorage(),
        Citizenship = dto.Citizenship,
        Email = dto.Email,
        PhoneNumber = dto.PhoneNumber,
        AddressStreet = dto.AddressStreet,
        AddressHouseNbr = dto.AddressHouseNbr,
        AddressPostCode = dto.AddressPostCode,
        AddressCity = dto.AddressCity,
        AddressAdministrativeDivisionId = dto.AddressAdministrativeDivisionId,
        ChapterId = dto.ChapterId,
        DueSelectionId = dueSelectionId,
        ConformityDeclarationAccepted = dto.ConformityDeclarationAccepted,
        HasPriorDeclinedApplication = dto.HasPriorDeclinedApplication,
        IsMemberOfAnotherParty = dto.IsMemberOfAnotherParty,
        ApplicationText = dto.ApplicationText,
        EntryDate = dto.EntryDate.ToStorage(),
        SubmittedAt = nowUtc,
        Status = dto.ChapterId.HasValue ? ApplicationStatus.Pending : ApplicationStatus.PendingDivisionLinking
    };

    public MembershipApplicationDetailDTO ToDetailDto(string chapterName, bool hasReducedDueSelection) => new() {
        Id = Id,
        FirstName = FirstName,
        LastName = LastName,
        DateOfBirth = DateOfBirth.ToDtoDate(),
        Citizenship = Citizenship,
        Email = Email,
        PhoneNumber = PhoneNumber,
        AddressStreet = AddressStreet,
        AddressHouseNbr = AddressHouseNbr,
        AddressPostCode = AddressPostCode,
        AddressCity = AddressCity,
        ChapterId = ChapterId,
        ChapterName = chapterName,
        ConformityDeclarationAccepted = ConformityDeclarationAccepted,
        HasPriorDeclinedApplication = HasPriorDeclinedApplication,
        IsMemberOfAnotherParty = IsMemberOfAnotherParty,
        ApplicationText = ApplicationText,
        EntryDate = EntryDate.ToDtoDate(),
        SubmittedAt = SubmittedAt.ToDtoUtc(),
        Status = Status,
        ProcessedAt = ProcessedAt.ToDtoUtc(),
        MemberNumber = MemberNumber,
        WelcomeSentAt = WelcomeSentAt.ToDtoUtc(),
        HasReducedDueSelection = hasReducedDueSelection
    };

    /// <summary>Synthesises a <see cref="MemberDetailDTO"/> from this application for the welcome-mail flow, before the real Member entity exists.</summary>
    public MemberDetailDTO ToPendingMemberDto(int memberNumber, string chapterName) => new() {
        MemberNumber = memberNumber,
        FirstName = FirstName,
        LastName = LastName,
        Email = Email,
        Phone = PhoneNumber,
        Street = $"{AddressStreet} {AddressHouseNbr}".Trim(),
        PostCode = AddressPostCode,
        City = AddressCity,
        DateOfBirth = DateOfBirth.ToDtoDate(),
        Citizenship = Citizenship,
        ChapterId = ChapterId,
        ChapterName = chapterName
    };
}
