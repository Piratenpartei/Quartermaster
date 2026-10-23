using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>The office the official address directory names for a municipality; a <see cref="CertificationOfficeMunicipality"/> overrides it.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class OfficialOfficeMunicipality {
    public const string TableName = "OfficialOfficeMunicipalities";

    [PrimaryKey]
    public Guid AdministrativeDivisionId { get; set; }

    public Guid OfficeId { get; set; }
}
