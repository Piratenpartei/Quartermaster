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
using Quartermaster.Data.Members;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionUpvoteServiceTests : RepositoryTestBase {
    private readonly Guid _userId = Guid.NewGuid();
    private DateTime _now = DateTime.UtcNow;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;
    private AssemblyMotionUpvoteRepository _upvoteRepo = default!;
    private AssemblyMotionService _service = default!;
    private AssemblyMotionUpvoteService _upvotes = default!;
    private Chapter _chapter = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        _motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        _upvoteRepo = new AssemblyMotionUpvoteRepository(Db);
        _service = new AssemblyMotionService(_motionRepo, _portalRepo, AuditLog, Db,
            new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance));
        _upvotes = new AssemblyMotionUpvoteService(_motionRepo, _portalRepo, _upvoteRepo);
        _chapter = _builder.SeedChapter();
        var ev = _builder.SeedEvent(_chapter.Id);
        _portal = _portalRepo.Create(ev.Id, _now.AddDays(-5), _now.AddDays(7), _now);
        _portal.PrioritizationEnabled = true;
        _portal.PrioritizationStartsAt = _now.AddHours(-1);
        _portal.PrioritizationEndsAt = _now.AddDays(2);
        _portalRepo.Update(_portal);
    }

    private AssemblyMotion NewMotion(bool release, int minSubmitters = 1) {
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == minSubmitters);
        var author = _builder.SeedMember(chapterId: _chapter.Id);
        var draft = new AssemblyMotionDraft(type.Id, "Testantrag", null, "Antragstext", "Begründung", null, null, null);
        var motion = _service.CreateDraft(_portal, author, _userId, draft, _now).Motion!;
        return release ? _service.Release(motion.Id, author, _now).Motion! : motion;
    }

    private int RowCount(Guid motionId) => _upvoteRepo.CountForMotion(motionId);

    [Test]
    public async Task Upvote_outside_window_refused() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);

        var before = _upvotes.Upvote(motion.Id, member, _now.AddHours(-2));
        var after = _upvotes.Upvote(motion.Id, member, _now.AddDays(3));
        var inside = _upvotes.Upvote(motion.Id, member, _now);

        await Assert.That(before.Succeeded).IsFalse();
        await Assert.That(before.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);
        await Assert.That(after.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);
        await Assert.That(inside.Succeeded).IsTrue();
        await Assert.That(RowCount(motion.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Upvote_when_prioritization_disabled_refused() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);
        _portal.PrioritizationEnabled = false;
        _portalRepo.Update(_portal);

        var outcome = _upvotes.Upvote(motion.Id, member, _now);

        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);
    }

    [Test]
    public async Task Upvote_twice_is_noop() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);

        var first = _upvotes.Upvote(motion.Id, member, _now);
        var second = _upvotes.Upvote(motion.Id, member, _now);

        await Assert.That(first.Succeeded).IsTrue();
        await Assert.That(second.Succeeded).IsTrue();
        await Assert.That(RowCount(motion.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Upvote_on_collecting_motion_refused() {
        var motion = NewMotion(true, minSubmitters: 5);
        var member = _builder.SeedMember(chapterId: _chapter.Id);

        var outcome = _upvotes.Upvote(motion.Id, member, _now);

        await Assert.That(motion.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(outcome.Succeeded).IsFalse();
        await Assert.That(outcome.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.InvalidState);
        await Assert.That(RowCount(motion.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Upvote_on_unknown_motion_is_not_found() {
        var member = _builder.SeedMember(chapterId: _chapter.Id);

        var outcome = _upvotes.Upvote(Guid.NewGuid(), member, _now);

        await Assert.That(outcome.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Remove_deletes_own_upvote_only_inside_window() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);
        var other = _builder.SeedMember(chapterId: _chapter.Id);
        _upvotes.Upvote(motion.Id, member, _now);
        _upvotes.Upvote(motion.Id, other, _now);

        var late = _upvotes.Remove(motion.Id, member, _now.AddDays(3));
        var removed = _upvotes.Remove(motion.Id, member, _now);
        var again = _upvotes.Remove(motion.Id, member, _now);

        await Assert.That(late.ErrorKey).IsEqualTo(I18nKey.Error.AssemblyMotion.PrioritizationNotOpen);
        await Assert.That(removed.Succeeded).IsTrue();
        await Assert.That(again.Succeeded).IsTrue();
        await Assert.That(_upvoteRepo.Exists(motion.Id, member.Id)).IsFalse();
        await Assert.That(_upvoteRepo.Exists(motion.Id, other.Id)).IsTrue();
    }

    [Test]
    public async Task Upvotes_are_not_audited() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);
        var auditBefore = Db.AuditLogs.Count();

        _upvotes.Upvote(motion.Id, member, _now);
        _upvotes.Remove(motion.Id, member, _now);

        await Assert.That(Db.AuditLogs.Count()).IsEqualTo(auditBefore);
    }

    [Test]
    public async Task Counts_for_portal_and_member_ids() {
        var first = NewMotion(true);
        var second = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);
        var other = _builder.SeedMember(chapterId: _chapter.Id);
        _upvotes.Upvote(first.Id, member, _now);
        _upvotes.Upvote(first.Id, other, _now);
        _upvotes.Upvote(second.Id, member, _now);

        var counts = _upvoteRepo.CountsForPortal(_portal.Id);
        var mine = _upvoteRepo.MotionIdsForMember(_portal.Id, member.Id);

        await Assert.That(counts[first.Id]).IsEqualTo(2);
        await Assert.That(counts[second.Id]).IsEqualTo(1);
        await Assert.That(mine).IsEquivalentTo(new[] { first.Id, second.Id });
        await Assert.That(_upvoteRepo.MotionIdsForMember(_portal.Id, Guid.NewGuid())).IsEmpty();
    }

    [Test]
    public async Task Concurrent_upvotes_create_one_row() {
        var motion = NewMotion(true);
        var member = _builder.SeedMember(chapterId: _chapter.Id);

        using var db1 = Worker.CreateDbContext();
        using var db2 = Worker.CreateDbContext();
        var service1 = NewServiceOn(db1);
        var service2 = NewServiceOn(db2);

        var task1 = Task.Run(() => service1.Upvote(motion.Id, member, _now));
        var task2 = Task.Run(() => service2.Upvote(motion.Id, member, _now));
        var results = await Task.WhenAll(task1, task2);

        await Assert.That(results.All(r => r.Succeeded)).IsTrue();
        await Assert.That(RowCount(motion.Id)).IsEqualTo(1);
    }

    private static AssemblyMotionUpvoteService NewServiceOn(DbContext context) {
        var auditLog = new AuditLogRepository(context);
        return new AssemblyMotionUpvoteService(new AssemblyMotionRepository(context, auditLog),
            new AssemblyMotionPortalRepository(context, auditLog), new AssemblyMotionUpvoteRepository(context));
    }
}
