using System;
using System.Collections.Generic;
using System.Linq;

namespace Quartermaster.Server.SupportSignatures;

public static class ShipmentPlanner {
    /// <summary>One planned shipment per office holding all plannable forms of its municipalities; municipalities without office come back unassigned.</summary>
    public static ShipmentPlan Plan(IReadOnlyDictionary<Guid, MunicipalityTally> tallies, IReadOnlyDictionary<Guid, Guid> officeByMunicipality) {
        var plannable = tallies
            .Where(t => t.Value.Plannable > 0)
            .Select(t => new PlannedLine(t.Key, t.Value.Plannable))
            .ToList();

        var offices = plannable
            .Where(l => officeByMunicipality.ContainsKey(l.MunicipalityId))
            .GroupBy(l => officeByMunicipality[l.MunicipalityId])
            .Select(g => new PlannedShipment(g.Key, g.ToList()))
            .ToList();
        var unassigned = plannable
            .Where(l => !officeByMunicipality.ContainsKey(l.MunicipalityId))
            .ToList();

        return new ShipmentPlan(offices, unassigned);
    }
}
