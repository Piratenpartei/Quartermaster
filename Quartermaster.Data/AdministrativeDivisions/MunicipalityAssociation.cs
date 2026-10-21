using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <summary>A Gemeindeverband from the Destatis GV100AD file, e.g. a Samtgemeinde.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class MunicipalityAssociation {
    public const string TableName = "MunicipalityAssociations";
    public const int KeyLength = 9;
    public const int NameMaxLength = 200;

    /// <summary>Land, Regierungsbezirk, Kreis and Gemeindeverband digits.</summary>
    [PrimaryKey]
    public string Key { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>The GV100AD Textkennzeichen of the record.</summary>
    public int Kind { get; set; }
    public DateTime ImportedAt { get; set; }
}
