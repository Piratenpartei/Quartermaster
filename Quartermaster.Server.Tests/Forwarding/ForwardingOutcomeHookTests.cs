using Quartermaster.Api.Forwarding;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Forwarding;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Forwarding;

public class ForwardingOutcomeHookTests : RepositoryTestBase {
    private ForwardingCaseRepository _cases = default!;
    private MotionRepository _motions = default!;
    private ForwardingOutcomeHook _hook = default!;

    [Before(Test)]
    public void Setup() {
        _cases = new ForwardingCaseRepository(Db);
        _motions = new MotionRepository(Db, AuditLog);
        _hook = new ForwardingOutcomeHook(_cases, _motions, new MembershipApplicationRepository(Db, AuditLog));
    }

    private (Motion Motion, ForwardingStep Note) Seed() {
        var chapter = new TestDataBuilder(Db).SeedChapter();
        var motion = Motion.Create(chapter.Id, "Anna", "anna@example.org", "Beitragsminderung", "x", "<p>x</p>", DateTime.UtcNow, null, null);
        _motions.Create(motion);
        var forwardingCase = new ForwardingCase { Kind = ForwardingCaseKind.DueSelection, MotionId = motion.Id, CreatedAt = DateTime.UtcNow };
        var note = new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow };
        _cases.Create(forwardingCase, [
            new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapter.Id, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow },
            note
        ]);
        return (motion, note);
    }

    [Test]
    public async Task Resolved_motion_activates_note() {
        var (motion, note) = Seed();
        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.Approved);

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        await Assert.That(_cases.GetSteps(note.CaseId).Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Pending_motion_keeps_note_waiting() {
        var (motion, note) = Seed();

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        await Assert.That(_cases.GetSteps(note.CaseId).Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Waiting);
    }

    [Test]
    public async Task Motion_closed_without_action_still_releases_a_due_selection_note() {
        var (motion, note) = Seed();
        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.ClosedWithoutAction);

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        await Assert.That(_cases.GetSteps(note.CaseId).Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Motion_closed_without_action_keeps_application_steps_waiting() {
        var chapter = new TestDataBuilder(Db).SeedChapter();
        var application = new TestDataBuilder(Db).SeedMembershipApplication(chapter.Id);
        var motion = Motion.Create(chapter.Id, "Anna", "anna@example.org", "Mitgliedsantrag", "x", "<p>x</p>", DateTime.UtcNow, application.Id, null);
        _motions.Create(motion);
        var steps = SeedApplicationCase(chapter.Id, application.Id, motion.Id);
        _motions.UpdatePendingApprovalStatus(motion.Id, MotionApprovalStatus.ClosedWithoutAction);

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        var after = _cases.GetSteps(steps[0].CaseId);
        await Assert.That(after.Count(s => s.State == ForwardingStepState.Waiting)).IsEqualTo(2);
    }

    [Test]
    public async Task Release_frees_note_and_sage_step() {
        var chapter = new TestDataBuilder(Db).SeedChapter();
        var application = new TestDataBuilder(Db).SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var steps = SeedApplicationCase(chapter.Id, application.Id);

        _hook.OnRecordDecided(ForwardingCaseKind.Application, application.Id, DateTime.UtcNow);

        var after = _cases.GetSteps(steps[0].CaseId);
        await Assert.That(after.Single(s => s.Kind == ForwardingStepKind.AddOutcomeNote).State).IsEqualTo(ForwardingStepState.Pending);
        await Assert.That(after.Single(s => s.Kind == ForwardingStepKind.AwaitSageEntry).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Reduction_decision_releases_decided_application() {
        var chapter = new TestDataBuilder(Db).SeedChapter();
        var application = new TestDataBuilder(Db).SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        var applicationSteps = SeedApplicationCase(chapter.Id, application.Id);
        var reductionCase = SeedReductionCase(chapter.Id, application.Id);

        _hook.OnRecordDecided(ForwardingCaseKind.DueSelection, reductionCase, DateTime.UtcNow);

        var after = _cases.GetSteps(applicationSteps[0].CaseId);
        await Assert.That(after.Count(s => s.State == ForwardingStepState.Waiting)).IsEqualTo(0);
    }

    [Test]
    public async Task Reduction_decision_leaves_undecided_application_waiting() {
        var chapter = new TestDataBuilder(Db).SeedChapter();
        var application = new TestDataBuilder(Db).SeedMembershipApplication(chapter.Id);
        var applicationSteps = SeedApplicationCase(chapter.Id, application.Id);
        var reductionCase = SeedReductionCase(chapter.Id, application.Id);

        _hook.OnRecordDecided(ForwardingCaseKind.DueSelection, reductionCase, DateTime.UtcNow);

        var after = _cases.GetSteps(applicationSteps[0].CaseId);
        await Assert.That(after.Count(s => s.State == ForwardingStepState.Waiting)).IsEqualTo(2);
    }

    private List<ForwardingStep> SeedApplicationCase(Guid chapterId, Guid applicationId, Guid? motionId = null) {
        var forwardingCase = new ForwardingCase { Kind = ForwardingCaseKind.Application, MembershipApplicationId = applicationId, MotionId = motionId, CreatedAt = DateTime.UtcNow };
        _cases.Create(forwardingCase, [
            new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow },
            new ForwardingStep { Order = 2, Kind = ForwardingStepKind.AddOutcomeNote, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow },
            new ForwardingStep { Order = 3, Kind = ForwardingStepKind.AwaitSageEntry, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Application, State = ForwardingStepState.Waiting, UpdatedAt = DateTime.UtcNow }
        ]);
        return _cases.GetSteps(forwardingCase.Id);
    }

    private Guid SeedReductionCase(Guid chapterId, Guid applicationId) {
        var dueRecord = new TestDataBuilder(Db).SeedDueSelection();
        var forwardingCase = new ForwardingCase { Kind = ForwardingCaseKind.DueSelection, MembershipApplicationId = applicationId, DueSelectionId = dueRecord.Id, CreatedAt = DateTime.UtcNow };
        _cases.Create(forwardingCase, [
            new ForwardingStep { Order = 1, Kind = ForwardingStepKind.CreateTicket, TargetChapterId = chapterId, Purpose = ForwardingPurpose.Reduction, State = ForwardingStepState.Done, UpdatedAt = DateTime.UtcNow }
        ]);
        return dueRecord.Id;
    }
}
