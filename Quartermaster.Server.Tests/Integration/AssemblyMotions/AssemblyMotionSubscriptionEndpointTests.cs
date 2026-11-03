using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Events;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionSubscriptionEndpointTests : IntegrationTestBase {
    private Chapter _chapter = default!;
    private Event _event = default!;
    private AssemblyMotionPortal _portal = default!;
    private AssemblyMotionType _type = default!;

    private static string Path(Guid motionId) => $"/api/assemblymotions/motions/{motionId}/subscription";

    [Before(Test)]
    public void SeedPortal() {
        _chapter = Builder.SeedChapter("Chapter");
        _event = Builder.SeedEvent(_chapter.Id, publicName: "BPT", status: EventStatus.Active, visibility: EventVisibility.Public);
        _portal = new AssemblyMotionPortal {
            EventId = _event.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(_portal);
        _type = new AssemblyMotionType {
            PortalId = _portal.Id,
            Code = "SÄA",
            Name = "Satzungsänderungsantrag",
            MinSubmitters = 1
        };
        Db.Insert(_type);
    }

    private async Task<(Guid UserId, HttpClient Client)> MemberClientAsync() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        Builder.SeedMember(chapterId: _chapter.Id, userId: user.Id);
        return (user.Id, await AuthenticatedClientWithCsrfAsync(token));
    }

    private async Task<(Guid UserId, HttpClient Client)> UserClientAsync() {
        var (user, token) = Builder.SeedAuthenticatedUser();
        return (user.Id, await AuthenticatedClientWithCsrfAsync(token));
    }

    private async Task<Guid> CreateMotion(HttpClient author, bool release = true) {
        var response = await author.PostAsJsonAsync($"/api/assemblymotions/portals/{_portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = _type.Id,
            Title = "Antrag",
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        if (release)
            await author.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created.Id;
    }

    private static async Task<AssemblyMotionDetailDTO> Detail(HttpClient client, Guid motionId) {
        return (await client.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{motionId}"))!;
    }

    [Test]
    public async Task Anonymous_gets_401() {
        var (_, author) = await MemberClientAsync();
        using (author) {
            var motionId = await CreateMotion(author);
            using var client = AnonymousClient();
            await AttachAntiforgeryTokenAsync(client);

            await Assert.That((await client.PostAsJsonAsync(Path(motionId), new { })).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That((await client.DeleteAsync(Path(motionId))).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
            await Assert.That((await client.GetAsync("/api/users/assembly-motion-subscriptions")).StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
        }
    }

    [Test]
    public async Task Draft_motion_gets_404() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await MemberClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author, release: false);

            await Assert.That((await other.PostAsJsonAsync(Path(motionId), new { })).StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        }
    }

    [Test]
    public async Task Author_gets_409() {
        var (_, author) = await MemberClientAsync();
        using (author) {
            var motionId = await CreateMotion(author);

            var response = await author.PostAsJsonAsync(Path(motionId), new { });

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.assembly_motion.subscription_author");
            await Assert.That((await Detail(author, motionId)).CanSubscribe).IsFalse();
        }
    }

    [Test]
    public async Task Completed_event_gets_409() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await MemberClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);
            Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Status, EventStatus.Completed).Update();

            var response = await other.PostAsJsonAsync(Path(motionId), new { });

            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
            await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.assembly_motion.subscription_closed");
        }
    }

    [Test]
    public async Task Private_event_without_view_gets_403() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await UserClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);
            Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Visibility, EventVisibility.Private).Update();

            await Assert.That((await other.PostAsJsonAsync(Path(motionId), new { })).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }
    }

    [Test]
    public async Task Subscribe_is_idempotent() {
        var (_, author) = await MemberClientAsync();
        var (userId, other) = await MemberClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);

            var first = await other.PostAsJsonAsync(Path(motionId), new { });
            var second = await other.PostAsJsonAsync(Path(motionId), new { });

            await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That(Db.AssemblyMotionSubscriptions.Count(s => s.MotionId == motionId && s.UserId == userId)).IsEqualTo(1);
        }
    }

    [Test]
    public async Task Unsubscribe_of_deleted_motion_returns_204() {
        var (_, author) = await MemberClientAsync();
        var (userId, other) = await MemberClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);
            await other.PostAsJsonAsync(Path(motionId), new { });
            Db.AssemblyMotions.Where(m => m.Id == motionId).Set(m => m.DeletedAt, DateTime.UtcNow).Update();

            await Assert.That((await other.DeleteAsync(Path(motionId))).StatusCode).IsEqualTo(HttpStatusCode.NoContent);
            await Assert.That(Db.AssemblyMotionSubscriptions.Any(s => s.UserId == userId)).IsFalse();
        }
    }

    [Test]
    public async Task Detail_reports_subscription_state() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await MemberClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);

            var before = await Detail(other, motionId);
            await other.PostAsJsonAsync(Path(motionId), new { });
            var after = await Detail(other, motionId);

            await Assert.That(before.CanSubscribe).IsTrue();
            await Assert.That(before.IsSubscribed).IsFalse();
            await Assert.That(after.IsSubscribed).IsTrue();
        }
    }

    [Test]
    public async Task User_without_member_can_subscribe() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await UserClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);

            var before = await Detail(other, motionId);
            var response = await other.PostAsJsonAsync(Path(motionId), new { });

            await Assert.That(before.CanSubscribe).IsTrue();
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        }
    }

    [Test]
    public async Task List_hides_details_of_motions_no_longer_visible() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await UserClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author);
            await other.PostAsJsonAsync(Path(motionId), new { });

            var visible = (await other.GetFromJsonAsync<List<AssemblyMotionSubscriptionListItemDTO>>("/api/users/assembly-motion-subscriptions"))!.Single();
            Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Visibility, EventVisibility.Private).Update();
            var hidden = (await other.GetFromJsonAsync<List<AssemblyMotionSubscriptionListItemDTO>>("/api/users/assembly-motion-subscriptions"))!.Single();

            await Assert.That(visible.IsVisible).IsTrue();
            await Assert.That(visible.Title).IsEqualTo("Antrag");
            await Assert.That(visible.EventName).IsEqualTo("BPT");
            await Assert.That(hidden.MotionId).IsEqualTo(motionId);
            await Assert.That(hidden.IsVisible).IsFalse();
            await Assert.That(hidden.Title).IsEqualTo("");
            await Assert.That(hidden.EventName).IsEqualTo("");
            await Assert.That(hidden.Status).IsNull();
        }
    }

    [Test]
    public async Task Draft_motion_of_invisible_event_gets_403() {
        var (_, author) = await MemberClientAsync();
        var (_, other) = await UserClientAsync();
        using (author)
        using (other) {
            var motionId = await CreateMotion(author, release: false);
            Db.Events.Where(e => e.Id == _event.Id).Set(e => e.Visibility, EventVisibility.Private).Update();

            await Assert.That((await other.PostAsJsonAsync(Path(motionId), new { })).StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        }
    }

    [Test]
    public async Task Author_with_a_stray_subscription_is_not_shown_subscribed() {
        var (authorUserId, author) = await MemberClientAsync();
        using (author) {
            var motionId = await CreateMotion(author);
            Db.Insert(new AssemblyMotionSubscription { MotionId = motionId, UserId = authorUserId, CreatedAt = DateTime.UtcNow });

            var detail = await Detail(author, motionId);

            await Assert.That(detail.IsSubscribed).IsFalse();
            await Assert.That(detail.CanSubscribe).IsFalse();
        }
    }

    /// <summary>The eligibility check picks one of the user's members; the author check must look at all of them.</summary>
    [Test]
    public async Task User_whose_other_member_authored_cannot_subscribe() {
        var (_, author) = await MemberClientAsync();
        var (user, token) = Builder.SeedAuthenticatedUser();
        var first = Builder.SeedMember(chapterId: _chapter.Id, userId: user.Id);
        var second = Builder.SeedMember(chapterId: _chapter.Id, userId: user.Id);
        var authoring = string.CompareOrdinal(first.Id.ToString(), second.Id.ToString()) > 0 ? first : second;
        using (author)
        using (var viewer = await AuthenticatedClientWithCsrfAsync(token)) {
            var motionId = await CreateMotion(author);
            Db.AssemblyMotions.Where(m => m.Id == motionId).Set(m => m.CreatedByMemberId, authoring.Id).Update();

            var detail = await Detail(viewer, motionId);
            var response = await viewer.PostAsJsonAsync(Path(motionId), new { });

            await Assert.That(detail.CanSubscribe).IsFalse();
            await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        }
    }
}
