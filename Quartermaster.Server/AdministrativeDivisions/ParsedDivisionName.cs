using Quartermaster.Data.AdministrativeDivisions;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <summary>A name read from the files, keyed by the admin code because the division ids are only known after the import.</summary>
public record ParsedDivisionName(string AdminCode, string Name, string? Language, AdministrativeDivisionNameSource Source, bool IsPreferred, bool IsHistoric);
