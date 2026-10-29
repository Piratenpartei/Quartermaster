using System;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;

namespace Quartermaster.Server.Chapters;

/// <summary>Moves undecided application motions up to the chapter whose take-over deadline has passed.</summary>
public class TakeOverSweep {
    private readonly DbContext _context;
    private readonly MotionRepository _motions;
    private readonly MembershipApplicationRepository _applications;
    private readonly TakeOverPlanner _planner;
    private readonly MotionHandover _handover;
    private readonly ILogger<TakeOverSweep> _logger;

    public TakeOverSweep(DbContext context, MotionRepository motions, MembershipApplicationRepository applications, TakeOverPlanner planner,
        MotionHandover handover, ILogger<TakeOverSweep> logger) {
        _context = context;
        _motions = motions;
        _applications = applications;
        _planner = planner;
        _handover = handover;
        _logger = logger;
    }

    /// <summary>Returns the number of motions moved; a motion that fails is logged and left for the next run.</summary>
    public int Run(DateTime nowUtc) {
        var moved = 0;
        foreach (var motion in _motions.ListPendingLinkedToApplications()) {
            try {
                if (MoveIfDue(motion, nowUtc))
                    moved++;
            } catch (Exception ex) {
                _handover.DiscardNotifications();
                _logger.LogError(ex, "Take-over of motion {MotionId} failed", motion.Id);
            }
        }
        return moved;
    }

    private bool MoveIfDue(Motion motion, DateTime nowUtc) {
        var application = _applications.Get(motion.LinkedMembershipApplicationId!.Value);
        if (application == null)
            return false;

        var step = _planner.DueFor(motion, application, nowUtc);
        if (step == null)
            return false;

        bool handedOver;
        using (var tx = _context.BeginTransaction()) {
            handedOver = _handover.HandOverMotion(motion, step.ChapterId, MotionHandoverReason.Deadline, null);
            tx.Commit();
        }

        try {
            _handover.SendNotifications();
        } catch (Exception ex) {
            _logger.LogError(ex, "Notification for the take-over of motion {MotionId} failed after it was moved", motion.Id);
        }
        return handedOver;
    }
}
