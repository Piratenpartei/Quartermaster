using Quartermaster.Api.Forwarding;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Forwarding;
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
        _hook = new ForwardingOutcomeHook(_cases, _motions);
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
        _motions.UpdateApprovalStatus(motion.Id, MotionApprovalStatus.Approved);

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        await Assert.That(_cases.GetSteps(note.CaseId).Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Pending);
    }

    [Test]
    public async Task Pending_motion_keeps_note_waiting() {
        var (motion, note) = Seed();

        _hook.OnMotionResolved(motion.Id, DateTime.UtcNow);

        await Assert.That(_cases.GetSteps(note.CaseId).Single(s => s.Id == note.Id).State).IsEqualTo(ForwardingStepState.Waiting);
    }
}
