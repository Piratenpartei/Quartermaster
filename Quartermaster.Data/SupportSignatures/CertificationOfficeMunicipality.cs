using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.SupportSignatures;

[Table(TableName, IsColumnAttributeRequired = false)]
public class CertificationOfficeMunicipality {
    public const string TableName = "CertificationOfficeMunicipalities";

    [PrimaryKey]
    public Guid AdministrativeDivisionId { get; set; }

    public Guid OfficeId { get; set; }
}
