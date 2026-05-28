using System;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.DueSelector;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Motions;

/// <summary>
/// After a motion is resolved, sends the approved/rejected decision mail to the submitter of
/// whatever the motion is linked to (membership application and/or due selection). The mail
/// services themselves only send on a terminal Approved/Rejected status, so calling this for a
/// motion that closed without a decision is a no-op.
/// </summary>
public class MotionResolutionDecisionMailer {
    private readonly MotionRepository _motionRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly DueSelectionRepository _dueSelectionRepo;
    private readonly MembershipApplicationMailService _applicationMail;
    private readonly DueSelectionMailService _dueSelectionMail;

    public MotionResolutionDecisionMailer(
        MotionRepository motionRepo,
        MembershipApplicationRepository applicationRepo,
        DueSelectionRepository dueSelectionRepo,
        MembershipApplicationMailService applicationMail,
        DueSelectionMailService dueSelectionMail) {
        _motionRepo = motionRepo;
        _applicationRepo = applicationRepo;
        _dueSelectionRepo = dueSelectionRepo;
        _applicationMail = applicationMail;
        _dueSelectionMail = dueSelectionMail;
    }

    public async Task NotifyAsync(Guid motionId, CancellationToken ct) {
        var motion = _motionRepo.Get(motionId);
        if (motion == null) {
            return;
        }

        if (motion.LinkedMembershipApplicationId.HasValue) {
            var application = _applicationRepo.Get(motion.LinkedMembershipApplicationId.Value);
            if (application != null) {
                await _applicationMail.SendApplicationDecisionAsync(application, ct);
            }
        }

        if (motion.LinkedDueSelectionId.HasValue) {
            var selection = _dueSelectionRepo.Get(motion.LinkedDueSelectionId.Value);
            if (selection != null) {
                await _dueSelectionMail.SendDueSelectionDecisionAsync(selection, ct);
            }
        }
    }
}
