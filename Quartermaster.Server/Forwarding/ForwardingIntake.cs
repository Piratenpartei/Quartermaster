using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Notifications;
using Quartermaster.Data.Options;
using Quartermaster.Server.MembershipApplications;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.Forwarding;

public record DueSelectionIntakeResult(Guid? DueSelectionId, Guid? CaseId);

public record ApplicationIntakeResult(Guid? ApplicationId, Guid? CaseId, bool IsReduced);

/// <summary>
/// Runs the routing plan of a submission inside the request: stores the Quartermaster records and motions it calls for and queues the ticket steps,
/// all in one transaction. Never talks to Redmine; the job does.
/// </summary>
public class ForwardingIntake {
    public const string ApplicationSourceType = "MembershipApplication";

    private readonly ForwardingSettingsResolver _resolver;
    private readonly ForwardingCaseRepository _cases;
    private readonly DueSelectionRepository _dueSelections;
    private readonly MembershipApplicationRepository _applications;
    private readonly ApplicationReviewService _review;
    private readonly OptionRepository _options;
    private readonly INotificationDispatchQueue _notifications;
    private readonly NotificationLogRepository _notificationLogs;
    private readonly ChapterRepository _chapters;

    public ForwardingIntake(ForwardingSettingsResolver resolver, ForwardingCaseRepository cases, DueSelectionRepository dueSelections,
        MembershipApplicationRepository applications, ApplicationReviewService review, OptionRepository options,
        INotificationDispatchQueue notifications, NotificationLogRepository notificationLogs, ChapterRepository chapters) {
        _resolver = resolver;
        _cases = cases;
        _dueSelections = dueSelections;
        _applications = applications;
        _review = review;
        _options = options;
        _notifications = notifications;
        _notificationLogs = notificationLogs;
        _chapters = chapters;
    }

    public DueSelectionIntakeResult IntakeDueSelection(DueSelectionDTO dto, Guid? memberChapterId, Guid? userId, string? userName, bool enteredByHand, DateTime nowUtc) {
        using var tx = _cases.BeginTransaction();
        var result = RouteDueSelection(dto, memberChapterId, userId, userName, enteredByHand, nowUtc);
        tx.Commit();
        return result;
    }

    private DueSelectionIntakeResult RouteDueSelection(DueSelectionDTO dto, Guid? memberChapterId, Guid? userId, string? userName, bool enteredByHand, DateTime nowUtc) {
        var ctx = _resolver.Build(memberChapterId);
        var plan = ForwardingRouter.PlanDueSelection(ctx, DueAmount.Effective(dto), dto.SelectedValuation == SelectedValuation.Reduced,
            IsEnabled(ForwardingOptionKeys.DueSelectionEnabled), RegularMinimum(), StateDecisionMinimum(), false);

        Guid? dueSelectionId = null;
        Guid? motionId = null;
        if (plan.StoreRecord) {
            var record = DueSelection.FromDto(dto);
            record.UserId = userId;
            record.ChapterId = plan.RecordChapterId;
            record.Status = plan.RecordStatus ?? DueSelectionStatus.Pending;
            _dueSelections.Create(record);
            dueSelectionId = record.Id;
            if (plan.MotionChapterId != null)
                motionId = _review.CreateReductionMotion(record, plan.MotionChapterId.Value, null);
            // The board that votes is always told. A record without a vote and without a known member chapter lands at the federal level by default,
            // where nobody has to act on it.
            if (motionId != null || memberChapterId != null)
                NotifyProcessors(record);
        }

        if (plan.Tickets.Count == 0)
            return new DueSelectionIntakeResult(dueSelectionId, null);

        var payload = new ForwardingPayload(null, dto, userId, userName, enteredByHand, nowUtc);
        var forwardingCase = new ForwardingCase {
            Kind = ForwardingCaseKind.DueSelection,
            ApplicationChapterId = memberChapterId,
            DecidingChapterId = plan.DecidingChapterId,
            StateChapterId = ctx.StateId,
            DueSelectionId = dueSelectionId,
            MotionId = motionId,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = nowUtc
        };
        _cases.Create(forwardingCase, Steps(plan, ForwardingPurpose.Reduction, nowUtc));
        return new DueSelectionIntakeResult(dueSelectionId, forwardingCase.Id);
    }

    /// <param name="applicationChapterId">The chapter the server determined from the address; null when none could be found.</param>
    public ApplicationIntakeResult IntakeApplication(MembershipApplicationDTO dto, Guid? applicationChapterId, DateTime nowUtc) {
        using var tx = _cases.BeginTransaction();
        var result = RouteApplication(dto, applicationChapterId, nowUtc);
        tx.Commit();
        return result;
    }

    /// <summary>
    /// Continues an application that waited for its administrative division: routes it as if its chapter had been known on arrival. Its federal ticket is
    /// already queued or created; a reduction in it was held back until now. When the application is handled in Redmine from here on, Quartermaster's
    /// copy is deleted and the case keeps the data until the chapter ticket exists.
    /// </summary>
    public void LinkApplication(MembershipApplication application, Guid? divisionId, Guid chapterId, DateTime nowUtc) {
        using var tx = _cases.BeginTransaction();
        var applicationCase = _cases.ListByRecord(ForwardingCaseKind.Application, application.Id).FirstOrDefault();
        var ctx = _resolver.Build(chapterId);
        var plan = ForwardingRouter.PlanApplication(ctx, chapterId, applicationCase != null);
        var dueRecord = application.DueSelectionId == null ? null : _dueSelections.Get(application.DueSelectionId.Value);
        var dues = dueRecord?.ToDto();
        var isReduced = dues != null && DueAmount.NeedsDecision(dues, RegularMinimum());
        var reductionPlan = isReduced ? PlanReduction(ctx, dues!) : null;

        application.AddressAdministrativeDivisionId = divisionId;
        application.ChapterId = plan.StoreRecord ? plan.RecordChapterId : chapterId;
        application.Status = ApplicationStatus.Pending;
        var dto = application.ToDto(dues);

        Guid? motionId = null;
        if (plan.StoreRecord) {
            _applications.LinkDivisionAndChapter(application.Id, divisionId, plan.RecordChapterId);
            if (plan.MotionChapterId != null)
                motionId = _review.CreateReviewMotionAndNotify(application, isReduced);
        }

        // Without the application in Quartermaster a reduction gets its own record carrying the applicant's name, as on arrival.
        var keepDueRecord = dueRecord != null && plan.StoreRecord;
        if (keepDueRecord)
            SetDueStatusAfterLinking(dueRecord!, plan, reductionPlan, nowUtc);

        if (applicationCase != null)
            ContinueApplicationCase(applicationCase, plan, ctx, chapterId, plan.StoreRecord ? application : null, plan.StoreRecord ? dueRecord : null, motionId, dto, nowUtc);

        if (reductionPlan != null) {
            IntakeApplicationReduction(dto, plan.StoreRecord ? application : null, keepDueRecord ? dueRecord : null, reductionPlan, ctx, chapterId,
                applicationCase?.Id, nowUtc);
        }

        if (!plan.StoreRecord) {
            _applications.Delete(application.Id);
            _notificationLogs.ClearBodies(ApplicationSourceType, application.Id);
            if (dueRecord != null)
                _dueSelections.Delete(dueRecord.Id);
        }

        tx.Commit();
    }

    private void SetDueStatusAfterLinking(DueSelection dueRecord, ForwardingPlan plan, ForwardingPlan? reductionPlan, DateTime nowUtc) {
        if (reductionPlan == null) {
            dueRecord.ChapterId = plan.RecordChapterId;
            dueRecord.Status = DueSelectionStatus.AutoApproved;
        } else {
            dueRecord.ChapterId = reductionPlan.DecidingChapterId;
            dueRecord.Status = DueStatusInApplication(true, reductionPlan);
        }

        dueRecord.ProcessedAt = dueRecord.Status == DueSelectionStatus.Forwarded ? nowUtc : null;
        _dueSelections.SetDecidingChapter(dueRecord.Id, dueRecord.ChapterId, dueRecord.Status, dueRecord.ProcessedAt);
    }

    private void ContinueApplicationCase(ForwardingCase applicationCase, ForwardingPlan plan, ForwardingChapterContext ctx, Guid chapterId,
        MembershipApplication? application, DueSelection? dueRecord, Guid? motionId, MembershipApplicationDTO dto, DateTime nowUtc) {
        var nextOrder = _cases.GetSteps(applicationCase.Id).Max(s => s.Order) + 1;
        var added = Steps(plan, ForwardingPurpose.Application, nowUtc).Skip(1).ToList();
        foreach (var step in added)
            step.Order = nextOrder++;

        applicationCase.ApplicationChapterId = chapterId;
        applicationCase.DecidingChapterId = plan.DecidingChapterId;
        applicationCase.StateChapterId = ctx.StateId;
        applicationCase.MembershipApplicationId = application?.Id;
        applicationCase.DueSelectionId = dueRecord?.Id;
        applicationCase.MotionId = motionId;
        // The federal ticket may already exist and the data be cleared; a chapter ticket still to come needs it again.
        if (applicationCase.PayloadJson == null && added.Any(s => s.Kind == ForwardingStepKind.CreateTicket))
            applicationCase.PayloadJson = JsonSerializer.Serialize(new ForwardingPayload(dto, null, null, null, false, nowUtc));
        if (added.Count > 0 && applicationCase.Status == ForwardingCaseStatus.Done) {
            applicationCase.Status = ForwardingCaseStatus.Open;
            applicationCase.CompletedAt = null;
        }

        _cases.UpdateRouting(applicationCase);
        _cases.AddSteps(applicationCase.Id, added);
    }

    private ApplicationIntakeResult RouteApplication(MembershipApplicationDTO dto, Guid? applicationChapterId, DateTime nowUtc) {
        var ctx = _resolver.Build(applicationChapterId);
        var plan = ForwardingRouter.PlanApplication(ctx, applicationChapterId, IsEnabled(ForwardingOptionKeys.MembershipApplicationEnabled));
        var dues = dto.DueSelection;
        var isReduced = dues != null && DueAmount.NeedsDecision(dues, RegularMinimum());
        // Who decides on a reduction depends on the chapter, so an application waiting for its division holds its reduction back until it is linked.
        var waitsForDivision = applicationChapterId == null && plan.StoreRecord;
        var reductionPlan = isReduced && !waitsForDivision ? PlanReduction(ctx, dues!) : null;

        MembershipApplication? application = null;
        DueSelection? dueRecord = null;
        Guid? applicationMotionId = null;
        if (plan.StoreRecord) {
            if (dues != null) {
                dueRecord = DueSelection.FromDto(dues);
                if (waitsForDivision) {
                    dueRecord.Status = isReduced ? DueSelectionStatus.Pending : DueSelectionStatus.AutoApproved;
                } else {
                    dueRecord.ChapterId = isReduced ? reductionPlan!.DecidingChapterId : plan.RecordChapterId;
                    dueRecord.Status = DueStatusInApplication(isReduced, reductionPlan);
                }
                // The decision happens outside Quartermaster; starting the retention clock now keeps the record from staying forever.
                if (dueRecord.Status == DueSelectionStatus.Forwarded)
                    dueRecord.ProcessedAt = nowUtc;
                _dueSelections.Create(dueRecord);
            }

            application = MembershipApplication.FromDto(dto, dueRecord?.Id, nowUtc);
            application.ChapterId = plan.RecordChapterId;
            application.Status = plan.RecordChapterId.HasValue ? ApplicationStatus.Pending : ApplicationStatus.PendingDivisionLinking;
            _applications.Create(application);
            if (plan.MotionChapterId != null)
                applicationMotionId = _review.CreateReviewMotionAndNotify(application, isReduced);
        }

        Guid? applicationCaseId = null;
        if (plan.Tickets.Count > 0) {
            var applicationCase = new ForwardingCase {
                Kind = ForwardingCaseKind.Application,
                ApplicationChapterId = applicationChapterId,
                DecidingChapterId = plan.DecidingChapterId,
                StateChapterId = ctx.StateId,
                MembershipApplicationId = application?.Id,
                DueSelectionId = dueRecord?.Id,
                MotionId = applicationMotionId,
                PayloadJson = JsonSerializer.Serialize(new ForwardingPayload(dto, null, null, null, false, nowUtc)),
                CreatedAt = nowUtc
            };
            _cases.Create(applicationCase, Steps(plan, ForwardingPurpose.Application, nowUtc));
            applicationCaseId = applicationCase.Id;
        }

        if (reductionPlan != null)
            IntakeApplicationReduction(dto, application, dueRecord, reductionPlan, ctx, applicationChapterId, applicationCaseId, nowUtc);

        return new ApplicationIntakeResult(application?.Id, applicationCaseId, isReduced);
    }

    private void IntakeApplicationReduction(MembershipApplicationDTO dto, MembershipApplication? application, DueSelection? dueRecord,
        ForwardingPlan plan, ForwardingChapterContext ctx, Guid? applicationChapterId, Guid? applicationCaseId, DateTime nowUtc) {
        var record = dueRecord;
        if (record == null && plan.StoreRecord) {
            record = DueSelection.FromDto(dto.DueSelection!);
            record.FirstName = dto.FirstName;
            record.LastName = dto.LastName;
            record.Email = dto.Email;
            record.ChapterId = plan.RecordChapterId;
            record.Status = DueSelectionStatus.Pending;
            _dueSelections.Create(record);
        }

        Guid? motionId = null;
        if (record != null && plan.MotionChapterId != null) {
            motionId = _review.CreateReductionMotion(record, plan.MotionChapterId.Value, application);
            NotifyProcessors(record);
        }

        if (plan.Tickets.Count == 0)
            return;

        var reductionCase = new ForwardingCase {
            Kind = ForwardingCaseKind.DueSelection,
            ApplicationChapterId = applicationChapterId,
            DecidingChapterId = plan.DecidingChapterId,
            StateChapterId = ctx.StateId,
            RelatedCaseId = applicationCaseId,
            MembershipApplicationId = application?.Id,
            DueSelectionId = record?.Id,
            MotionId = motionId,
            PayloadJson = JsonSerializer.Serialize(new ForwardingPayload(dto, dto.DueSelection, null, null, false, nowUtc)),
            CreatedAt = nowUtc
        };
        _cases.Create(reductionCase, Steps(plan, ForwardingPurpose.Reduction, nowUtc));
        if (applicationCaseId != null)
            _cases.SetRelatedCase(applicationCaseId.Value, reductionCase.Id);
    }

    private ForwardingPlan PlanReduction(ForwardingChapterContext ctx, DueSelectionDTO dues) {
        return ForwardingRouter.PlanDueSelection(ctx, DueAmount.Effective(dues), true, IsEnabled(ForwardingOptionKeys.DueSelectionEnabled),
            RegularMinimum(), StateDecisionMinimum(), true);
    }

    private static DueSelectionStatus DueStatusInApplication(bool isReduced, ForwardingPlan? reductionPlan) {
        if (!isReduced)
            return DueSelectionStatus.AutoApproved;
        return reductionPlan?.MotionChapterId != null ? DueSelectionStatus.Pending : DueSelectionStatus.Forwarded;
    }

    private static List<ForwardingStep> Steps(ForwardingPlan plan, ForwardingPurpose notePurpose, DateTime nowUtc) {
        var steps = new List<ForwardingStep>();
        foreach (var ticket in plan.Tickets) {
            steps.Add(new ForwardingStep {
                Order = steps.Count + 1,
                Kind = ForwardingStepKind.CreateTicket,
                TargetChapterId = ticket.TargetChapterId,
                Purpose = ticket.Purpose,
                State = ForwardingStepState.Pending,
                UpdatedAt = nowUtc
            });
        }

        if (plan.NoteAfterDecision) {
            steps.Add(new ForwardingStep {
                Order = steps.Count + 1,
                Kind = ForwardingStepKind.AddOutcomeNote,
                TargetChapterId = plan.Tickets[0].TargetChapterId,
                Purpose = notePurpose,
                State = ForwardingStepState.Waiting,
                UpdatedAt = nowUtc
            });
        }

        return steps;
    }

    private void NotifyProcessors(DueSelection record) {
        if (record.ChapterId == null)
            return;

        var chapterId = record.ChapterId.Value;
        var chapter = _chapters.Get(chapterId);
        var chapterName = chapter?.Name ?? "";
        var payload = new DueSelectionSubmittedPayload(record.Id, chapterId, chapterName, record.FirstName, record.LastName, record.SelectedDue);
        _notifications.Enqueue(new NotificationDispatchRequest(
            NotificationTriggers.DueSelectionSubmitted,
            payload,
            _ => new Dictionary<string, object> {
                ["selection"] = record.ToDetailDto(),
                ["chapter"] = chapter?.ToDto() ?? new ChapterDTO { Id = chapterId, Name = chapterName }
            },
            SourceEntityType: "DueSelection",
            SourceEntityId: record.Id));
    }

    private bool IsEnabled(string key) {
        return string.Equals(_options.GetGlobalValue(key)?.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private decimal RegularMinimum() {
        return ReadAmount(ForwardingOptionKeys.RegularMinimum, ForwardingOptionKeys.DefaultRegularMinimum);
    }

    private decimal StateDecisionMinimum() {
        return ReadAmount(ForwardingOptionKeys.StateDecisionMinimum, ForwardingOptionKeys.DefaultStateDecisionMinimum);
    }

    private decimal ReadAmount(string key, decimal fallback) {
        var value = _options.GetGlobalValue(key)?.Value?.Trim().Replace(',', '.');
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount > 0 ? amount : fallback;
    }
}
