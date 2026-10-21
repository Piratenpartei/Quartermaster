using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>Forms that came back from an office; a shipment can come back in several parts.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureReturn {
    public const string TableName = "SignatureReturns";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShipmentId { get; set; }
    public DateTime ReturnedOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
