using System;
using System.Linq;
using System.Threading.Tasks;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Data.Roles;
using Quartermaster.Data.UserChapterPermissions;
using Quartermaster.Data.UserGlobalPermissions;
using Quartermaster.Server.Events;
using Quartermaster.Server.Events.Subscriptions;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Events;

public class EventReminderServiceTests : RepositoryTestBase {
    private static readonly DateTime Start = new(2026, 11, 28, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Deadline = new(2026, 11, 10, 22, 59, 0, DateTimeKind.Utc);

    private readonly CapturingNotificationQueue _queue = new();
    private TestDataBuilder _builder = default!;
    private EventSubscriptionRepository _subscriptions = default!;
    private EventReminderService _service = default!;
    private Chapter _chapter = default!;

    [Before(Test)]
    public void Setup() {
        _builder = new TestDataBuilder(Db);
        _subscriptions = new EventSubscriptionRepository(Db);
        var roles = new RoleRepository(Db);
        var visibility = new EventUserVisibility(
            new UserGlobalPermissionRepository(Db, roles),
            new UserChapterPermissionRepository(Db, roles),
            new ChapterRepository(Db, AuditLog));
        var notifier = new EventSubscriptionNotifier(_queue, NullLogger<EventSubscriptionNotifier>.Instance);
        _service = new EventReminderService(_subscriptions, Db, visibility, notifier, NullLogger<EventReminderService>.Instance);
        _chapter = _builder.SeedChapter();
    }

    private Event SeedEvent(EventVisibility visibility = EventVisibility.Public, EventStatus status = EventStatus.Active) {
        var ev = _builder.SeedEvent(_chapter.Id, publicName: "Bundesparteitag", visibility: visibility, status: status, eventDate: new DateTime(2026, 11, 28));
        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.StartTime, new TimeSpan(19, 0, 0)).Update();
        ev.StartTime = new TimeSpan(19, 0, 0);
        return ev;
    }

    private void SeedPortal(Event ev) {
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = Deadline.AddDays(-30),
            ClosesAt = Deadline.AddDays(5),
            CreatedAt = Deadline.AddDays(-30)
        };
        Db.Insert(portal);
        Db.Insert(new AssemblyMotionType { PortalId = portal.Id, Code = "SA", Name = "Satzungsänderungsantrag", SortOrder = 1, MinSubmitters = 1, Deadline = Deadline, WikiTemplate = "" });
    }

    private Guid Subscribe(Event ev, DateTime createdAt) {
        var user = _builder.SeedUser();
        _subscriptions.Subscribe(ev.Id, user.Id, createdAt);
        return user.Id;
    }

    private int Count(string triggerId) => _queue.Requests.Count(r => r.TriggerId == triggerId);

    private static EventReminderNotificationModel ModelOf(NotificationDispatchRequest request)
        => (EventReminderNotificationModel)request.ModelFactory(new NotificationRecipient(Guid.NewGuid(), "x@test.local"))["event"];

    [Test]
    public async Task Sends_deadline_reminder_36_hours_before() {
        var ev = SeedEvent();
        SeedPortal(ev);
        var userId = Subscribe(ev, Deadline.AddDays(-10));

        _service.SendDue(Deadline.AddHours(-36).AddMinutes(-1));
        await Assert.That(Count(NotificationTriggers.EventDeadlineReminder)).IsEqualTo(0);

        _service.SendDue(Deadline.AddHours(-36));

        var request = _queue.Requests.Single(r => r.TriggerId == NotificationTriggers.EventDeadlineReminder);
        var payload = (EventReminderNotificationPayload)request.Payload;
        var model = ModelOf(request);
        await Assert.That(payload.UserIds.Single()).IsEqualTo(userId);
        await Assert.That(model.TypeNames).IsEqualTo("Satzungsänderungsantrag");
        await Assert.That(model.Date).IsEqualTo("10.11.2026");
        await Assert.That(model.Time).IsEqualTo("23:59");
        await Assert.That(model.Path).IsEqualTo($"/Events/{ev.Id}/Motions");
    }

    [Test]
    public async Task Sends_each_reminder_once() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddHours(-23));
        _service.SendDue(Start.AddHours(-22));

        await Assert.That(Count(NotificationTriggers.EventReminder)).IsEqualTo(1);
        await Assert.That(ModelOf(_queue.Requests.Single()).Kind).IsEqualTo(EventReminderNotificationModel.KindDayBefore);
    }

    [Test]
    public async Task One_request_carries_all_subscribers() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));
        Subscribe(ev, Start.AddDays(-9));

        var sent = _service.SendDue(Start.AddMinutes(-30));

        var request = _queue.Requests.Single(r => r.TriggerId == NotificationTriggers.EventReminder);
        await Assert.That(sent).IsEqualTo(2);
        await Assert.That(((EventReminderNotificationPayload)request.Payload).UserIds.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Subscribing_late_skips_passed_reminder_only() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddHours(-10));

        _service.SendDue(Start.AddHours(-9));
        await Assert.That(Count(NotificationTriggers.EventReminder)).IsEqualTo(0);

        _service.SendDue(Start.AddMinutes(-29));

        var request = _queue.Requests.Single();
        var model = ModelOf(request);
        await Assert.That(model.Kind).IsEqualTo(EventReminderNotificationModel.KindSoon);
        await Assert.That(model.Time).IsEqualTo("19:00");
    }

    [Test]
    public async Task Moved_start_gets_new_reminder_once() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));
        _service.SendDue(Start.AddHours(-23));

        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.StartTime, new TimeSpan(21, 0, 0)).Update();
        _service.SendDue(Start.AddHours(-22));
        _service.SendDue(Start.AddHours(-21));

        await Assert.That(Count(NotificationTriggers.EventReminder)).IsEqualTo(2);
        await Assert.That(ModelOf(_queue.Requests[1]).Time).IsEqualTo("21:00");
    }

    [Test]
    public async Task Not_sent_after_target() {
        var ev = SeedEvent();
        SeedPortal(ev);
        Subscribe(ev, Deadline.AddDays(-10));

        _service.SendDue(Start.AddMinutes(1));

        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments(EventStatus.Completed)]
    [Arguments(EventStatus.Archived)]
    public async Task Completed_and_archived_events_send_nothing(EventStatus status) {
        var ev = SeedEvent(status: status);
        Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddMinutes(-30));

        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Deleted_event_sends_nothing() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));
        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.DeletedAt, Start.AddDays(-1)).Update();

        _service.SendDue(Start.AddMinutes(-30));

        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Invisible_event_sends_nothing_and_records_nothing() {
        var ev = SeedEvent(visibility: EventVisibility.Private);
        var userId = Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddHours(-23));

        await Assert.That(_queue.Requests.Count).IsEqualTo(0);
        await Assert.That(Db.EventReminderDeliveries.Count()).IsEqualTo(0);

        _builder.GrantChapterPermission(userId, _chapter.Id, PermissionIdentifier.ViewEvents);
        _service.SendDue(Start.AddHours(-22));

        await Assert.That(Count(NotificationTriggers.EventReminder)).IsEqualTo(1);
    }

    [Test]
    public async Task Meeting_link_only_in_the_soon_reminder() {
        var ev = SeedEvent();
        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.MeetingUrl, "https://meet.test/bpt").Update();
        Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddHours(-23));
        _service.SendDue(Start.AddMinutes(-29));

        await Assert.That(ModelOf(_queue.Requests[0]).MeetingUrl).IsEqualTo("");
        await Assert.That(ModelOf(_queue.Requests[1]).MeetingUrl).IsEqualTo("https://meet.test/bpt");
        await Assert.That(ModelOf(_queue.Requests[1]).Path).IsEqualTo($"/Events/{ev.Id}");
    }

    [Test]
    public async Task Reminder_model_has_no_member_names() {
        var ev = SeedEvent();
        var user = _builder.SeedUser(firstName: "Erika", lastName: "Musterfrau");
        _subscriptions.Subscribe(ev.Id, user.Id, Start.AddDays(-10));

        _service.SendDue(Start.AddHours(-23));

        var model = ModelOf(_queue.Requests.Single());
        var texts = string.Join("|", model.Name, model.NameEscaped, model.Date, model.Time, model.Kind, model.TypeNames, model.MeetingUrl, model.Path);
        await Assert.That(texts).DoesNotContain("Erika");
        await Assert.That(texts).DoesNotContain("Musterfrau");
    }

    [Test]
    public async Task Day_before_reminder_due_on_the_same_day_says_today() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddHours(-6));

        await Assert.That(ModelOf(_queue.Requests.Single()).Kind).IsEqualTo(EventReminderNotificationModel.KindToday);
    }

    [Test]
    public async Task Day_before_reminder_not_sent_inside_the_soon_window() {
        var ev = SeedEvent();
        Subscribe(ev, Start.AddDays(-10));

        _service.SendDue(Start.AddMinutes(-20));
        _service.SendDue(Start.AddMinutes(-10));

        var request = _queue.Requests.Single();
        await Assert.That(ModelOf(request).Kind).IsEqualTo(EventReminderNotificationModel.KindSoon);
    }
}
