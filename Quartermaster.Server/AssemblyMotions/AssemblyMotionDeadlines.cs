using System;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionDeadlines {
    public static DateTime Effective(AssemblyMotionPortal portal, AssemblyMotionType type, bool isCompetingMotion) {
        DateTime candidate;
        if (isCompetingMotion && type.CompetingDeadline != null) {
            candidate = type.CompetingDeadline.Value;
        } else {
            candidate = type.Deadline ?? portal.ClosesAt;
        }

        return candidate < portal.ClosesAt ? candidate : portal.ClosesAt;
    }

    public static bool IsPast(DateTime effectiveDeadlineUtc, DateTime nowUtc) {
        return nowUtc > effectiveDeadlineUtc;
    }
}
