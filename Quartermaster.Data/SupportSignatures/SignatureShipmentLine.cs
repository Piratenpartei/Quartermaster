using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.SupportSignatures;

[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureShipmentLine {
    public const string TableName = "SignatureShipmentLines";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ShipmentId { get; set; }
    public Guid AdministrativeDivisionId { get; set; }
    public int Count { get; set; }
}
