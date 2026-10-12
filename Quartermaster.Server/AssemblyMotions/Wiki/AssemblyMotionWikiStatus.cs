using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Wiki;

public static class AssemblyMotionWikiStatus {
    public static int CheckIcon(AssemblyMotion motion, bool hasOpenInquiry) {
        if (motion.Status == AssemblyMotionStatus.Withdrawn || motion.Status == AssemblyMotionStatus.Lapsed)
            return 6;
        if (motion.Status == AssemblyMotionStatus.Admitted)
            return 2;
        if (motion.Status == AssemblyMotionStatus.FormallyInsufficient)
            return 5;

        return hasOpenInquiry ? 7 : 1;
    }

    public static int VoteResult(AssemblyMotion motion) {
        if (motion.Status == AssemblyMotionStatus.Withdrawn || motion.Status == AssemblyMotionStatus.Lapsed)
            return 4;
        if (motion.Status == AssemblyMotionStatus.FormallyInsufficient)
            return 8;

        return motion.Result switch {
            AssemblyMotionResult.Accepted => 2,
            AssemblyMotionResult.Rejected => 3,
            AssemblyMotionResult.AcceptedModified => 5,
            _ => 1
        };
    }
}
