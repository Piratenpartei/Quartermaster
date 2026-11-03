using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Members;
using Quartermaster.Data.Migrations;
using Quartermaster.Server.AssemblyMotions;
using Quartermaster.Server.AssemblyMotions.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionSubscriptionRepositoryTests : RepositoryTestBase {
    private TestDataBuilder _builder = default!;
    private AssemblyMotionSubscriptionRepository _repo = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionService _service = default!;
    private Chapter _chapter = default!;
    private AssemblyMotionPortal _portal = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _repo = new AssemblyMotionSubscriptionRepository(Db);
        _portalRepo = new AssemblyMotionPortalRepository(Db, AuditLog);
        var motionRepo = new AssemblyMotionRepository(Db, AuditLog);
        var notifier = new AssemblyMotionNotifier(new CapturingNotificationQueue(), Db, NullLogger<AssemblyMotionNotifier>.Instance);
        _service = new AssemblyMotionService(motionRepo, _portalRepo, AuditLog, Db, notifier, new AssemblyMotionSubscriptionRepository(Db));
        _chapter = _builder.SeedChapter();
        var ev = _builder.SeedEvent(_chapter.Id, status: EventStatus.Active);
        _portal = _portalRepo.Create(ev.Id, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
    }

    private (AssemblyMotion Motion, Member Author) SeedMotion() {
        var user = _builder.SeedUser();
        var author = _builder.SeedMember(chapterId: _chapter.Id, userId: user.Id);
        var type = _portalRepo.GetTypes(_portal.Id).First(t => t.MinSubmitters == 1);
        var draft = new AssemblyMotionDraft(type.Id, "Testantrag", null, "Antragstext", "Begründung", null, null, null);
        var motion = _service.CreateDraft(_portal, author, Guid.NewGuid(), draft, DateTime.UtcNow).Motion!;
        return (_service.Release(motion.Id, author, DateTime.UtcNow).Motion!, author);
    }

    private Member SeedSupporter(Guid motionId, DateTime joinedAt, bool withUser = true) {
        Guid? userId = withUser ? _builder.SeedUser().Id : null;
        var member = _builder.SeedMember(chapterId: _chapter.Id, userId: userId);
        Db.Insert(new AssemblyMotionSubmitter {
            MotionId = motionId,
            MemberId = member.Id,
            IsAuthor = false,
            JoinedAt = joinedAt,
            JoinedRevision = 1
        });
        return member;
    }

    [Test]
    public async Task Subscribe_is_idempotent_and_keeps_created_at() {
        var (motion, _) = SeedMotion();
        var user = _builder.SeedUser();
        var first = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        var created = _repo.Subscribe(motion.Id, user.Id, first);
        var again = _repo.Subscribe(motion.Id, user.Id, first.AddHours(5));

        await Assert.That(again.Id).IsEqualTo(created.Id);
        await Assert.That(again.CreatedAt).IsEqualTo(first);
        await Assert.That(Db.AssemblyMotionSubscriptions.Count(s => s.MotionId == motion.Id)).IsEqualTo(1);
        await Assert.That(_repo.IsSubscribed(motion.Id, user.Id)).IsTrue();
    }

    [Test]
    public async Task Unsubscribe_removes_the_row() {
        var (motion, _) = SeedMotion();
        var user = _builder.SeedUser();
        _repo.Subscribe(motion.Id, user.Id, DateTime.UtcNow);

        _repo.Unsubscribe(motion.Id, user.Id);

        await Assert.That(_repo.IsSubscribed(motion.Id, user.Id)).IsFalse();
    }

    [Test]
    public async Task Unsubscribe_without_row_is_noop() {
        var (motion, _) = SeedMotion();
        var user = _builder.SeedUser();

        _repo.Unsubscribe(motion.Id, user.Id);

        await Assert.That(_repo.IsSubscribed(motion.Id, user.Id)).IsFalse();
    }

    [Test]
    public async Task Lists_for_user_and_motion() {
        var (first, _) = SeedMotion();
        var (second, _) = SeedMotion();
        var user = _builder.SeedUser();
        var other = _builder.SeedUser();
        var now = DateTime.UtcNow;
        _repo.Subscribe(first.Id, user.Id, now);
        _repo.Subscribe(second.Id, user.Id, now);
        _repo.Subscribe(first.Id, other.Id, now);

        await Assert.That(_repo.ListForUser(user.Id).Count).IsEqualTo(2);
        await Assert.That(_repo.ListUserIdsForMotion(first.Id).ToHashSet().SetEquals([user.Id, other.Id])).IsTrue();
    }

    [Test]
    public async Task Backfill_subscribes_supporters_but_not_authors() {
        var (motion, author) = SeedMotion();
        var (deleted, _) = SeedMotion();
        var joinedAt = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var supporter = SeedSupporter(motion.Id, joinedAt);
        SeedSupporter(motion.Id, joinedAt, withUser: false);
        SeedSupporter(deleted.Id, joinedAt);
        Db.AssemblyMotions.Where(m => m.Id == deleted.Id).Set(m => m.DeletedAt, DateTime.UtcNow).Update();

        Db.Execute(M009.BackfillSubscriptionsSql);

        var rows = Db.AssemblyMotionSubscriptions.ToList();
        await Assert.That(rows.Count).IsEqualTo(1);
        await Assert.That(rows[0].MotionId).IsEqualTo(motion.Id);
        await Assert.That(rows[0].UserId).IsEqualTo(supporter.UserId!.Value);
        await Assert.That(rows[0].CreatedAt).IsEqualTo(joinedAt);
        await Assert.That(_repo.IsSubscribed(motion.Id, author.UserId!.Value)).IsFalse();
    }

    [Test]
    public async Task Backfill_skips_existing_rows() {
        var (motion, _) = SeedMotion();
        var supporter = SeedSupporter(motion.Id, DateTime.UtcNow);
        _repo.Subscribe(motion.Id, supporter.UserId!.Value, DateTime.UtcNow);

        Db.Execute(M009.BackfillSubscriptionsSql);
        Db.Execute(M009.BackfillSubscriptionsSql);

        await Assert.That(Db.AssemblyMotionSubscriptions.Count(s => s.MotionId == motion.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Backfill_subscribes_a_user_with_two_supporting_members_once() {
        var (motion, _) = SeedMotion();
        var user = _builder.SeedUser();
        var earlier = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        foreach (var joinedAt in new[] { earlier.AddDays(2), earlier }) {
            var member = _builder.SeedMember(chapterId: _chapter.Id, userId: user.Id);
            Db.Insert(new AssemblyMotionSubmitter {
                MotionId = motion.Id,
                MemberId = member.Id,
                IsAuthor = false,
                JoinedAt = joinedAt,
                JoinedRevision = 1
            });
        }

        Db.Execute(M009.BackfillSubscriptionsSql);

        var row = Db.AssemblyMotionSubscriptions.Single(s => s.MotionId == motion.Id);
        await Assert.That(row.UserId).IsEqualTo(user.Id);
        await Assert.That(row.CreatedAt).IsEqualTo(earlier);
    }
}
