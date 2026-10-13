using System;
using System.Collections.Generic;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Data.AssemblyMotions;

public static class AssemblyMotionDefaultTypes {
    public static List<AssemblyMotionType> Create(Guid portalId, Func<string, string> templateFor) {
        return [
            new AssemblyMotionType { PortalId = portalId, Code = "GP", Name = "Grundsatzprogramm", SortOrder = 10, MinSubmitters = 5, RequiredMajority = RequiredMajority.TwoThirds, WikiTemplate = templateFor("GP") },
            new AssemblyMotionType { PortalId = portalId, Code = "WP", Name = "Wahlprogramm", SortOrder = 20, MinSubmitters = 5, RequiredMajority = RequiredMajority.TwoThirds, WikiTemplate = templateFor("WP") },
            new AssemblyMotionType { PortalId = portalId, Code = "SÄA", Name = "Satzungsänderungsantrag", SortOrder = 30, MinSubmitters = 5, RequiredMajority = RequiredMajority.TwoThirds, WikiTemplate = templateFor("SÄA") },
            new AssemblyMotionType { PortalId = portalId, Code = "SBA", Name = "Satzungsbeiordnung", SortOrder = 40, MinSubmitters = 5, RequiredMajority = RequiredMajority.Simple, WikiTemplate = templateFor("SBA") },
            new AssemblyMotionType { PortalId = portalId, Code = "PP", Name = "Positionspapier", SortOrder = 50, MinSubmitters = 1, RequiredMajority = RequiredMajority.Simple, WikiTemplate = templateFor("PP") },
            new AssemblyMotionType { PortalId = portalId, Code = "SO", Name = "Sonstiger Antrag", SortOrder = 60, MinSubmitters = 1, RequiredMajority = RequiredMajority.Simple, WikiTemplate = templateFor("SO") }
        ];
    }
}
