using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.Members;

namespace Quartermaster.Data.Members;

[Table(TableName, IsColumnAttributeRequired = false)]
public class Member {
    public const string TableName = "Members";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MemberNumber { get; set; }
    public string? AdmissionReference { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Street { get; set; }
    public string? Country { get; set; }
    public string? PostCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Citizenship { get; set; }
    public decimal MembershipFee { get; set; }
    public decimal ReducedFee { get; set; }
    public decimal? FirstFee { get; set; }
    public decimal? OpenFeeTotal { get; set; }
    public DateTime? ReducedFeeEnd { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? ExitDate { get; set; }
    public string? FederalState { get; set; }
    public string? County { get; set; }
    public string? Municipality { get; set; }
    public bool IsPending { get; set; }
    public bool HasVotingRights { get; set; }
    public bool ReceivesSurveys { get; set; }
    public bool ReceivesActions { get; set; }
    public bool ReceivesNewsletter { get; set; }
    public bool PostBounce { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? ResidenceAdministrativeDivisionId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime LastImportedAt { get; set; }
    public DateTime? AnonymizedAt { get; set; }

    public MemberDetailDTO ToDetailDto(string chapterName = "") => new() {
        Id = Id,
        MemberNumber = MemberNumber,
        AdmissionReference = AdmissionReference,
        FirstName = FirstName,
        LastName = LastName,
        Street = Street,
        Country = Country,
        PostCode = PostCode,
        City = City,
        Phone = Phone,
        Email = Email,
        DateOfBirth = DateOfBirth.ToDtoDate(),
        Citizenship = Citizenship,
        MembershipFee = MembershipFee,
        ReducedFee = ReducedFee,
        FirstFee = FirstFee,
        OpenFeeTotal = OpenFeeTotal,
        ReducedFeeEnd = ReducedFeeEnd.ToDtoDate(),
        EntryDate = EntryDate.ToDtoDate(),
        ExitDate = ExitDate.ToDtoDate(),
        FederalState = FederalState,
        County = County,
        Municipality = Municipality,
        IsPending = IsPending,
        HasVotingRights = HasVotingRights,
        ReceivesSurveys = ReceivesSurveys,
        ReceivesActions = ReceivesActions,
        ReceivesNewsletter = ReceivesNewsletter,
        PostBounce = PostBounce,
        ChapterId = ChapterId,
        ChapterName = chapterName,
        ResidenceAdministrativeDivisionId = ResidenceAdministrativeDivisionId,
        UserId = UserId,
        LastImportedAt = new DateTimeOffset(LastImportedAt, TimeSpan.Zero)
    };
}
