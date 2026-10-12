using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions;

public static class AssemblyMotionVisibility {
    public static bool CanSee(AssemblyMotion motion, AssemblyMotionViewer viewer) {
        if (motion.Status == AssemblyMotionStatus.Draft)
            return viewer.MemberId != null && viewer.MemberId == motion.CreatedByMemberId;
        return true;
    }

    public static bool IsPublic(AssemblyMotion motion)
        => motion.Status != AssemblyMotionStatus.Draft;

    public static bool CanSeeNames(AssemblyMotionViewer viewer, bool viewerIsSubmitter)
        => viewer.IsEligible || viewer.IsCommission || viewerIsSubmitter;
}
