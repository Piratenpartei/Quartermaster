using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionPortalEndpointTests : IntegrationTestBase {
    private static AssemblyMotionPortalEnableRequest EnableRequest(Guid eventId) => new() {
        EventId = eventId,
        OpensAt = DateTimeOffset.UtcNow.AddDays(1),
        ClosesAt = DateTimeOffset.UtcNow.AddDays(30)
    };

    private async Task<(Guid ChapterId, Guid EventId, HttpClient Client)> SeedEditorAsync(EventVisibility visibility = EventVisibility.Public) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, visibility: visibility);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        var client = await AuthenticatedClientWithCsrfAsync(token);
        return (chapter.Id, ev.Id, client);
    }

    private AssemblyMotionPortal SeedPortal(Guid eventId) {
        var portal = new AssemblyMotionPortal {
            EventId = eventId,
            OpensAt = DateTime.UtcNow.AddDays(1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(portal);
        return portal;
    }

    private AssemblyMotionType SeedType(Guid portalId, string code) {
        var type = new AssemblyMotionType {
            PortalId = portalId,
            Code = code,
            Name = "Art " + code,
            MinSubmitters = 1
        };
        Db.Insert(type);
        return type;
    }

    private AssemblyMotion SeedMotion(Guid portalId, Guid typeId) {
        var chapter = Builder.SeedChapter("Member chapter");
        var member = Builder.SeedMember(chapter.Id);
        var motion = new AssemblyMotion {
            PortalId = portalId,
            TypeId = typeId,
            Title = "Antrag",
            Status = AssemblyMotionStatus.Draft,
            CreatedByMemberId = member.Id,
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(motion);
        return motion;
    }

    [Test]
    public async Task Enable_requires_events_edit() {
        var chapter = Builder.SeedChapter("Chapter");
        var other = Builder.SeedChapter("Other");
        var ev = Builder.SeedEvent(chapter.Id);

        var (_, tokenWithout) = Builder.SeedAuthenticatedUser();
        using var clientWithout = await AuthenticatedClientWithCsrfAsync(tokenWithout);
        var denied = await clientWithout.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(ev.Id));

        var (_, tokenOther) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [other.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var clientOther = await AuthenticatedClientWithCsrfAsync(tokenOther);
        var deniedOther = await clientOther.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(ev.Id));

        var (_, tokenWith) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.EditEvents } });
        using var clientWith = await AuthenticatedClientWithCsrfAsync(tokenWith);
        var allowed = await clientWith.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(ev.Id));

        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(deniedOther.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(allowed.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Enable_twice_conflicts() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;

        var first = await client.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(eventId));
        var second = await client.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(eventId));

        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Db.AssemblyMotionPortals.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Enable_seeds_default_types() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;

        var response = await client.PostAsJsonAsync("/api/assemblymotions/portals", EnableRequest(eventId));
        var settings = await response.Content.ReadFromJsonAsync<AssemblyMotionPortalSettingsDTO>();

        await Assert.That(settings!.Types.Select(t => t.Code).ToArray())
            .IsEquivalentTo(new[] { "GP", "WP", "SÄA", "SBA", "PP", "SO" });
        await Assert.That(settings.EventId).IsEqualTo(eventId);
    }

    [Test]
    public async Task Settings_requires_events_edit() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var (_, tokenWithout) = Builder.SeedAuthenticatedUser();
        using var clientWithout = AuthenticatedClient(tokenWithout);

        var denied = await clientWithout.GetAsync($"/api/assemblymotions/portals/{portal.Id}/settings");
        var allowed = await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/settings");

        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(allowed.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task ByEvent_is_anonymous_for_public_event() {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "Bundesparteitag", visibility: EventVisibility.Public);
        SeedPortal(ev.Id);
        using var client = AnonymousClient();

        var response = await client.GetAsync($"/api/assemblymotions/portals/by-event/{ev.Id}");
        var dto = await response.Content.ReadFromJsonAsync<AssemblyMotionPortalDTO>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(dto!.EventName).IsEqualTo("Bundesparteitag");
    }

    [Test]
    public async Task ByEvent_hides_private_event_from_anonymous() {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Private);
        SeedPortal(ev.Id);
        using var client = AnonymousClient();

        var response = await client.GetAsync($"/api/assemblymotions/portals/by-event/{ev.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task ByEvent_is_not_found_without_portal() {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, visibility: EventVisibility.Public);
        using var client = AnonymousClient();

        var response = await client.GetAsync($"/api/assemblymotions/portals/by-event/{ev.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Update_renders_intro_html() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", new AssemblyMotionPortalUpdateRequest {
            Id = portal.Id,
            OpensAt = DateTimeOffset.UtcNow.AddDays(1),
            ClosesAt = DateTimeOffset.UtcNow.AddDays(30),
            IntroMarkdown = "**Hallo**"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var stored = Db.AssemblyMotionPortals.First(p => p.Id == portal.Id);
        await Assert.That(stored.IntroHtml).Contains("<strong>Hallo</strong>");
    }

    [Test]
    public async Task Update_rejects_prioritization_end_after_close() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var closes = DateTimeOffset.UtcNow.AddDays(30);

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", new AssemblyMotionPortalUpdateRequest {
            Id = portal.Id,
            OpensAt = DateTimeOffset.UtcNow.AddDays(1),
            ClosesAt = closes,
            PrioritizationEnabled = true,
            PrioritizationStartsAt = closes.AddDays(-2),
            PrioritizationEndsAt = closes.AddDays(1)
        });
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(body).Contains("PrioritizationEndsAt", StringComparison.OrdinalIgnoreCase);
    }

    [Test]
    public async Task Type_create_rejects_duplicate_code() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        SeedType(portal.Id, "AB");

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/types", new AssemblyMotionTypeCreateRequest {
            PortalId = portal.Id,
            Code = "ab",
            Name = "Doppelt"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Type_create_adds_type() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/types", new AssemblyMotionTypeCreateRequest {
            PortalId = portal.Id,
            Code = "Neu",
            Name = "Neue Art",
            MinSubmitters = 3
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionTypes.Count(t => t.PortalId == portal.Id && t.Code == "Neu")).IsEqualTo(1);
    }

    [Test]
    public async Task Type_code_change_refused_when_motions_exist() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");
        SeedMotion(portal.Id, type.Id);

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/types/{type.Id}", new AssemblyMotionTypeUpdateRequest {
            Id = type.Id,
            Code = "CD",
            Name = type.Name,
            MinSubmitters = 1
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Db.AssemblyMotionTypes.First(t => t.Id == type.Id).Code).IsEqualTo("AB");
    }

    [Test]
    public async Task Type_name_change_allowed_when_motions_exist() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");
        SeedMotion(portal.Id, type.Id);

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/types/{type.Id}", new AssemblyMotionTypeUpdateRequest {
            Id = type.Id,
            Code = "AB",
            Name = "Umbenannt",
            MinSubmitters = 1
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionTypes.First(t => t.Id == type.Id).Name).IsEqualTo("Umbenannt");
    }

    [Test]
    public async Task Type_default_text_is_saved_and_offered_with_the_portal() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");

        var response = await client.PutAsJsonAsync($"/api/assemblymotions/types/{type.Id}", new AssemblyMotionTypeUpdateRequest {
            Id = type.Id,
            Code = "AB",
            Name = type.Name,
            MinSubmitters = 1,
            DefaultText = "Der Bundesparteitag möge beschließen:"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That((await response.Content.ReadFromJsonAsync<AssemblyMotionTypeSettingsDTO>())!.DefaultText)
            .IsEqualTo("Der Bundesparteitag möge beschließen:");
        await Assert.That(Db.AssemblyMotionTypes.First(t => t.Id == type.Id).DefaultText).IsEqualTo("Der Bundesparteitag möge beschließen:");
        await Assert.That(Db.AssemblyMotionTypes.First(t => t.Id == type.Id).ToDto().DefaultText).IsEqualTo("Der Bundesparteitag möge beschließen:");
    }

    [Test]
    public async Task Type_delete_refused_when_motions_exist() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");
        SeedMotion(portal.Id, type.Id);

        var response = await client.DeleteAsync($"/api/assemblymotions/types/{type.Id}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Db.AssemblyMotionTypes.Count(t => t.Id == type.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task Type_delete_removes_unused_type() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");

        var response = await client.DeleteAsync($"/api/assemblymotions/types/{type.Id}");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(Db.AssemblyMotionTypes.Count(t => t.Id == type.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Type_delete_purges_deleted_drafts_with_their_rows() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        var type = SeedType(portal.Id, "AB");
        var draft = SeedMotion(portal.Id, type.Id);
        Db.Insert(new AssemblyMotionSubmitter { MotionId = draft.Id, MemberId = draft.CreatedByMemberId, IsAuthor = true, JoinedAt = DateTime.UtcNow });
        Db.Insert(new AssemblyMotionRevision { MotionId = draft.Id, Revision = 1, Title = "Antrag", EditedByUserId = Guid.NewGuid(), EditedAt = DateTime.UtcNow });
        Db.AssemblyMotions
            .Where(m => m.Id == draft.Id)
            .Set(m => m.DeletedAt, DateTime.UtcNow)
            .Update();

        var response = await client.DeleteAsync($"/api/assemblymotions/types/{type.Id}");

        await Assert.That(response.IsSuccessStatusCode).IsTrue();
        await Assert.That(Db.AssemblyMotionTypes.Count(t => t.Id == type.Id)).IsEqualTo(0);
        await Assert.That(Db.AssemblyMotions.Count(m => m.Id == draft.Id)).IsEqualTo(0);
        await Assert.That(Db.AssemblyMotionSubmitters.Count(s => s.MotionId == draft.Id)).IsEqualTo(0);
        await Assert.That(Db.AssemblyMotionRevisions.Count(r => r.MotionId == draft.Id)).IsEqualTo(0);
    }

    [Test]
    public async Task Type_name_has_control_characters_removed_and_must_not_be_only_control_characters() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);

        var created = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/types", new AssemblyMotionTypeCreateRequest {
            PortalId = portal.Id,
            Code = "Neu",
            Name = "Neue\r\nArt\u0001",
            MinSubmitters = 1
        });
        var rejected = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/types", new AssemblyMotionTypeCreateRequest {
            PortalId = portal.Id,
            Code = "Leer",
            Name = "\u0001\u0002",
            MinSubmitters = 1
        });

        await Assert.That(created.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(Db.AssemblyMotionTypes.First(t => t.PortalId == portal.Id && t.Code == "Neu").Name).IsEqualTo("NeueArt");
        await Assert.That(rejected.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.AssemblyMotionTypes.Count(t => t.PortalId == portal.Id && t.Code == "Leer")).IsEqualTo(0);
    }

    [Test]
    public async Task Wiki_prefix_and_label_are_checked_and_stored_without_control_characters() {
        var (_, eventId, client) = await SeedEditorAsync();
        using var _ = client;
        var portal = SeedPortal(eventId);
        AssemblyMotionPortalUpdateRequest Request(string prefix, string label) => new() {
            Id = portal.Id,
            OpensAt = portal.OpensAt,
            ClosesAt = portal.ClosesAt,
            WikiPagePrefix = prefix,
            WikiAssemblyLabel = label
        };

        var clean = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", Request("Antrag:Test\n/Sub", "BPT\r\n 1"));
        var joinsToDoubleSlash = await client.PutAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}", Request("A/\n/B", "BPT"));
        var stored = Db.AssemblyMotionPortals.First(p => p.Id == portal.Id);

        await Assert.That(clean.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(stored.WikiPagePrefix).IsEqualTo("Antrag:Test/Sub");
        await Assert.That(stored.WikiAssemblyLabel).IsEqualTo("BPT 1");
        await Assert.That(joinsToDoubleSlash.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
