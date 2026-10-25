using LinqToDB;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Forwarding.SageEntry;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class SageEntryGateTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private ForwardingCaseRepository _cases = default!;
    private SageEntryGate _gate = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _cases = new ForwardingCaseRepository(Db);
        _gate = new SageEntryGate(_cases, new MembershipApplicationRepository(Db, AuditLog), new DueSelectionRepository(Db, AuditLog));
    }

    private void SeedCase(Guid chapterId, Guid applicationId, bool withSageStep) {
        var steps = new List<ForwardingStep> {
            new() { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow }
        };
        if (withSageStep)
            steps.Add(new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow });
        _cases.Create(new ForwardingCase { Kind = ForwardingCaseKind.Application, MembershipApplicationId = applicationId, CreatedAt = DateTime.UtcNow }, steps);
    }

    [Test]
    public async Task Holds_only_approved_with_sage_step() {
        var chapter = _builder.SeedChapter();
        var approvedWithStep = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var approvedWithoutStep = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var pendingWithStep = _builder.SeedMembershipApplication(chapter.Id);
        var rejectedWithStep = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Rejected);
        var approvedWithoutCase = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        SeedCase(chapter.Id, approvedWithStep.Id, true);
        SeedCase(chapter.Id, approvedWithoutStep.Id, false);
        SeedCase(chapter.Id, pendingWithStep.Id, true);
        SeedCase(chapter.Id, rejectedWithStep.Id, true);

        await Assert.That(_gate.HoldsApprovalMail(approvedWithStep.Id)).IsTrue();
        await Assert.That(_gate.HoldsApprovalMail(approvedWithoutStep.Id)).IsFalse();
        await Assert.That(_gate.HoldsApprovalMail(pendingWithStep.Id)).IsFalse();
        await Assert.That(_gate.HoldsApprovalMail(rejectedWithStep.Id)).IsFalse();
        await Assert.That(_gate.HoldsApprovalMail(approvedWithoutCase.Id)).IsFalse();
        await Assert.That(_gate.HoldsApprovalMail(Guid.NewGuid())).IsFalse();
    }

    private MembershipApplication ApplicationWith(SelectedValuation? valuation, DueSelectionStatus status) {
        Guid? dueId = null;
        if (valuation != null) {
            var due = _builder.SeedDueSelection(status: status);
            Db.DueSelections.Where(d => d.Id == due.Id).Set(d => d.SelectedValuation, valuation.Value).Update();
            dueId = due.Id;
        }

        var application = _builder.SeedMembershipApplication();
        Db.MembershipApplications.Where(a => a.Id == application.Id).Set(a => a.DueSelectionId, dueId).Update();
        return new MembershipApplicationRepository(Db, AuditLog).Get(application.Id)!;
    }

    [Test]
    public async Task Reduction_outcome_per_due_status() {
        await Assert.That(_gate.ReductionOf(ApplicationWith(null, DueSelectionStatus.Pending))).IsEqualTo(ReductionOutcome.None);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.MonthlyPayGroup, DueSelectionStatus.Pending))).IsEqualTo(ReductionOutcome.None);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.Pending))).IsEqualTo(ReductionOutcome.Pending);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.Approved))).IsEqualTo(ReductionOutcome.Approved);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.Rejected))).IsEqualTo(ReductionOutcome.Rejected);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.Forwarded))).IsEqualTo(ReductionOutcome.Outside);
        await Assert.That(_gate.ReductionOf(ApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.AutoApproved))).IsEqualTo(ReductionOutcome.None);
    }

    private ForwardingStep SeedStatusCase(Guid chapterId, Guid applicationId, ForwardingStepState noteState, ForwardingStepState awaitState, DateTime awaitUpdatedAt) {
        var awaitStep = new ForwardingStep { Order = 3, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = awaitState, UpdatedAt = awaitUpdatedAt };
        _cases.Create(new ForwardingCase { Kind = ForwardingCaseKind.Application, MembershipApplicationId = applicationId, CreatedAt = DateTime.UtcNow }, [
            new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Done, TicketUrl = "https://redmine.invalid/issues/7", UpdatedAt = DateTime.UtcNow },
            new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = noteState, UpdatedAt = DateTime.UtcNow },
            awaitStep
        ]);
        return awaitStep;
    }

    private MembershipApplication ApprovedApplicationWith(SelectedValuation? valuation, DueSelectionStatus dueStatus, Guid chapterId) {
        var application = ApplicationWith(valuation, dueStatus);
        Db.MembershipApplications.Where(a => a.Id == application.Id)
            .Set(a => a.Status, ApplicationStatus.Approved)
            .Set(a => a.ChapterId, chapterId)
            .Update();
        return application;
    }

    [Test]
    public async Task Status_per_step_state() {
        var chapter = _builder.SeedChapter();

        var withoutStep = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        SeedCase(chapter.Id, withoutStep.Id, false);
        var none = _gate.StatusOf(withoutStep.Id);
        await Assert.That(none.State).IsEqualTo(ApprovalMailState.None);
        await Assert.That(none.SentAt).IsNull();
        await Assert.That(none.FederalTicketUrl).IsNull();

        var waitingForReduction = ApprovedApplicationWith(SelectedValuation.Reduced, DueSelectionStatus.Pending, chapter.Id);
        SeedStatusCase(chapter.Id, waitingForReduction.Id, ForwardingStepState.Waiting, ForwardingStepState.Waiting, DateTime.UtcNow);
        var reduction = _gate.StatusOf(waitingForReduction.Id);
        await Assert.That(reduction.State).IsEqualTo(ApprovalMailState.WaitingForReduction);
        await Assert.That(reduction.FederalTicketUrl).IsEqualTo("https://redmine.invalid/issues/7");

        var waitingForSage = ApprovedApplicationWith(null, DueSelectionStatus.Pending, chapter.Id);
        SeedStatusCase(chapter.Id, waitingForSage.Id, ForwardingStepState.Done, ForwardingStepState.Pending, DateTime.UtcNow);
        var sage = _gate.StatusOf(waitingForSage.Id);
        await Assert.That(sage.State).IsEqualTo(ApprovalMailState.WaitingForSage);
        await Assert.That(sage.SentAt).IsNull();

        var sentAt = new DateTime(2026, 10, 25, 9, 30, 0, DateTimeKind.Utc);
        var sent = ApprovedApplicationWith(null, DueSelectionStatus.Pending, chapter.Id);
        var sentStep = SeedStatusCase(chapter.Id, sent.Id, ForwardingStepState.Done, ForwardingStepState.Done, sentAt);
        var sentStatus = _gate.StatusOf(sent.Id);
        await Assert.That(sentStatus.State).IsEqualTo(ApprovalMailState.Sent);
        await Assert.That(sentStatus.SentAt).IsEqualTo(sentStep.UpdatedAt);

        var skipped = ApprovedApplicationWith(null, DueSelectionStatus.Pending, chapter.Id);
        SeedStatusCase(chapter.Id, skipped.Id, ForwardingStepState.Done, ForwardingStepState.Skipped, DateTime.UtcNow);
        await Assert.That(_gate.StatusOf(skipped.Id).State).IsEqualTo(ApprovalMailState.None);
    }

    [Test]
    public async Task Case_resolved_by_hand_before_the_mail_is_reported() {
        var chapter = _builder.SeedChapter();
        var application = ApprovedApplicationWith(null, DueSelectionStatus.Pending, chapter.Id);
        var awaitStep = SeedStatusCase(chapter.Id, application.Id, ForwardingStepState.Waiting, ForwardingStepState.Waiting, DateTime.UtcNow);

        new ForwardingCaseActions(_cases).Resolve(awaitStep.CaseId, DateTime.UtcNow);

        var status = _gate.StatusOf(application.Id);
        await Assert.That(status.State).IsEqualTo(ApprovalMailState.ManuallyResolved);
        await Assert.That(status.FederalTicketUrl).IsEqualTo("https://redmine.invalid/issues/7");
        await Assert.That(_gate.HoldsApprovalMail(application.Id)).IsTrue();
    }

    [Test]
    [Arguments(ForwardingStepState.Failed, ForwardingStepState.Pending)]
    [Arguments(ForwardingStepState.Done, ForwardingStepState.Failed)]
    public async Task Failed_case_is_reported(ForwardingStepState noteState, ForwardingStepState awaitState) {
        var chapter = _builder.SeedChapter();
        var application = ApprovedApplicationWith(null, DueSelectionStatus.Pending, chapter.Id);
        var awaitStep = SeedStatusCase(chapter.Id, application.Id, noteState, awaitState, DateTime.UtcNow);
        _cases.SetCaseStatus(awaitStep.CaseId, ForwardingCaseStatus.Open, ForwardingCaseStatus.Failed, null);

        var status = _gate.StatusOf(application.Id);

        await Assert.That(status.State).IsEqualTo(ApprovalMailState.Failed);
        await Assert.That(status.FederalTicketUrl).IsEqualTo("https://redmine.invalid/issues/7");
    }

    [Test]
    public async Task Rejection_skip_in_a_resolved_case_stays_none() {
        var chapter = _builder.SeedChapter();
        var application = _builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Rejected);
        var awaitStep = SeedStatusCase(chapter.Id, application.Id, ForwardingStepState.Done, ForwardingStepState.Skipped, DateTime.UtcNow);
        _cases.SetCaseStatus(awaitStep.CaseId, ForwardingCaseStatus.Open, ForwardingCaseStatus.ManuallyResolved, DateTime.UtcNow);

        await Assert.That(_gate.StatusOf(application.Id).State).IsEqualTo(ApprovalMailState.None);
    }
}
