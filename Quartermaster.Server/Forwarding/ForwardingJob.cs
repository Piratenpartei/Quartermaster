using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Forwarding.Redmine;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.MembershipApplications;

namespace Quartermaster.Server.Forwarding;

/// <summary>
/// Creates the tickets and outcome notes of forwarding cases and waits for the SAGE entry of approved applications, step by step in order. A step gets three attempts (now, after 5 and after 15 more minutes);
/// errors that will not go away by waiting fail at once. A failed step stops its case and sends the failure mail.
/// </summary>
public class ForwardingJob {
    public const int BatchSize = 20;
    public const int MaxAttempts = 3;

    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SecondRetryDelay = TimeSpan.FromMinutes(15);
    private static readonly HashSet<string> PermanentCodes = [
        RedmineException.Unauthorized,
        RedmineException.Rejected,
        RedmineException.InsecureUrl,
        RedmineException.NotConfigured
    ];

    private readonly ForwardingCaseRepository _cases;
    private readonly ForwardingSettingsRepository _settings;
    private readonly ForwardingSettingsResolver _resolver;
    private readonly ForwardingTicketRenderer _renderer;
    private readonly IRedmineClient _redmine;
    private readonly MotionRepository _motions;
    private readonly DueSelectionRepository _dueSelections;
    private readonly MembershipApplicationRepository _applications;
    private readonly ForwardingFailureMailer _mailer;
    private readonly SageEntryGate _sageGate;
    private readonly SageEntryNote _sageNote;
    private readonly SageEntryCheck _sageCheck;
    private readonly SageEntrySetup _sageSetup;
    private readonly MembershipApplicationMailService _applicationMails;
    private readonly ILogger<ForwardingJob> _logger;

    public ForwardingJob(ForwardingCaseRepository cases, ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver,
        ForwardingTicketRenderer renderer, IRedmineClient redmine, MotionRepository motions, DueSelectionRepository dueSelections,
        MembershipApplicationRepository applications, ForwardingFailureMailer mailer, SageEntryGate sageGate, SageEntryNote sageNote,
        SageEntryCheck sageCheck, SageEntrySetup sageSetup, MembershipApplicationMailService applicationMails, ILogger<ForwardingJob> logger) {
        _cases = cases;
        _settings = settings;
        _resolver = resolver;
        _renderer = renderer;
        _redmine = redmine;
        _motions = motions;
        _dueSelections = dueSelections;
        _applications = applications;
        _mailer = mailer;
        _sageGate = sageGate;
        _sageNote = sageNote;
        _sageCheck = sageCheck;
        _sageSetup = sageSetup;
        _applicationMails = applicationMails;
        _logger = logger;
    }

    /// <summary>Processes the cases with due steps and returns how many steps were completed.</summary>
    public async Task<int> RunAsync(DateTime nowUtc, CancellationToken ct) {
        var completed = 0;
        foreach (var caseId in _cases.ListCasesWithDueSteps(nowUtc, BatchSize)) {
            ct.ThrowIfCancellationRequested();
            try {
                completed += await ProcessCaseAsync(caseId, nowUtc, ct);
            } catch (Exception ex) when (!ct.IsCancellationRequested) {
                _logger.LogError(ex, "Forwarding case {CaseId} failed unexpectedly", caseId);
            }
        }

        return completed;
    }

    private async Task<int> ProcessCaseAsync(Guid caseId, DateTime nowUtc, CancellationToken ct) {
        var forwardingCase = _cases.Get(caseId);
        if (forwardingCase == null || forwardingCase.Status != ForwardingCaseStatus.Open)
            return 0;

        var steps = _cases.GetSteps(caseId);
        var completed = 0;
        foreach (var step in steps) {
            if (step.State == ForwardingStepState.Done || step.State == ForwardingStepState.Skipped)
                continue;
            if (!IsDue(step, nowUtc))
                break;

            var done = step.Kind switch {
                ForwardingStepKind.CreateTicket => await CreateTicketAsync(forwardingCase, step, steps, nowUtc, ct),
                ForwardingStepKind.AwaitSageEntry => await AwaitSageEntryAsync(forwardingCase, step, steps, nowUtc, ct),
                _ => await AddNoteAsync(forwardingCase, step, steps, nowUtc, ct)
            };
            if (!done)
                break;
            completed++;
        }

        Finish(forwardingCase, steps, nowUtc);
        return completed;
    }

    private static bool IsDue(ForwardingStep step, DateTime nowUtc) {
        if (step.State == ForwardingStepState.InFlight)
            return true;
        if (step.State != ForwardingStepState.Pending)
            return false;
        return step.NextAttemptAt == null || step.NextAttemptAt <= nowUtc;
    }

    private async Task<bool> CreateTicketAsync(ForwardingCase forwardingCase, ForwardingStep step, List<ForwardingStep> steps, DateTime nowUtc, CancellationToken ct) {
        RenderedTicket? ticket = null;
        try {
            var target = _settings.GetTarget(step.TargetChapterId, step.Purpose)
                ?? throw new RedmineException("Für diese Gliederung ist kein Zielprojekt für diese Art von Vorgang eingerichtet.", RedmineException.NotConfigured);
            var connection = Connection(step.TargetChapterId);
            var federal = steps.FirstOrDefault(s => s.Kind == ForwardingStepKind.CreateTicket && s.Order < step.Order && s.TicketId != null);
            ticket = await _renderer.RenderTicketAsync(forwardingCase, step, target, federal?.TicketId, federal?.TicketUrl);

            // A retry (also one started by hand, which resets the attempts but keeps the last error) or a run after a crash may follow a request
            // that Redmine already carried out; adopt that ticket instead of creating a second one.
            int? ticketId = null;
            if (step.State == ForwardingStepState.InFlight || step.Attempts > 0 || step.LastError != null)
                ticketId = await _redmine.FindIssueByReferenceAsync(connection, target.ProjectId, ForwardingTicketRenderer.ReferenceLine(forwardingCase, step), ct);

            if (ticketId == null) {
                // A step resolved or retried by hand since this run read it belongs to that action now.
                var readState = step.State;
                step.State = ForwardingStepState.InFlight;
                step.UpdatedAt = nowUtc;
                if (!_cases.UpdateStep(step, readState))
                    return false;
                ticketId = await _redmine.CreateIssueAsync(connection, new RedmineIssueCreate(target.ProjectId, target.TrackerId, ticket.Subject, ticket.Description,
                    target.PriorityId, target.AssigneeId, ticket.CustomFields), ct);
            }

            step.State = ForwardingStepState.Done;
            step.TicketId = ticketId;
            step.TicketUrl = connection.IssueUrl(ticketId.Value);
            step.LastError = null;
            step.NextAttemptAt = null;
            step.UpdatedAt = nowUtc;
            _cases.UpdateStep(step);
            return true;
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            await RecordFailureAsync(forwardingCase, step, ex, ticket, nowUtc, ct);
            return false;
        }
    }

    private async Task<bool> AddNoteAsync(ForwardingCase forwardingCase, ForwardingStep step, List<ForwardingStep> steps, DateTime nowUtc, CancellationToken ct) {
        var federal = FederalTicket(step, steps);
        var decision = DecisionFor(forwardingCase);
        if (decision == null && AwaitsDecision(forwardingCase)) {
            WaitForDecision(forwardingCase, step, nowUtc);
            return false;
        }
        if (federal == null || decision == null)
            return Skip(step, nowUtc);

        var approved = ApprovedApplication(forwardingCase, steps, decision);
        if (approved != null && _sageGate.ReductionOf(approved) == ReductionOutcome.Pending) {
            WaitForReduction(step, approved, nowUtc);
            return false;
        }

        try {
            var target = _settings.GetTarget(step.TargetChapterId, step.Purpose);
            var connection = Connection(step.TargetChapterId);
            var note = await _renderer.RenderNoteAsync(forwardingCase, step, decision);
            var update = new RedmineIssueUpdate(note, target?.NoteStatusId, target?.NoteAssigneeId);
            if (approved != null) {
                var sageTarget = _sageSetup.RequireTarget(target);
                await _sageCheck.RequireNotOnDoneStatusAsync(sageTarget, federal.TicketId!.Value, connection, ct);
                var request = await _sageNote.BuildAsync(forwardingCase, step, approved, federal.TicketId!.Value, sageTarget, connection, ct);
                update = new RedmineIssueUpdate(note + "\n\n" + request, null, sageTarget.SageAssigneeId);
            }

            await _redmine.UpdateIssueAsync(connection, federal.TicketId!.Value, update, ct);
            MarkDone(step, federal, nowUtc);
            _cases.UpdateStep(step);
            return true;
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            await RecordFailureAsync(forwardingCase, step, ex, null, nowUtc, ct);
            return false;
        }
    }

    /// <summary>
    /// Holds the approval mail until the federal ticket reports the SAGE entry. Waiting is no error, so a check that finds the entry missing
    /// resets the attempts and stops the case until the next check.
    /// </summary>
    private async Task<bool> AwaitSageEntryAsync(ForwardingCase forwardingCase, ForwardingStep step, List<ForwardingStep> steps, DateTime nowUtc, CancellationToken ct) {
        var federal = FederalTicket(step, steps);
        var decision = DecisionFor(forwardingCase);
        if (decision == null && AwaitsDecision(forwardingCase)) {
            WaitForDecision(forwardingCase, step, nowUtc);
            return false;
        }
        if (federal == null || decision == null || decision.Result != MotionApprovalStatus.Approved)
            return Skip(step, nowUtc);

        try {
            var target = _sageSetup.RequireTarget(_settings.GetTarget(step.TargetChapterId, step.Purpose));
            if (!await _sageCheck.IsEnteredAsync(target, federal.TicketId!.Value, Connection(step.TargetChapterId), ct)) {
                ScheduleNextCheck(step, nowUtc);
                return false;
            }

            // Claimed before the mail, so a case resolved by hand in the meantime ends without it.
            var readState = step.State;
            step.State = ForwardingStepState.InFlight;
            step.UpdatedAt = nowUtc;
            if (!_cases.UpdateStep(step, readState))
                return false;

            await SendApprovalMailAsync(forwardingCase, ct);
            MarkDone(step, federal, nowUtc);
            return _cases.UpdateStep(step, ForwardingStepState.InFlight);
        } catch (Exception ex) when (!IsShutdown(ex, ct)) {
            await RecordFailureAsync(forwardingCase, step, ex, null, nowUtc, ct);
            return false;
        }
    }

    private async Task SendApprovalMailAsync(ForwardingCase forwardingCase, CancellationToken ct) {
        var application = forwardingCase.MembershipApplicationId == null ? null : _applications.Get(forwardingCase.MembershipApplicationId.Value);
        if (application == null) {
            _logger.LogWarning("Forwarding case {Reference} found the SAGE entry, but its application is gone; no approval mail sent", forwardingCase.Reference);
            return;
        }

        await _applicationMails.SendApplicationDecisionAsync(application, ct);
    }

    private static ForwardingStep? FederalTicket(ForwardingStep step, List<ForwardingStep> steps) {
        return steps.FirstOrDefault(s => s.Kind == ForwardingStepKind.CreateTicket && s.TargetChapterId == step.TargetChapterId && s.TicketId != null);
    }

    /// <summary>Cases created before the SAGE entry wait existed have no such step; their approval mail went out at once, so their note asks for nothing.</summary>
    private MembershipApplication? ApprovedApplication(ForwardingCase forwardingCase, List<ForwardingStep> steps, ForwardingDecision decision) {
        if (forwardingCase.Kind != ForwardingCaseKind.Application || forwardingCase.MembershipApplicationId == null)
            return null;
        if (decision.Result != MotionApprovalStatus.Approved)
            return null;
        if (!steps.Any(s => s.Kind == ForwardingStepKind.AwaitSageEntry))
            return null;
        return _applications.Get(forwardingCase.MembershipApplicationId.Value);
    }

    /// <summary>An application that is still open, for example after its vote closed without action, is decided later and releases its steps then.</summary>
    private bool AwaitsDecision(ForwardingCase forwardingCase) {
        if (forwardingCase.Kind != ForwardingCaseKind.Application || forwardingCase.MembershipApplicationId == null)
            return false;
        var application = _applications.Get(forwardingCase.MembershipApplicationId.Value);
        if (application == null)
            return false;
        return application.Status != ApplicationStatus.Approved && application.Status != ApplicationStatus.Rejected;
    }

    /// <summary>Like <see cref="WaitForReduction"/>: a decision between the first read and this write would find the step not yet waiting, so it is read once more.</summary>
    private void WaitForDecision(ForwardingCase forwardingCase, ForwardingStep step, DateTime nowUtc) {
        var readState = step.State;
        step.State = ForwardingStepState.Waiting;
        step.NextAttemptAt = null;
        step.UpdatedAt = nowUtc;
        if (!_cases.UpdateStep(step, readState))
            return;
        if (DecisionFor(forwardingCase) == null)
            return;

        step.State = ForwardingStepState.Pending;
        _cases.UpdateStep(step, ForwardingStepState.Waiting);
    }

    /// <summary>
    /// The outcome hook releases the note when the reduction is decided. A decision that lands between the first read and this write would find
    /// the note not yet waiting and leave it alone, so the reduction is read once more after the write.
    /// </summary>
    private void WaitForReduction(ForwardingStep step, MembershipApplication application, DateTime nowUtc) {
        var readState = step.State;
        step.State = ForwardingStepState.Waiting;
        step.NextAttemptAt = null;
        step.UpdatedAt = nowUtc;
        if (!_cases.UpdateStep(step, readState))
            return;
        if (_sageGate.ReductionOf(application) == ReductionOutcome.Pending)
            return;

        step.State = ForwardingStepState.Pending;
        _cases.UpdateStep(step, ForwardingStepState.Waiting);
    }

    private void ScheduleNextCheck(ForwardingStep step, DateTime nowUtc) {
        var readState = step.State;
        step.State = ForwardingStepState.Pending;
        step.Attempts = 0;
        step.LastError = null;
        step.NextAttemptAt = nowUtc + _sageSetup.CheckInterval();
        step.UpdatedAt = nowUtc;
        _cases.UpdateStep(step, readState);
    }

    private bool Skip(ForwardingStep step, DateTime nowUtc) {
        var readState = step.State;
        step.State = ForwardingStepState.Skipped;
        step.UpdatedAt = nowUtc;
        return _cases.UpdateStep(step, readState);
    }

    private static void MarkDone(ForwardingStep step, ForwardingStep federal, DateTime nowUtc) {
        step.State = ForwardingStepState.Done;
        step.TicketId = federal.TicketId;
        step.TicketUrl = federal.TicketUrl;
        step.LastError = null;
        step.NextAttemptAt = null;
        step.UpdatedAt = nowUtc;
    }

    /// <summary>The board vote when there was one, otherwise the decision taken directly on the due selection or application.</summary>
    private ForwardingDecision? DecisionFor(ForwardingCase forwardingCase) {
        var motion = forwardingCase.MotionId == null ? null : _motions.Get(forwardingCase.MotionId.Value);
        if (motion != null && ForwardingOutcomeHook.Decides(motion, forwardingCase))
            return new ForwardingDecision(motion.ApprovalStatus, motion.ChapterId, motion.ResolvedAt ?? DateTime.UtcNow, $"/Administration/Motions/{motion.Id}");

        var chapterId = forwardingCase.DecidingChapterId ?? motion?.ChapterId;
        if (chapterId == null)
            return null;

        if (forwardingCase.Kind == ForwardingCaseKind.Application && forwardingCase.MembershipApplicationId != null) {
            var application = _applications.Get(forwardingCase.MembershipApplicationId.Value);
            var result = application == null ? null : ResultOf(application.Status);
            return result == null
                ? null
                : new ForwardingDecision(result.Value, chapterId.Value, application!.ProcessedAt ?? DateTime.UtcNow, $"/Administration/MembershipApplications/{application.Id}");
        }

        if (forwardingCase.Kind == ForwardingCaseKind.DueSelection && forwardingCase.DueSelectionId != null) {
            var dueSelection = _dueSelections.Get(forwardingCase.DueSelectionId.Value);
            var result = dueSelection == null ? null : ResultOf(dueSelection.Status);
            return result == null
                ? null
                : new ForwardingDecision(result.Value, chapterId.Value, dueSelection!.ProcessedAt ?? DateTime.UtcNow, $"/Administration/DueSelections/{dueSelection.Id}");
        }

        return null;
    }

    private static MotionApprovalStatus? ResultOf(ApplicationStatus status) {
        return status switch {
            ApplicationStatus.Approved => MotionApprovalStatus.Approved,
            ApplicationStatus.Rejected => MotionApprovalStatus.Rejected,
            _ => null
        };
    }

    private static MotionApprovalStatus? ResultOf(DueSelectionStatus status) {
        return status switch {
            DueSelectionStatus.Approved => MotionApprovalStatus.Approved,
            DueSelectionStatus.Rejected => MotionApprovalStatus.Rejected,
            _ => null
        };
    }

    private RedmineConnectionSettings Connection(Guid chapterId) {
        var connection = _resolver.ConnectionFor(chapterId)
            ?? throw new RedmineException("Für diese Gliederung ist keine Redmine-Verbindung eingerichtet.", RedmineException.NotConfigured);
        return new RedmineConnectionSettings(connection.BaseUrl, connection.ApiKey);
    }

    private async Task RecordFailureAsync(ForwardingCase forwardingCase, ForwardingStep step, Exception exception, RenderedTicket? ticket, DateTime nowUtc, CancellationToken ct) {
        var readState = step.State;
        step.Attempts++;
        step.LastError = exception is RedmineException ? exception.Message : "Interner Fehler bei der Weiterleitung, Details stehen im Serverprotokoll.";
        step.UpdatedAt = nowUtc;

        var permanent = exception is RedmineException redmine && PermanentCodes.Contains(redmine.Code);
        if (permanent || step.Attempts >= MaxAttempts) {
            step.State = ForwardingStepState.Failed;
            step.NextAttemptAt = null;
            if (!_cases.UpdateStep(step, readState))
                return;
            if (!_cases.SetCaseStatus(forwardingCase.Id, ForwardingCaseStatus.Open, ForwardingCaseStatus.Failed, null))
                return;
            forwardingCase.Status = ForwardingCaseStatus.Failed;
            _logger.LogWarning(exception, "Forwarding step {StepId} of case {Reference} failed for good after {Attempts} attempts", step.Id, forwardingCase.Reference, step.Attempts);
            await _mailer.SendAsync(forwardingCase, step, ticket, ct);
            return;
        }

        step.State = ForwardingStepState.Pending;
        step.NextAttemptAt = nowUtc + (step.Attempts == 1 ? FirstRetryDelay : SecondRetryDelay);
        if (!_cases.UpdateStep(step, readState))
            return;
        _logger.LogWarning(exception, "Forwarding step {StepId} of case {Reference} failed (attempt {Attempts}), retrying at {NextAttemptAt}",
            step.Id, forwardingCase.Reference, step.Attempts, step.NextAttemptAt);
    }

    private void Finish(ForwardingCase forwardingCase, List<ForwardingStep> steps, DateTime nowUtc) {
        var ticketsDone = steps
            .Where(s => s.Kind == ForwardingStepKind.CreateTicket)
            .All(s => s.State == ForwardingStepState.Done || s.State == ForwardingStepState.Skipped);
        if (ticketsDone && forwardingCase.PayloadJson != null) {
            _cases.ClearPayload(forwardingCase.Id);
            forwardingCase.PayloadJson = null;
        }

        var allDone = steps.All(s => s.State == ForwardingStepState.Done || s.State == ForwardingStepState.Skipped);
        if (allDone && forwardingCase.Status == ForwardingCaseStatus.Open)
            _cases.SetCaseStatus(forwardingCase.Id, ForwardingCaseStatus.Open, ForwardingCaseStatus.Done, nowUtc);
    }

    private static bool IsShutdown(Exception exception, CancellationToken ct) {
        return exception is OperationCanceledException && ct.IsCancellationRequested;
    }
}
