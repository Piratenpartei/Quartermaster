using System.Collections.Generic;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>One administration of the official address directory with the official keys (AGS) of the municipalities it serves.</summary>
public record OfficialOfficeEntry(string Key, string Name, string Street, string PostCode, string City, string? Email, IReadOnlyList<string> MunicipalityKeys);

public record OfficialOfficeSyncResult(int Created, int Updated, int Removed, int KeptForShipments, int Assigned, int UnknownMunicipalities);
