using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.AuditLog;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionServiceTests : RepositoryTestBase {
    private readonly CapturingNotificationQueue _queue = new();
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionService _service = default!;
    private Chapter _chapter = default!;
    private Event _event = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, _context, new AssemblyMotionNotifier(_queue, _context, NullLogger<AssemblyMotionNotifier>.Instance));
        _chapter = _builder.SeedChapter();
        _event = _builder.SeedEvent(_chapter.Id);
    }

    private AssemblyMotionPortal SeedPortal(DateTime opensAt, DateTime closesAt) {
        var portal = _portalRepo.Create(_event.Id, opensAt, closesAt, DateTime.UtcNow);
        _context.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.WikiMirrorEnabled, true)
            .Update();
        return portal;
    }

    private Member SeedEligibleMember()
        => _builder.SeedMember(chapterId: _chapter.Id);

    private static AssemblyMotionDraft NewDraft(Guid typeId)
        => new(typeId, "Testantrag", null, "Antragstext", "Begründung", null, null, null);

    private AssemblyMotionType TypeWithMinSubmitters(Guid portalId, int minSubmitters)
        => _portalRepo.GetTypes(portalId).First(t => t.MinSubmitters == minSubmitters);

    private static AssemblyMotionService NewServiceOn(DbContext context) {
        var auditLog = new AuditLogRepository(context);
        return new AssemblyMotionService(new AssemblyMotionRepository(context, auditLog), new AssemblyMotionPortalRepository(context, auditLog), auditLog, context, new AssemblyMotionNotifier(new CapturingNotificationQueue(), context, NullLogger<AssemblyMotionNotifier>.Instance));
    }

    [Test]
    public async Task Release_with_one_required_submits_immediately_with_number_1() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var outcome = _service.Release(motion.Id, author, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(outcome.Motion!.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(outcome.Motion!.Number).IsEqualTo(1);
    }

    [Test]
    public async Task Release_with_five_required_collects() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var outcome = _service.Release(motion.Id, author, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(outcome.Motion!.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(outcome.Motion!.Number).IsNull();
    }

    [Test]
    public async Task Fifth_join_submits_and_numbers() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        AssemblyMotionOutcome lastOutcome = null!;
        for (var i = 0; i < 4; i++) {
            var coSubmitter = SeedEligibleMember();
            lastOutcome = _service.Join(motion.Id, coSubmitter, now);
        }

        await Assert.That(lastOutcome.Succeeded).IsTrue();
        await Assert.That(lastOutcome.Motion!.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(lastOutcome.Motion!.Number).IsEqualTo(1);
        await Assert.That(_motionRepo.Get(motion.Id)!.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Numbers_are_sequential_per_type() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var soType = _portalRepo.GetTypes(portal.Id).First(t => t.Code == "SO");
        var ppType = _portalRepo.GetTypes(portal.Id).First(t => t.Code == "PP");
        var author = SeedEligibleMember();

        var soMotion1 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(soType.Id), now).Motion!;
        var soMotion2 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(soType.Id), now).Motion!;
        var ppMotion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(ppType.Id), now).Motion!;

        var soOutcome1 = _service.Release(soMotion1.Id, author, now);
        var soOutcome2 = _service.Release(soMotion2.Id, author, now);
        var ppOutcome = _service.Release(ppMotion.Id, author, now);

        await Assert.That(soOutcome1.Motion!.Number).IsEqualTo(1);
        await Assert.That(soOutcome2.Motion!.Number).IsEqualTo(2);
        await Assert.That(ppOutcome.Motion!.Number).IsEqualTo(1);
    }

    [Test]
    public async Task Withdrawn_number_is_not_reused() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();

        var motion1 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion1.Id, author, now);
        _service.Withdraw(motion1.Id, author, now);

        var motion2 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        var outcome2 = _service.Release(motion2.Id, author, now);

        await Assert.That(outcome2.Motion!.Number).IsEqualTo(2);
    }

    [Test]
    public async Task Join_refused_after_deadline() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        type.Deadline = now.AddMinutes(10);
        _portalRepo.UpdateType(type);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var coSubmitter = SeedEligibleMember();
        var outcome = _service.Join(motion.Id, coSubmitter, now.AddMinutes(20));

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).Contains("deadline=");
    }

    [Test]
    public async Task Release_refused_one_minute_after_deadline() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        type.Deadline = now;
        _portalRepo.UpdateType(type);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var outcome = _service.Release(motion.Id, author, now.AddMinutes(1));

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).Contains(I18nKey.Error.AssemblyMotion.DeadlinePassed);
    }

    [Test]
    public async Task Release_allowed_one_minute_before_deadline() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        type.Deadline = now;
        _portalRepo.UpdateType(type);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now.AddMinutes(-2)).Motion!;

        var outcome = _service.Release(motion.Id, author, now.AddMinutes(-1));

        await Assert.That(outcome.Succeeded).IsTrue();
    }

    [Test]
    public async Task Create_refused_before_opens_and_after_closes() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now, now.AddHours(1));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();

        var beforeOpen = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now.AddMinutes(-1));
        var afterClose = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now.AddHours(2));

        await Assert.That(beforeOpen.Succeeded).IsFalse();
        await Assert.That(beforeOpen.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PortalNotOpen);
        await Assert.That(afterClose.Succeeded).IsFalse();
        await Assert.That(afterClose.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PortalClosed);
    }

    [Test]
    public async Task Leave_refused_for_author() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var outcome = _service.Leave(motion.Id, author, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.AuthorCannotLeave);
    }

    [Test]
    public async Task Join_twice_refused() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var coSubmitter = SeedEligibleMember();
        var firstJoin = _service.Join(motion.Id, coSubmitter, now);
        var secondJoin = _service.Join(motion.Id, coSubmitter, now);

        await Assert.That(firstJoin.Succeeded).IsTrue();
        await Assert.That(secondJoin.Succeeded).IsFalse();
        await Assert.That(secondJoin.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.AlreadySubmitter);
    }

    [Test]
    public async Task Stale_base_revision_conflicts() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var firstUpdate = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Erste Überarbeitung" }, baseRevision: 1, now);
        await Assert.That(firstUpdate.Succeeded).IsTrue();

        var staleUpdate = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Zweite Überarbeitung" }, baseRevision: 1, now);

        await Assert.That(staleUpdate.Succeeded).IsFalse();
        await Assert.That(staleUpdate.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.RevisionConflict);
    }

    [Test]
    public async Task LapseOverdue_lapses_only_past_effective_deadline() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(60));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        type.Deadline = now.AddMinutes(10);
        type.CompetingDeadline = now.AddMinutes(30);
        _portalRepo.UpdateType(type);

        var authorA = SeedEligibleMember();
        var motionA = _service.CreateDraft(portal, authorA, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motionA.Id, authorA, now);

        var authorB = SeedEligibleMember();
        var motionB = _service.CreateDraft(portal, authorB, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        motionB.IsCompetingMotion = true;
        _motionRepo.Update(motionB);
        _service.Release(motionB.Id, authorB, now);

        var lapsedCount = _service.LapseOverdue(now.AddMinutes(20));

        await Assert.That(lapsedCount).IsEqualTo(1);
        await Assert.That(_motionRepo.Get(motionA.Id)!.Status).IsEqualTo(AssemblyMotionStatus.Lapsed);
        await Assert.That(_motionRepo.Get(motionB.Id)!.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
    }

    [Test]
    public async Task Concurrent_last_joins_submit_once() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        type.MinSubmitters = 3;
        _portalRepo.UpdateType(type);

        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var secondSubmitter = SeedEligibleMember();
        _service.Join(motion.Id, secondSubmitter, now);

        var memberA = SeedEligibleMember();
        var memberB = SeedEligibleMember();

        using var dbA = Worker.CreateDbContext();
        using var dbB = Worker.CreateDbContext();
        var serviceA = NewServiceOn(dbA);
        var serviceB = NewServiceOn(dbB);

        var taskA = Task.Run(() => serviceA.Join(motion.Id, memberA, now));
        var taskB = Task.Run(() => serviceB.Join(motion.Id, memberB, now));
        var results = await Task.WhenAll(taskA, taskB);

        await Assert.That(results.Count(r => r.Succeeded)).IsEqualTo(1);
        var failed = results.First(r => !r.Succeeded);
        await Assert.That(failed.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);

        var submitters = _motionRepo.GetSubmitters(motion.Id);
        await Assert.That(submitters.Count).IsEqualTo(3);

        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(reloaded.Number).IsEqualTo(1);

        var submittedAuditEntries = AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "Submitted");
        await Assert.That(submittedAuditEntries).IsEqualTo(1);
    }

    [Test]
    public async Task Leave_after_concurrent_submit_is_refused() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        type.MinSubmitters = 3;
        _portalRepo.UpdateType(type);

        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var leavingMember = SeedEligibleMember();
        _service.Join(motion.Id, leavingMember, now);
        var lastMember = SeedEligibleMember();

        using var dbJoin = Worker.CreateDbContext();
        using var dbLeave = Worker.CreateDbContext();
        var joinService = NewServiceOn(dbJoin);
        var leaveService = NewServiceOn(dbLeave);

        var joinTask = Task.Run(() => joinService.Join(motion.Id, lastMember, now));
        var leaveTask = Task.Run(() => leaveService.Leave(motion.Id, leavingMember, now));
        await Task.WhenAll(joinTask, leaveTask);

        var reloaded = _motionRepo.Get(motion.Id)!;
        var submitterCount = _motionRepo.GetSubmitters(motion.Id).Count;
        await Assert.That(joinTask.Result.Succeeded).IsTrue();
        if (reloaded.Status == AssemblyMotionStatus.Submitted) {
            await Assert.That(submitterCount).IsEqualTo(3);
            await Assert.That(leaveTask.Result.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        } else {
            await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
            await Assert.That(submitterCount).IsEqualTo(2);
            await Assert.That(leaveTask.Result.Succeeded).IsTrue();
        }
    }

    [Test]
    public async Task UpdateByAuthor_after_concurrent_withdraw_is_locked() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var staleMotion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        using var dbReleaser = Worker.CreateDbContext();
        using var dbEditor = Worker.CreateDbContext();
        var withdrawOutcome = NewServiceOn(dbReleaser).Withdraw(staleMotion.Id, author, now);
        var updateOutcome = NewServiceOn(dbEditor).UpdateByAuthor(staleMotion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Spät" }, baseRevision: 1, now);

        await Assert.That(withdrawOutcome.Succeeded).IsTrue();
        await Assert.That(updateOutcome.Succeeded).IsFalse();
        await Assert.That(updateOutcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);

        var reloaded = _motionRepo.Get(staleMotion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
        await Assert.That(reloaded.Title).IsEqualTo("Testantrag");
    }

    [Test]
    public async Task UpdateByAuthor_writes_revision_2_and_updates_author_joined_revision() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Neu" }, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Revision).IsEqualTo(2);
        await Assert.That(reloaded.Title).IsEqualTo("Neu");
        var revisions = _motionRepo.GetRevisions(motion.Id);
        await Assert.That(revisions.Count).IsEqualTo(2);
        await Assert.That(revisions.Last().Title).IsEqualTo("Neu");
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Single(x => x.IsAuthor).JoinedRevision).IsEqualTo(2);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "Updated")).IsEqualTo(1);
    }

    [Test]
    public async Task Nfd_input_is_stored_as_nfc_and_an_unchanged_nfd_resave_creates_no_revision() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var nfd = NewDraft(type.Id) with { Title = "Antra\u0308ge", TextMarkdown = "Pru\u0308fung", Keywords = "Wa\u0308hler" };

        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), nfd, now).Motion!;

        var stored = _motionRepo.Get(motion.Id)!;
        await Assert.That(stored.Title).IsEqualTo("Antr\u00E4ge");
        await Assert.That(stored.TextMarkdown).IsEqualTo("Pr\u00FCfung");
        await Assert.That(stored.Keywords).IsEqualTo("W\u00E4hler");

        var outcome = _service.UpdateByAuthor(stored, author, Guid.NewGuid(), nfd, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
        await Assert.That(_motionRepo.GetRevisions(motion.Id).Count).IsEqualTo(1);
    }

    private AssemblyMotion SeedReleased(AssemblyMotionPortal portal, AssemblyMotionType type, Member author, DateTime now) {
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        return _service.Release(motion.Id, author, now).Motion!;
    }

    [Test]
    public async Task Edit_while_collecting_removes_cosubmitters() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _service.Join(motion.Id, SeedEligibleMember(), now);
        _service.Join(motion.Id, SeedEligibleMember(), now);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Neu" }, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(reloaded.Revision).IsEqualTo(2);
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Count).IsEqualTo(1);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "CoSubmittersReset")).IsEqualTo(1);
    }

    [Test]
    public async Task Edit_submitted_five_type_returns_to_collecting_keeps_number() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        for (var i = 0; i < 4; i++)
            _service.Join(motion.Id, SeedEligibleMember(), now);
        var submitted = _motionRepo.Get(motion.Id)!;

        var outcome = _service.UpdateByAuthor(submitted, author, Guid.NewGuid(), NewDraft(type.Id) with { TextMarkdown = "Neuer Text" }, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(submitted.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(reloaded.Number).IsEqualTo(1);
        await Assert.That(reloaded.SubmittedAt).IsNull();
        await Assert.That(reloaded.IsInRevision).IsTrue();
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Edit_submitted_one_type_stays_submitted_and_queues_wiki() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _context.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();
        var editTime = now.AddMinutes(5);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Neu" }, baseRevision: 1, editTime);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(reloaded.Number).IsEqualTo(1);
        await Assert.That(reloaded.Revision).IsEqualTo(2);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(reloaded.SubmittedAt).IsNotNull();
    }

    [Test]
    public async Task Edit_after_deadline_locked() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Spät" }, baseRevision: 1, now.AddDays(8));

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task Edit_between_type_deadline_and_close_locked() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _context.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.Deadline, now.AddDays(1))
            .Update();
        var lockedType = _portalRepo.GetType(type.Id)!;
        var editTime = now.AddDays(2);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Spät" }, baseRevision: 1, editTime);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
        await Assert.That(_service.CanEditAsAuthor(motion, lockedType, portal, author.Id, editTime)).IsFalse();
        await Assert.That(_service.CanEditAsAuthor(motion, lockedType, portal, author.Id, now.AddHours(1))).IsTrue();
    }

    [Test]
    public async Task Edit_between_competing_deadline_and_type_deadline_locked() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _context.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.Deadline, now.AddDays(5))
            .Set(t => t.CompetingDeadline, now.AddDays(1))
            .Update();
        _context.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.IsCompetingMotion, true)
            .Update();

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Spät" }, baseRevision: 1, now.AddDays(2));

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
    }

    [Test]
    public async Task Type_change_to_single_submitter_type_while_collecting_submits() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var fiveType = TypeWithMinSubmitters(portal.Id, 5);
        var oneType = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, fiveType, author, now);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(oneType.Id), baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.TypeId).IsEqualTo(oneType.Id);
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(reloaded.Number).IsEqualTo(1);
        await Assert.That(reloaded.SubmittedAt).IsNotNull();
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Edit_numbered_collecting_motion_keeps_its_number() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        for (var i = 0; i < 4; i++)
            _service.Join(motion.Id, SeedEligibleMember(), now);
        var submitted = _motionRepo.Get(motion.Id)!;
        _service.UpdateByAuthor(submitted, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Eins" }, baseRevision: 1, now);

        var outcome = _service.UpdateByAuthor(submitted, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Zwei" }, baseRevision: 2, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(reloaded.Number).IsEqualTo(1);
    }

    [Test]
    public async Task Edit_admitted_locked() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _context.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Status, AssemblyMotionStatus.Admitted)
            .Update();

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id) with { Title = "Neu" }, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.EditLocked);
    }

    [Test]
    public async Task Type_change_refused_after_number() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var otherType = _portalRepo.GetTypes(portal.Id).First(t => t.Id != type.Id);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(otherType.Id), baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(_motionRepo.Get(motion.Id)!.TypeId).IsEqualTo(type.Id);
    }

    [Test]
    public async Task Noop_save_creates_no_revision() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), NewDraft(type.Id), baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
        await Assert.That(_motionRepo.GetRevisions(motion.Id).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Noop_save_with_empty_strings_for_null_fields_creates_no_revision() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = SeedReleased(portal, type, author, now);
        _service.Join(motion.Id, SeedEligibleMember(), now);
        var draft = NewDraft(type.Id) with { Section = "", Summary = "  ", Keywords = "", PriorDiscussion = " " };

        var outcome = _service.UpdateByAuthor(motion, author, Guid.NewGuid(), draft, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(motion.Id)!.Revision).IsEqualTo(1);
        await Assert.That(_motionRepo.GetRevisions(motion.Id).Count).IsEqualTo(1);
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Count).IsEqualTo(2);
    }

    [Test]
    public async Task Noop_save_with_crlf_text_creates_no_revision() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var created = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id) with { TextMarkdown = "a\r\nb\r\n" }, now).Motion!;

        var outcome = _service.UpdateByAuthor(created, author, Guid.NewGuid(), NewDraft(type.Id) with { TextMarkdown = "a\r\nb" }, baseRevision: 1, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(created.Id)!.TextMarkdown).IsEqualTo("a\nb");
        await Assert.That(_motionRepo.GetRevisions(created.Id).Count).IsEqualTo(1);
    }

    [Test]
    public async Task Leave_removes_co_submitter() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);
        var coSubmitter = SeedEligibleMember();
        _service.Join(motion.Id, coSubmitter, now);

        var outcome = _service.Leave(motion.Id, coSubmitter, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        await Assert.That(_motionRepo.GetSubmitters(motion.Id).Count).IsEqualTo(1);
        await Assert.That(AuditLog.GetForEntity("AssemblyMotion", motion.Id).Count(a => a.Action == "CoSubmitterLeft")).IsEqualTo(1);
    }

    [Test]
    public async Task Withdraw_submitted_motion_marks_wiki_pending() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var outcome = _service.Withdraw(motion.Id, author, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
        await Assert.That(reloaded.WithdrawnAt).IsNotNull();
        await Assert.That(reloaded.Number).IsEqualTo(1);
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Withdraw_unnumbered_motion_does_not_mark_wiki_pending() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 5);
        var author = SeedEligibleMember();
        var motion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(motion.Id, author, now);

        var outcome = _service.Withdraw(motion.Id, author, now);

        await Assert.That(outcome.Succeeded).IsTrue();
        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
        await Assert.That(reloaded.Number).IsNull();
        await Assert.That(reloaded.WikiSyncState).IsEqualTo(WikiSyncState.NotMirrored);
    }

    [Test]
    public async Task DeleteDraft_soft_deletes_draft_only() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();
        var draftMotion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        var releasedMotion = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        _service.Release(releasedMotion.Id, author, now);

        var deleted = _service.DeleteDraft(draftMotion.Id, author, now);
        var refused = _service.DeleteDraft(releasedMotion.Id, author, now);

        await Assert.That(deleted.Succeeded).IsTrue();
        await Assert.That(_motionRepo.Get(draftMotion.Id)).IsNull();
        await Assert.That(refused.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(_motionRepo.Get(releasedMotion.Id)).IsNotNull();
    }

    [Test]
    public async Task Concurrent_submissions_get_distinct_numbers() {
        var now = DateTime.UtcNow;
        var portal = SeedPortal(now.AddHours(-1), now.AddDays(7));
        var type = TypeWithMinSubmitters(portal.Id, 1);
        var author = SeedEligibleMember();

        var motion1 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;
        var motion2 = _service.CreateDraft(portal, author, Guid.NewGuid(), NewDraft(type.Id), now).Motion!;

        using var db1 = Worker.CreateDbContext();
        using var db2 = Worker.CreateDbContext();
        var service1 = NewServiceOn(db1);
        var service2 = NewServiceOn(db2);

        var task1 = Task.Run(() => service1.Release(motion1.Id, author, now));
        var task2 = Task.Run(() => service2.Release(motion2.Id, author, now));
        var results = await Task.WhenAll(task1, task2);

        await Assert.That(results.All(r => r.Succeeded)).IsTrue();

        var numbers = results.Select(r => r.Motion!.Number!.Value).OrderBy(n => n).ToArray();
        await Assert.That(numbers).IsEquivalentTo([1, 2]);
    }
}
