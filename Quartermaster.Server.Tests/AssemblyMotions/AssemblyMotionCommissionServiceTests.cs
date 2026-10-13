using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionCommissionServiceTests : RepositoryTestBase {
    private readonly CapturingNotificationQueue _queue = new();
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionCommissionService _commission = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;
    private readonly Guid _userId = Guid.NewGuid();

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, _context, new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance));
        _commission = new AssemblyMotionCommissionService(_motionRepo, AuditLog, new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance));
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id);
    }

    private static AssemblyMotionDraft NewDraft(Guid typeId, string title = "Testantrag")
        => new(typeId, title, null, "Antragstext", "Begründung", null, null, null);

    private (AssemblyMotionPortal Portal, AssemblyMotionType Type, Member Author) SeedPortal(int minSubmitters) {
        var now = DateTime.UtcNow;
        var portal = _portalRepo.Create(_event.Id, now.AddHours(-1), now.AddDays(7), now);
        _context.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.WikiMirrorEnabled, true)
            .Update();
        var type = _portalRepo.GetTypes(portal.Id).First(t => t.MinSubmitters == minSubmitters);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        return (portal, type, author);
    }

    private AssemblyMotion SubmittedMotion() {
        var (portal, type, author) = SeedPortal(1);
        var now = DateTime.UtcNow;
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        return _service.Release(motion.Id, author, now).Motion!;
    }

    [Test]
    public async Task Insufficient_requires_note() {
        var motion = SubmittedMotion();

        var blank = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, "   ", _userId, DateTime.UtcNow);
        var withNote = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, " Frist verpasst ", _userId, DateTime.UtcNow);

        await Assert.That(blank.Succeeded).IsFalse();
        await Assert.That(blank.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CheckNoteRequired);
        await Assert.That(withNote.Succeeded).IsTrue();
        await Assert.That(withNote.Motion!.CheckNote).IsEqualTo("Frist verpasst");
        await Assert.That(withNote.Motion.CheckedByUserId).IsEqualTo(_userId);
        await Assert.That(withNote.Motion.CheckedAt).IsNotNull();
    }

    [Test]
    public async Task Decision_can_be_corrected() {
        var motion = SubmittedMotion();

        var admitted = _commission.Check(motion.Id, AssemblyMotionStatus.Admitted, null, _userId, DateTime.UtcNow);
        var insufficient = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, "Grund", _userId, DateTime.UtcNow);
        var corrected = _commission.Check(motion.Id, AssemblyMotionStatus.Admitted, null, _userId, DateTime.UtcNow);
        var stored = _motionRepo.Get(motion.Id)!;

        await Assert.That(admitted.Succeeded).IsTrue();
        await Assert.That(insufficient.Motion!.Status).IsEqualTo(AssemblyMotionStatus.FormallyInsufficient);
        await Assert.That(corrected.Succeeded).IsTrue();
        await Assert.That(stored.Status).IsEqualTo(AssemblyMotionStatus.Admitted);
        await Assert.That(stored.CheckNote).IsNull();
        await Assert.That(stored.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Check_refused_while_collecting() {
        var (portal, type, author) = SeedPortal(5);
        var now = DateTime.UtcNow;
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var outcome = _commission.Check(motion.Id, AssemblyMotionStatus.Admitted, null, _userId, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Commission_edit_keeps_cosubmitters_and_bumps_submitted_revision() {
        var (portal, type, author) = SeedPortal(5);
        Db.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.MinSubmitters, 2)
            .Update();
        var second = _builder.SeedMember(chapterId: _chapter.Id);
        var now = DateTime.UtcNow;
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);
        _service.Join(motion.Id, second, now);

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(type.Id, "Neuer Titel"), 1, "Tippfehler", now);
        var stored = _motionRepo.Get(motion.Id)!;
        var revisions = _motionRepo.GetRevisions(motion.Id);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(stored.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(stored.Revision).IsEqualTo(2);
        await Assert.That(stored.Title).IsEqualTo("Neuer Titel");
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Count).IsEqualTo(2);
        await Assert.That(stored.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(revisions.Count).IsEqualTo(2);
        await Assert.That(revisions.Single(r => r.Revision == 2).EditedByCommission).IsTrue();
        await Assert.That(revisions.Single(r => r.Revision == 2).ChangeNote).IsEqualTo("Tippfehler");
    }

    [Test]
    public async Task Commission_edit_requires_change_note() {
        var motion = SubmittedMotion();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neuer Titel"), 1, " ", DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.ChangeNoteRequired);
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task Commission_edit_without_change_creates_no_revision() {
        var motion = SubmittedMotion();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId), 1, "Nichts", DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
        await Assert.That(_motionRepo.GetRevisions(motion.Id).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Commission_edit_refuses_stale_revision_and_type_change() {
        var motion = SubmittedMotion();
        var otherType = _portalRepo.GetTypes(motion.PortalId).First(t => t.Id != motion.TypeId);

        var stale = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "X"), 7, "Notiz", DateTime.UtcNow);
        var typeChange = _commission.Edit(motion.Id, _userId, NewDraft(otherType.Id, "X"), 1, "Notiz", DateTime.UtcNow);

        await Assert.That(stale.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.RevisionConflict);
        await Assert.That(typeChange.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Commission_withdraw_after_close_allowed() {
        var motion = SubmittedMotion();
        Db.AssemblyMotionPortals
            .Where(p => p.Id == motion.PortalId)
            .Set(p => p.ClosesAt, DateTime.UtcNow.AddDays(-1))
            .Update();

        var outcome = _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);
        var again = _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
        await Assert.That(_motionRepo.Get(motion.Id)!.WithdrawnAt).IsNotNull();
        await Assert.That(again.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    private AssemblyMotion CollectingMotion() {
        var (portal, type, author) = SeedPortal(5);
        var now = DateTime.UtcNow;
        var motion = _service.CreateDraft(portal, author, _userId, NewDraft(type.Id), now).Motion!;
        return _service.Release(motion.Id, author, now).Motion!;
    }

    [Test]
    public async Task Commission_edit_refused_for_withdrawn() {
        var motion = SubmittedMotion();
        _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "Notiz", DateTime.UtcNow);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(_motionRepo.Get(motion.Id)!.Title).IsEqualTo("Testantrag");
    }

    [Test]
    public async Task Commission_edit_refused_for_lapsed() {
        var motion = SubmittedMotion();
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Status, AssemblyMotionStatus.Lapsed)
            .Update();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "Notiz", DateTime.UtcNow);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Commission_edit_refused_while_collecting() {
        var motion = CollectingMotion();
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Number, 1)
            .Update();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "Notiz", DateTime.UtcNow);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Commission_edit_refused_without_number() {
        var motion = CollectingMotion();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "Notiz", DateTime.UtcNow);

        await Assert.That(motion.Number).IsNull();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Check_refused_for_withdrawn() {
        var motion = SubmittedMotion();
        _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);

        var outcome = _commission.Check(motion.Id, AssemblyMotionStatus.Admitted, null, _userId, DateTime.UtcNow);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
    }

    [Test]
    public async Task Commission_withdraw_of_collecting_motion_does_not_queue_wiki() {
        var motion = CollectingMotion();

        var outcome = _commission.Withdraw(motion.Id, _userId, DateTime.UtcNow);
        var stored = _motionRepo.Get(motion.Id)!;

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(stored.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
        await Assert.That(stored.WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
    }

    [Test]
    [MethodDataSource(typeof(MotionMarkdownSafetyTests), nameof(MotionMarkdownSafetyTests.ShapeShiftingPayloads))]
    public async Task Commission_edit_refuses_text_that_becomes_html_when_normalized(string markdown) {
        var motion = SubmittedMotion();

        var outcome = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId) with { TextMarkdown = markdown }, 1, "Korrektur", DateTime.UtcNow);
        var stored = _motionRepo.Get(motion.Id)!;

        await Assert.That(outcome.StatusCode).IsEqualTo(422);
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.MarkdownHtmlNotAllowed);
        await Assert.That(stored.TextMarkdown).IsEqualTo("Antragstext");
        await Assert.That(stored.Revision).IsEqualTo(1);
        await Assert.That(MotionMarkdownSafetyTests.ForeignMarkup(stored.TextHtml + stored.ReasonHtml)).IsEmpty();
    }

    [Test]
    public async Task Check_note_and_change_note_lose_control_characters() {
        var motion = SubmittedMotion();

        var checkedOutcome = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, "Frist\r\nverpasst\u0001", _userId, DateTime.UtcNow);
        var onlyControl = _commission.Check(motion.Id, AssemblyMotionStatus.FormallyInsufficient, "\r\n\u0001", _userId, DateTime.UtcNow);
        var edited = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "Tippfehler\nkorrigiert", DateTime.UtcNow);
        var revision = _motionRepo.GetRevision(motion.Id, 2);

        await Assert.That(checkedOutcome.Motion!.CheckNote).IsEqualTo("Fristverpasst");
        await Assert.That(onlyControl.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.CheckNoteRequired);
        await Assert.That(edited.Succeeded).IsTrue();
        await Assert.That(revision!.ChangeNote).IsEqualTo("Tippfehlerkorrigiert");
    }

    [Test]
    public async Task Change_note_of_only_control_characters_counts_as_missing() {
        var motion = SubmittedMotion();

        var edited = _commission.Edit(motion.Id, _userId, NewDraft(motion.TypeId, "Neu"), 1, "\r\n\u0002", DateTime.UtcNow);

        await Assert.That(edited.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.ChangeNoteRequired);
    }
}
