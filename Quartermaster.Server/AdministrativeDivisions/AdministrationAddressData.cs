using System.Collections.Generic;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.AdministrativeDivisions;

/// <param name="TwoAdministrations">Municipalities the directory lists with their association and their own administration.</param>
public record AdministrationAddressData(IReadOnlyList<OfficialOfficeEntry> Offices, int Municipalities, int WithoutAddress, int TwoAdministrations);
