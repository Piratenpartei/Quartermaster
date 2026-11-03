using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Events;
using Quartermaster.Data.Events;
using Quartermaster.Server.Notifications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Events;

public class EventScheduleChangeNoticeTests : IntegrationTestBase {
    private static readonly DateOnly InTwoWeeks = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));

    private Event SeedEvent(Guid chapterId, DateOnly? date, TimeOnly? startTime = null, EventVisibility visibility = EventVisibility.Public, EventStatus status = EventStatus.Active) {
        var ev = Builder.SeedEvent(chapterId, publicName: "Kreisparteitag", visibility: visibility, status: status, eventDate: date.ToStorage());
        Db.Events.Where(e => e.Id == ev.Id).Set(e => e.StartTime, startTime.ToStorage()).Update();
        return ev;
    }

    private Guid Subscribe(Event ev) {
        var user = Builder.SeedUser();
        Db.Insert(new EventSubscription { EventId = ev.Id, UserId = user.Id, CreatedAt = DateTime.UtcNow });
        return user.Id;
    }

    private async Task UpdateAsync(Event ev, Guid chapterId, DateOnly? date, TimeOnly? startTime, string publicName = "Kreisparteitag", EventVisibility visibility = EventVisibility.Public) {
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterId] = new[] { PermissionIdentifier.EditEvents } });
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.PutAsJsonAsync($"/api/events/{ev.Id}", new EventUpdateRequest {
            Id = ev.Id,
            InternalName = "KPT",
            PublicName = publicName,
            EventDate = date,
            StartTime = startTime,
            Visibility = visibility
        });
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    private int NoticesFor(Guid userId) => Db.NotificationLogs.Count(l => l.TriggerId == NotificationTriggers.EventReminder && l.RecipientUserId == userId);

    [Test]
    public async Task Date_change_notifies_subscribers() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0));
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, InTwoWeeks.AddDays(1), new TimeOnly(10, 0));

        var log = Db.NotificationLogs.Single(l => l.TriggerId == NotificationTriggers.EventReminder && l.RecipientUserId == userId);
        await Assert.That(log.Subject).IsEqualTo("Neuer Termin: Kreisparteitag");
        await Assert.That(log.Body!).Contains(InTwoWeeks.AddDays(1).ToString("dd.MM.yyyy") + ", 10:00 Uhr");
    }

    [Test]
    public async Task Start_time_change_notifies() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0));
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, InTwoWeeks, new TimeOnly(11, 0));

        await Assert.That(NoticesFor(userId)).IsEqualTo(1);
    }

    [Test]
    public async Task Removed_date_notifies_with_noch_offen() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0));
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, null, null);

        var log = Db.NotificationLogs.Single(l => l.TriggerId == NotificationTriggers.EventReminder && l.RecipientUserId == userId);
        await Assert.That(log.Body!).Contains("noch offen");
    }

    [Test]
    public async Task No_change_no_notice() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0));
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, InTwoWeeks, new TimeOnly(10, 0), publicName: "Kreisparteitag Celle");

        await Assert.That(NoticesFor(userId)).IsEqualTo(0);
    }

    [Test]
    public async Task Past_event_no_notice() {
        var chapter = Builder.SeedChapter("C");
        var lastWeek = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var ev = SeedEvent(chapter.Id, lastWeek, new TimeOnly(10, 0));
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, lastWeek.AddDays(-1), new TimeOnly(10, 0));

        await Assert.That(NoticesFor(userId)).IsEqualTo(0);
    }

    [Test]
    public async Task Completed_event_no_notice() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0), status: EventStatus.Completed);
        var userId = Subscribe(ev);

        await UpdateAsync(ev, chapter.Id, InTwoWeeks.AddDays(1), new TimeOnly(10, 0));

        await Assert.That(NoticesFor(userId)).IsEqualTo(0);
    }

    [Test]
    public async Task Unsubscribed_or_invisible_user_gets_nothing() {
        var chapter = Builder.SeedChapter("C");
        var ev = SeedEvent(chapter.Id, InTwoWeeks, new TimeOnly(10, 0), visibility: EventVisibility.Private);
        var subscriberWithoutAccess = Subscribe(ev);
        var bystander = Builder.SeedUser();

        await UpdateAsync(ev, chapter.Id, InTwoWeeks.AddDays(1), new TimeOnly(10, 0), visibility: EventVisibility.Private);

        await Assert.That(NoticesFor(subscriberWithoutAccess)).IsEqualTo(0);
        await Assert.That(NoticesFor(bystander.Id)).IsEqualTo(0);
    }
}
