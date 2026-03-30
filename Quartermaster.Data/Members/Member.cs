using System;
using LinqToDB.Mapping;

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
    public string? EMail { get; set; }
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
}
