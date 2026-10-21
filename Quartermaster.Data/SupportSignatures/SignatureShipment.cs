using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>Forms sent to one office; prepared while <see cref="SentOn"/> is null.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureShipment {
    public const string TableName = "SignatureShipments";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CampaignId { get; set; }
    public Guid OfficeId { get; set; }
    public DateTime? SentOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
