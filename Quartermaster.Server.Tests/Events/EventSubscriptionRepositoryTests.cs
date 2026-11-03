using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Events;

public class EventSubscriptionRepositoryTests : RepositoryTestBase {
    private EventSubscriptionRepository _repo = default!;
    private TestDataBuilder _builder = default!;
    private Guid _chapterId;

    [Before(Test)]
    public void Setup() {
        _repo = new EventSubscriptionRepository(Db);
        _builder = new TestDataBuilder(Db);
        _chapterId = _builder.SeedChapter().Id;
    }

    [Test]
    public async Task Subscribe_is_idempotent_and_keeps_created_at() {
        var ev = _builder.SeedEvent(_chapterId);
        var user = _builder.SeedUser();
        var first = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

        var created = _repo.Subscribe(ev.Id, user.Id, first);
        var again = _repo.Subscribe(ev.Id, user.Id, first.AddHours(5));

        await Assert.That(again.Id).IsEqualTo(created.Id);
        await Assert.That(again.CreatedAt).IsEqualTo(first);
        await Assert.That(Db.EventSubscriptions.Count(s => s.EventId == ev.Id)).IsEqualTo(1);
        await Assert.That(_repo.IsSubscribed(ev.Id, user.Id)).IsTrue();
    }

    [Test]
    public async Task Unsubscribe_removes_row_and_deliveries() {
        var ev = _builder.SeedEvent(_chapterId);
        var user = _builder.SeedUser();
        var now = DateTime.UtcNow;
        var subscription = _repo.Subscribe(ev.Id, user.Id, now);
        _repo.TryRecordDelivery(subscription.Id, EventReminderKind.StartSoon, now.AddDays(1), now);

        _repo.Unsubscribe(ev.Id, user.Id);

        await Assert.That(_repo.IsSubscribed(ev.Id, user.Id)).IsFalse();
        await Assert.That(Db.EventReminderDeliveries.Count(d => d.SubscriptionId == subscription.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Unsubscribe_without_row_is_noop() {
        var ev = _builder.SeedEvent(_chapterId);
        var user = _builder.SeedUser();

        _repo.Unsubscribe(ev.Id, user.Id);

        await Assert.That(_repo.IsSubscribed(ev.Id, user.Id)).IsFalse();
    }

    [Test]
    public async Task ListActive_skips_completed_archived_and_deleted_events() {
        var user = _builder.SeedUser();
        var now = DateTime.UtcNow;
        var active = _builder.SeedEvent(_chapterId, status: EventStatus.Active);
        var draft = _builder.SeedEvent(_chapterId, status: EventStatus.Draft);
        var completed = _builder.SeedEvent(_chapterId, status: EventStatus.Completed);
        var archived = _builder.SeedEvent(_chapterId, status: EventStatus.Archived);
        var deleted = _builder.SeedEvent(_chapterId, status: EventStatus.Active);
        Db.Events.Where(e => e.Id == deleted.Id).Set(e => e.DeletedAt, now).Update();
        foreach (var ev in new[] { active, draft, completed, archived, deleted })
            _repo.Subscribe(ev.Id, user.Id, now);

        var eventIds = _repo.ListActive().Select(s => s.EventId).ToHashSet();

        await Assert.That(eventIds.SetEquals([active.Id, draft.Id])).IsTrue();
    }

    [Test]
    public async Task TryRecordDelivery_true_once_per_target() {
        var ev = _builder.SeedEvent(_chapterId);
        var user = _builder.SeedUser();
        var now = DateTime.UtcNow;
        var subscription = _repo.Subscribe(ev.Id, user.Id, now);
        var target = new DateTime(2026, 11, 28, 18, 0, 0, DateTimeKind.Utc);

        var first = _repo.TryRecordDelivery(subscription.Id, EventReminderKind.StartDayBefore, target, now);
        var second = _repo.TryRecordDelivery(subscription.Id, EventReminderKind.StartDayBefore, target, now);
        var moved = _repo.TryRecordDelivery(subscription.Id, EventReminderKind.StartDayBefore, target.AddHours(2), now);

        await Assert.That(first).IsTrue();
        await Assert.That(second).IsFalse();
        await Assert.That(moved).IsTrue();
    }

    [Test]
    public async Task Lists_for_user_and_event() {
        var ev = _builder.SeedEvent(_chapterId);
        var other = _builder.SeedEvent(_chapterId);
        var user = _builder.SeedUser();
        var otherUser = _builder.SeedUser();
        var now = DateTime.UtcNow;
        _repo.Subscribe(ev.Id, user.Id, now);
        _repo.Subscribe(other.Id, user.Id, now);
        _repo.Subscribe(ev.Id, otherUser.Id, now);

        await Assert.That(_repo.ListForUser(user.Id).Count).IsEqualTo(2);
        await Assert.That(_repo.ListForEvent(ev.Id).Count).IsEqualTo(2);
    }
}
