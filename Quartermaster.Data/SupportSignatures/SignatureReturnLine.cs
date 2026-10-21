using System;
using LinqToDB.Mapping;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Data.SupportSignatures;

[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureReturnLine {
    public const string TableName = "SignatureReturnLines";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ReturnId { get; set; }
    public Guid AdministrativeDivisionId { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public SignatureRejectionReason? RejectionReason { get; set; }
}
