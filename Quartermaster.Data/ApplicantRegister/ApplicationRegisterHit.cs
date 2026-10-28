using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.ApplicantRegister;

namespace Quartermaster.Data.ApplicantRegister;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ApplicationRegisterHit {
    public const string TableName = "ApplicationRegisterHits";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ApplicationId { get; set; }
    public Guid EntryId { get; set; }
    public RegisterMatchFields MatchedFields { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DismissedAt { get; set; }
    public Guid? DismissedByUserId { get; set; }

    public ApplicationRegisterHitDTO ToDto(ApplicantRegisterEntry entry) => new() {
        Id = Id,
        EntryId = EntryId,
        Kind = entry.Kind,
        OccurredOn = entry.OccurredOn.ToDtoDate(),
        FirstName = entry.FirstName,
        LastName = entry.LastName,
        MatchedFields = MatchedFields,
        DismissedAt = DismissedAt.ToDtoUtc()
    };
}
