using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.AdministrativeDivisions;

public enum AdministrativeDivisionNameSource {
    GeoNames = 0,
    GeoNamesAlternate = 1,
    Gv100Ad = 2
}

/// <summary>One of the names a division goes by; the search matches all of them, <see cref="AdministrativeDivision.Name"/> is the one shown.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class AdministrativeDivisionName {
    public const string TableName = "AdministrativeDivisionNames";
    public const int NameMaxLength = 200;
    public const int LanguageMaxLength = 16;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AdministrativeDivisionId { get; set; }

    public string Name { get; set; } = "";

    /// <summary>The GeoNames language code; null where the source gives none.</summary>
    public string? Language { get; set; }
    public AdministrativeDivisionNameSource Source { get; set; }
    public bool IsPreferred { get; set; }
    public bool IsHistoric { get; set; }
}
