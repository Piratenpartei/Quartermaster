using System;
using System.Collections.Generic;

namespace Quartermaster.Server.SupportSignatures;

public record ShipmentPlan(IReadOnlyList<PlannedShipment> Offices, IReadOnlyList<PlannedLine> Unassigned);

public record PlannedShipment(Guid OfficeId, IReadOnlyList<PlannedLine> Lines);

public record PlannedLine(Guid MunicipalityId, int Count);
