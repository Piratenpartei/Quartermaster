namespace Quartermaster.Data.AdministrativeDivisions;

/// <param name="Language">The GeoNames language code; empty where the file gives none.</param>
public record GeoNamesAlternateName(int GeoNameId, string Language, string Name, bool IsPreferred, bool IsShort, bool IsColloquial, bool IsHistoric);
