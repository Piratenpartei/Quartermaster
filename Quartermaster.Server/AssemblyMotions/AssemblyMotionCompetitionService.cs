using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions.Notifications;

namespace Quartermaster.Server.AssemblyMotions;

public class AssemblyMotionCompetitionService {
    private const string EntityType = "AssemblyMotion";

    private readonly AssemblyMotionRepository _motions;
    private readonly AssemblyMotionCompetitionRepository _competitions;
    private readonly AssemblyMotionPortalRepository _portals;
    private readonly AssemblyMotionService _motionService;
    private readonly AuditLogRepository _auditLog;
    private readonly AssemblyMotionNotifier _notifier;

    public AssemblyMotionCompetitionService(AssemblyMotionRepository motions, AssemblyMotionCompetitionRepository competitions,
        AssemblyMotionPortalRepository portals, AssemblyMotionService motionService, AuditLogRepository auditLog,
        AssemblyMotionNotifier notifier) {
        _motions = motions;
        _competitions = competitions;
        _portals = portals;
        _motionService = motionService;
        _auditLog = auditLog;
        _notifier = notifier;
    }

    public static (Guid A, Guid B) OrderPair(Guid first, Guid second) {
        return first.CompareTo(second) <= 0 ? (first, second) : (second, first);
    }

    /// <summary>
    /// A link qualifies for the competing flag when the other motion is numbered and was submitted earlier than
    /// <paramref name="motion"/>, or when <paramref name="motion"/> has not been submitted yet.
    /// </summary>
    public static bool IsQualifying(AssemblyMotion motion, AssemblyMotion other) {
        if (other.Number == null || !IsActive(other.Status))
            return false;
        if (motion.SubmittedAt == null)
            return true;
        return other.SubmittedAt != null && other.SubmittedAt < motion.SubmittedAt;
    }

    public AssemblyMotionOutcome Declare(Guid motionId, Guid otherMotionId, Guid userId, Member? member, bool asCommission, DateTime nowUtc) {
        if (motionId == otherMotionId)
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        var notifications = new List<Action>();
        using var tx = _motions.BeginTransaction();

        var locked = LockPair(motionId, otherMotionId);
        var motion = locked.GetValueOrDefault(motionId);
        var other = locked.GetValueOrDefault(otherMotionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var permission = DeclarePermissionProblem(motion, member, asCommission);
        if (permission != null)
            return permission;

        if (other == null || other.PortalId != motion.PortalId || !AssemblyMotionVisibility.IsPublic(other))
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);
        if (!CanDeclareFrom(motion.Status) || !IsDeclarable(other.Status))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.InvalidState);

        var pair = OrderPair(motionId, otherMotionId);
        if (_competitions.Exists(pair.A, pair.B))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.CompetitionExists);

        _competitions.Insert(new AssemblyMotionCompetition {
            MotionAId = pair.A,
            MotionBId = pair.B,
            DeclaringMotionId = motionId,
            DeclaredByUserId = userId,
            DeclaredAt = nowUtc,
            DeclaredByCommission = asCommission && (member == null || !IsSubmitter(motion.Id, member.Id))
        });

        MarkChanged(motion, nowUtc, "CompetitionDeclared", null, otherMotionId.ToString());
        MarkChanged(other, nowUtc, "CompetitionDeclared", null, motionId.ToString());

        if (motion.Status != AssemblyMotionStatus.Draft) {
            var otherSubmitterIds = SubmitterIds(other.Id);
            notifications.Add(() => _notifier.CompetitionDeclared(motion, other, otherSubmitterIds));
        }

        tx.Commit();
        foreach (var notification in notifications)
            notification();
        return AssemblyMotionOutcome.Ok(motion);
    }

    public AssemblyMotionOutcome Remove(Guid competitionId, Guid userId, Member? member, bool asCommission, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var link = _competitions.Get(competitionId);
        if (link == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.CompetitionNotFound);

        var locked = LockPair(link.MotionAId, link.MotionBId);

        link = _competitions.Get(competitionId);
        if (link == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.CompetitionNotFound);

        var declaring = locked.GetValueOrDefault(link.DeclaringMotionId);
        if (declaring == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var permission = RemovePermissionProblem(link, member, asCommission);
        if (permission != null)
            return permission;

        _competitions.Delete(link.Id);

        foreach (var motion in locked.Values) {
            var otherId = motion.Id == link.MotionAId ? link.MotionBId : link.MotionAId;
            MarkChanged(motion, nowUtc, "CompetitionRemoved", otherId.ToString(), null);
            ClearFlagWithoutQualifyingLink(motion);
        }

        tx.Commit();
        return AssemblyMotionOutcome.Ok(declaring);
    }

    public AssemblyMotionOutcome SetCompetingFlag(Guid motionId, bool value, Member? member, bool asCommission, DateTime nowUtc) {
        using var tx = _motions.BeginTransaction();

        var motion = _motions.GetForUpdate(motionId);
        if (motion == null)
            return AssemblyMotionOutcome.Fail(404, I18nKey.Error.AssemblyMotion.NotFound);

        var commissionClears = asCommission && !value;
        var isAuthor = member != null && motion.CreatedByMemberId == member.Id;
        if (!commissionClears && !isAuthor)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
        if (motion.IsCompetingMotion == value)
            return AssemblyMotionOutcome.Ok(motion);

        if (!commissionClears) {
            var problem = AuthorFlagProblem(motion, member!, value, nowUtc);
            if (problem != null)
                return problem;
        }

        var before = motion.IsCompetingMotion;
        motion.IsCompetingMotion = value;
        _motions.SetCompetingFlag(motion.Id, value);
        _auditLog.Log(EntityType, motion.Id, "CompetingFlagChanged", nameof(AssemblyMotion.IsCompetingMotion), before.ToString(), value.ToString());

        tx.Commit();
        return AssemblyMotionOutcome.Ok(motion);
    }

    private AssemblyMotionOutcome? AuthorFlagProblem(AssemblyMotion motion, Member author, bool value, DateTime nowUtc) {
        var portal = _portals.Get(motion.PortalId)
            ?? throw new InvalidOperationException($"Portal {motion.PortalId} not found for motion {motion.Id}.");
        var type = _portals.GetType(motion.TypeId)
            ?? throw new InvalidOperationException($"Type {motion.TypeId} not found for motion {motion.Id}.");

        if (!_motionService.CanEditAsAuthor(motion, type, portal, author.Id, nowUtc, value))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.EditLocked);
        if (value && !HasQualifyingLink(motion))
            return AssemblyMotionOutcome.Fail(409, I18nKey.Error.AssemblyMotion.CompetingFlagRequiresLink);

        return null;
    }

    private AssemblyMotionOutcome? DeclarePermissionProblem(AssemblyMotion declaring, Member? member, bool asCommission) {
        if (declaring.Status == AssemblyMotionStatus.Draft) {
            if (member == null || declaring.CreatedByMemberId != member.Id)
                return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotAuthor);
            return null;
        }

        if (asCommission)
            return null;
        if (member == null || !IsSubmitter(declaring.Id, member.Id))
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotSubmitter);

        return null;
    }

    private AssemblyMotionOutcome? RemovePermissionProblem(AssemblyMotionCompetition link, Member? member, bool asCommission) {
        if (asCommission)
            return null;
        if (link.DeclaredByCommission)
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotCommission);
        if (member == null || !IsSubmitter(link.DeclaringMotionId, member.Id))
            return AssemblyMotionOutcome.Fail(403, I18nKey.Error.AssemblyMotion.NotSubmitter);

        return null;
    }

    /// <summary>
    /// Whether <paramref name="viewer"/> may remove <paramref name="link"/>, as the same rule <see cref="Remove"/> applies.
    /// </summary>
    public bool CanRemove(AssemblyMotionCompetition link, Member? viewer, bool isCommission) {
        return RemovePermissionProblem(link, viewer, isCommission) == null;
    }

    private Dictionary<Guid, AssemblyMotion> LockPair(Guid first, Guid second) {
        var pair = OrderPair(first, second);
        var result = new Dictionary<Guid, AssemblyMotion>();

        var a = _motions.GetForUpdate(pair.A);
        if (a != null)
            result[a.Id] = a;
        var b = _motions.GetForUpdate(pair.B);
        if (b != null)
            result[b.Id] = b;

        return result;
    }

    private void MarkChanged(AssemblyMotion motion, DateTime nowUtc, string action, string? oldValue, string? newValue) {
        if (motion.WasSubmitted)
            _motions.MarkWikiPending(motion.Id, nowUtc);

        _auditLog.Log(EntityType, motion.Id, action, "OtherMotionId", oldValue, newValue);
    }

    private void ClearFlagWithoutQualifyingLink(AssemblyMotion motion) {
        if (!motion.IsCompetingMotion || HasQualifyingLink(motion))
            return;

        motion.IsCompetingMotion = false;
        _motions.SetCompetingFlag(motion.Id, false);
        _auditLog.Log(EntityType, motion.Id, "CompetingFlagChanged", nameof(AssemblyMotion.IsCompetingMotion), true.ToString(), false.ToString());
    }

    private bool HasQualifyingLink(AssemblyMotion motion) {
        return _competitions.ListForMotion(motion.Id)
            .Select(l => l.MotionAId == motion.Id ? l.MotionBId : l.MotionAId)
            .Select(id => _motions.Get(id))
            .Any(other => other != null && IsQualifying(motion, other));
    }

    private bool IsSubmitter(Guid motionId, Guid memberId) {
        return _motions.GetSubmitters(motionId).Any(s => s.MemberId == memberId);
    }

    private List<Guid> SubmitterIds(Guid motionId) {
        return _motions.GetSubmitters(motionId)
                .Select(s => s.MemberId)
                .ToList();
    }

    private static bool IsActive(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Submitted)
            return true;
        if (status == AssemblyMotionStatus.Admitted)
            return true;
        return status == AssemblyMotionStatus.FormallyInsufficient;
    }

    private static bool CanDeclareFrom(AssemblyMotionStatus status) {
        if (status == AssemblyMotionStatus.Draft)
            return true;
        if (status == AssemblyMotionStatus.CollectingCoSubmitters)
            return true;
        return IsDeclarable(status);
    }

    private static bool IsDeclarable(AssemblyMotionStatus status) {
        return status == AssemblyMotionStatus.Submitted || status == AssemblyMotionStatus.Admitted;
    }
}
