using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Dashboard;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Dashboard;

public class DashboardUpcomingEventsTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_sees_only_public_events() {
        var chapter = Builder.SeedChapter("C");
        var publicEvent = SeedDated(chapter.Id, EventVisibility.Public, 5);
        SeedDated(chapter.Id, EventVisibility.MembersOnly, 6);
        SeedDated(chapter.Id, EventVisibility.Private, 7);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { publicEvent.Id });
    }

    [Test]
    public async Task Member_sees_public_and_members_only() {
        var chapter = Builder.SeedChapter("C");
        var publicEvent = SeedDated(chapter.Id, EventVisibility.Public, 5);
        var membersEvent = SeedDated(chapter.Id, EventVisibility.MembersOnly, 6);
        SeedDated(chapter.Id, EventVisibility.Private, 7);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { publicEvent.Id, membersEvent.Id });
    }

    [Test]
    public async Task Viewer_with_permission_sees_private() {
        var chapter = Builder.SeedChapter("C");
        SeedDated(chapter.Id, EventVisibility.Public, 5);
        var privateEvent = SeedDated(chapter.Id, EventVisibility.Private, 7);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewEvents });
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id)).Contains(privateEvent.Id);
        await Assert.That(rows.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Private_event_of_another_chapter_is_left_out_for_chapter_scoped_viewer() {
        var own = Builder.SeedChapter("Own");
        var other = Builder.SeedChapter("Other");
        var ownPrivate = SeedDated(own.Id, EventVisibility.Private, 5);
        SeedDated(other.Id, EventVisibility.Private, 6);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [own.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { ownPrivate.Id });
    }

    [Test]
    public async Task Past_events_are_left_out() {
        var chapter = Builder.SeedChapter("C");
        SeedDated(chapter.Id, EventVisibility.Public, -3);
        var upcoming = SeedDated(chapter.Id, EventVisibility.Public, 3);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { upcoming.Id });
    }

    [Test]
    public async Task Undated_event_without_portal_is_left_out() {
        var chapter = Builder.SeedChapter("C");
        SeedUndated(chapter.Id, EventVisibility.Public, withPortal: false);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Undated_event_with_portal_is_listed_after_dated_ones() {
        var chapter = Builder.SeedChapter("C");
        var olderUndated = SeedUndated(chapter.Id, EventVisibility.Public, withPortal: true, createdDaysAgo: 5);
        var newerUndated = SeedUndated(chapter.Id, EventVisibility.Public, withPortal: true, createdDaysAgo: 1);
        var late = SeedDated(chapter.Id, EventVisibility.Public, 30);
        var soon = SeedDated(chapter.Id, EventVisibility.Public, 2);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        var expected = new[] { soon.Id, late.Id, newerUndated.Id, olderUndated.Id };
        await Assert.That(rows.Select(r => r.Id).SequenceEqual(expected)).IsTrue();
        await Assert.That(rows[2].EventDate).IsNull();
    }

    [Test]
    public async Task Undated_private_event_with_portal_hidden_from_member() {
        var chapter = Builder.SeedChapter("C");
        SeedUndated(chapter.Id, EventVisibility.Private, withPortal: true);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Has_motion_portal_is_set_for_events_with_a_portal() {
        var chapter = Builder.SeedChapter("C");
        var withPortal = SeedDated(chapter.Id, EventVisibility.Public, 5);
        var withoutPortal = SeedDated(chapter.Id, EventVisibility.Public, 6);
        SeedPortal(withPortal.Id);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Single(r => r.Id == withPortal.Id).HasMotionPortal).IsTrue();
        await Assert.That(rows.Single(r => r.Id == withoutPortal.Id).HasMotionPortal).IsFalse();
    }

    [Test]
    public async Task Portal_of_a_hidden_event_is_not_observable() {
        var chapter = Builder.SeedChapter("C");
        var hidden = SeedDated(chapter.Id, EventVisibility.MembersOnly, 5);
        SeedPortal(hidden.Id);
        var visible = SeedDated(chapter.Id, EventVisibility.Public, 6);
        using var client = AnonymousClient();

        var json = await (await client.GetAsync("/api/dashboard")).Content.ReadAsStringAsync();
        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { visible.Id });
        await Assert.That(rows[0].HasMotionPortal).IsFalse();
        await Assert.That(json).DoesNotContain(hidden.Id.ToString());
    }

    [Test]
    public async Task Row_has_no_fields_beyond_the_event_summary() {
        var names = typeof(DashboardEventDTO)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        await Assert.That(names).IsEquivalentTo(new[] {
            nameof(DashboardEventDTO.Id),
            nameof(DashboardEventDTO.PublicName),
            nameof(DashboardEventDTO.ChapterName),
            nameof(DashboardEventDTO.EventDate),
            nameof(DashboardEventDTO.HasMotionPortal)
        });
    }

    [Test]
    public async Task Event_detail_endpoint_returns_each_listed_event_for_the_same_viewer() {
        var own = Builder.SeedChapter("Own");
        var other = Builder.SeedChapter("Other");
        SeedDated(own.Id, EventVisibility.Public, 1);
        SeedDated(own.Id, EventVisibility.MembersOnly, 2);
        SeedDated(own.Id, EventVisibility.Private, 3);
        SeedDated(other.Id, EventVisibility.Private, 4);
        SeedUndated(own.Id, EventVisibility.Private, withPortal: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [own.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Count).IsEqualTo(4);
        foreach (var row in rows) {
            var response = await client.GetAsync($"/api/events/{row.Id}");
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task Undated_private_portal_event_of_another_chapter_is_left_out_for_chapter_scoped_viewer() {
        var own = Builder.SeedChapter("Own");
        var other = Builder.SeedChapter("Other");
        var ownPrivate = SeedUndated(own.Id, EventVisibility.Private, withPortal: true);
        var otherPrivate = SeedUndated(other.Id, EventVisibility.Private, withPortal: true);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [own.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);

        var json = await (await client.GetAsync("/api/dashboard")).Content.ReadAsStringAsync();
        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).ToList()).IsEquivalentTo(new[] { ownPrivate.Id });
        await Assert.That(json).DoesNotContain(otherPrivate.Id.ToString());
    }

    [Test]
    public async Task Dated_private_event_of_another_chapter_is_absent_from_the_raw_response() {
        var own = Builder.SeedChapter("Own");
        var other = Builder.SeedChapter("Other");
        var otherPrivate = SeedDated(other.Id, EventVisibility.Private, 4);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [own.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);

        var json = await (await client.GetAsync("/api/dashboard")).Content.ReadAsStringAsync();

        await Assert.That(json).DoesNotContain(otherPrivate.Id.ToString());
    }

    [Test]
    public async Task Limit_is_applied_after_the_visibility_filter() {
        var own = Builder.SeedChapter("Own");
        var other = Builder.SeedChapter("Other");
        for (var i = 1; i <= 10; i++)
            SeedDated(other.Id, EventVisibility.Private, i);
        var visible = Enumerable.Range(11, 3)
            .Select(i => SeedDated(own.Id, EventVisibility.Public, i))
            .ToList();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [own.Id] = new[] { PermissionIdentifier.ViewEvents } });
        using var client = AuthenticatedClient(token);

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Select(r => r.Id).SequenceEqual(visible.Select(e => e.Id))).IsTrue();
    }

    [Test]
    public async Task Row_count_never_exceeds_ten_and_dated_events_win_over_undated() {
        var chapter = Builder.SeedChapter("C");
        var dated = Enumerable.Range(1, 12)
            .Select(i => SeedDated(chapter.Id, EventVisibility.Public, i))
            .ToList();
        var undated = SeedUndated(chapter.Id, EventVisibility.Public, withPortal: true);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Count).IsEqualTo(10);
        await Assert.That(rows.Select(r => r.Id).SequenceEqual(dated.Take(10).Select(e => e.Id))).IsTrue();
        await Assert.That(rows.Any(r => r.Id == undated.Id)).IsFalse();
    }

    [Test]
    public async Task Undated_event_takes_the_last_free_slot() {
        var chapter = Builder.SeedChapter("C");
        var dated = Enumerable.Range(1, 9)
            .Select(i => SeedDated(chapter.Id, EventVisibility.Public, i))
            .ToList();
        var undated = SeedUndated(chapter.Id, EventVisibility.Public, withPortal: true);
        SeedUndated(chapter.Id, EventVisibility.Public, withPortal: true, createdDaysAgo: 3);
        using var client = AnonymousClient();

        var rows = await GetUpcoming(client);

        await Assert.That(rows.Count).IsEqualTo(10);
        await Assert.That(rows[9].Id).IsEqualTo(undated.Id);
    }

    private Event SeedDated(Guid chapterId, EventVisibility visibility, int daysFromNow)
        => Builder.SeedEvent(chapterId, publicName: $"E{daysFromNow}", status: EventStatus.Active,
            visibility: visibility, eventDate: DateTime.UtcNow.AddDays(daysFromNow));

    private Event SeedUndated(Guid chapterId, EventVisibility visibility, bool withPortal, int createdDaysAgo = 0) {
        var ev = Builder.SeedEvent(chapterId, publicName: "Undated", status: EventStatus.Active, visibility: visibility);
        Db.Events
            .Where(e => e.Id == ev.Id)
            .Set(e => e.CreatedAt, DateTime.UtcNow.AddDays(-createdDaysAgo))
            .Update();
        if (withPortal)
            SeedPortal(ev.Id);
        return ev;
    }

    private void SeedPortal(Guid eventId)
        => Db.Insert(new AssemblyMotionPortal {
            EventId = eventId,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(20),
            CreatedAt = DateTime.UtcNow
        });

    private static async Task<List<DashboardEventDTO>> GetUpcoming(HttpClient client) {
        var dto = await client.GetFromJsonAsync<DashboardDTO>("/api/dashboard");
        return dto!.UpcomingEvents!;
    }
}
