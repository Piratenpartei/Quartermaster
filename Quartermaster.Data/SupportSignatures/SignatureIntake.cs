using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>Forms of one municipality that came in on one day; only the count, never the signers.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureIntake {
    public const string TableName = "SignatureIntakes";
    public const int NoteMaxLength = 500;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampaignId { get; set; }
    public Guid AdministrativeDivisionId { get; set; }
    public int Count { get; set; }
    public bool AlreadyCertified { get; set; }
    public DateTime ReceivedOn { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

    public SignatureIntakeDTO ToDto(string municipalityName) {
        return new() {
            Id = Id,
            AdministrativeDivisionId = AdministrativeDivisionId,
            MunicipalityName = municipalityName,
            Count = Count,
            AlreadyCertified = AlreadyCertified,
            ReceivedOn = DateOnly.FromDateTime(ReceivedOn),
            Note = Note,
            CreatedAt = CreatedAt.ToDtoUtc()
        };
    }
}
