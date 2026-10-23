using System.Collections.Generic;

namespace Quartermaster.Data.AdministrativeDivisions;

/// <param name="Key">Land, Regierungsbezirk, Kreis and Gemeindeverband digits, e.g. 033515403.</param>
/// <param name="Kind">The GV100AD Textkennzeichen, e.g. 52 for a Samtgemeinde.</param>
public record Gv100AdAssociation(string Key, string Name, int Kind);

/// <param name="Ags">The 8-digit Amtlicher Gemeindeschlüssel.</param>
public record Gv100AdMunicipality(string Ags, string AssociationKey, string Name);

/// <param name="Key">Land and Regierungsbezirk digits, e.g. 094.</param>
public record Gv100AdRegion(string Key, string Name);

public record Gv100AdData(IReadOnlyList<Gv100AdAssociation> Associations, IReadOnlyList<Gv100AdMunicipality> Municipalities, IReadOnlyList<Gv100AdRegion> Regions);
