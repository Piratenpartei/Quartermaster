using System.Collections.Generic;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <param name="MainName">The GeoNames main name, which may be English or Sorbian rather than German.</param>
/// <param name="IsDistrict">A city district, whose main name carries "Stadtbezirk" or "Bezirk" to tell it apart from places.</param>
public record AdministrativeDivisionOrigin(int GeoNameId, string MainName, bool IsDistrict);

/// <param name="OriginsByAdminCode">The GeoNames entry each division was read from; the country and the world have none.</param>
public record AdministrativeDivisionParseResult(List<AdministrativeDivision> Divisions, IReadOnlyDictionary<string, AdministrativeDivisionOrigin> OriginsByAdminCode);
