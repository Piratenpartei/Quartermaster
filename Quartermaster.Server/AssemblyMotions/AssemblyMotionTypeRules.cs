using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionTypeRules {
    public static bool CompetingDeadlineIsValid(AssemblyMotionTypeFields fields, AssemblyMotionPortal portal) {
        if (fields.CompetingDeadline == null)
            return true;
        var baseline = fields.Deadline?.UtcDateTime ?? portal.ClosesAt;
        return fields.CompetingDeadline.Value.UtcDateTime > baseline;
    }
}
