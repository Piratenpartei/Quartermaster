using System;
using LinqToDB.Mapping;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Data.ApplicantRegister;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ApplicantRegisterEntry {
    public const string TableName = "ApplicantRegisterEntries";
    public const int NameMaxLength = 256;
    public const int StreetMaxLength = 256;
    public const int HouseNumberMaxLength = 32;
    public const int PostCodeMaxLength = 16;
    public const int CityMaxLength = 256;
    public const int EmailMaxLength = 256;
    public const int NoteMaxLength = 2000;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public ApplicantRegisterKind Kind { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string? Email { get; set; }
    public DateTime OccurredOn { get; set; }
    public string? Note { get; set; }
    public Guid? ChapterId { get; set; }
    public Guid? SourceApplicationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }

    /// <summary>The automatic entry for an application rejected in Quartermaster by <paramref name="chapterId"/>.</summary>
    public static ApplicantRegisterEntry FromRejectedApplication(MembershipApplication application, Guid chapterId, DateTime occurredOn, DateTime nowUtc) => new() {
        Kind = ApplicantRegisterKind.Rejected,
        FirstName = application.FirstName,
        LastName = application.LastName,
        DateOfBirth = application.DateOfBirth,
        Street = application.AddressStreet,
        HouseNumber = application.AddressHouseNbr,
        PostCode = application.AddressPostCode,
        City = application.AddressCity,
        Email = application.Email,
        OccurredOn = occurredOn,
        ChapterId = chapterId,
        SourceApplicationId = application.Id,
        CreatedAt = nowUtc
    };
}
