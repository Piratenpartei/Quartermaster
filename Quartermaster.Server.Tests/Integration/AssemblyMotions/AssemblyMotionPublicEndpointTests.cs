using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionPublicEndpointTests : IntegrationTestBase {
    private (AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(EventVisibility visibility = EventVisibility.Public) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT", visibility: visibility);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        Db.Insert(portal);
        var type = new AssemblyMotionType {
            PortalId = portal.Id,
            Code = "SÄA",
            Name = "Satzungsänderungsantrag",
            MinSubmitters = 1
        };
        Db.Insert(type);
        return (portal, type);
    }

    private AssemblyMotion SeedMotion(
        AssemblyMotionPortal portal,
        AssemblyMotionType type,
        string title,
        AssemblyMotionStatus status,
        int? number,
        string text = "Text",
        string firstName = "Zebulon",
        string lastName = "Quaxwell",
        int memberNumber = 0) {
        var chapter = Builder.SeedChapter("Member chapter " + Guid.NewGuid());
        var member = Builder.SeedMember(chapter.Id, memberNumber: memberNumber, firstName: firstName, lastName: lastName);
        var motion = new AssemblyMotion {
            PortalId = portal.Id,
            TypeId = type.Id,
            Number = number,
            Title = title,
            TextMarkdown = text,
            TextHtml = $"<p>{text}</p>",
            ReasonHtml = "<p>Grund</p>",
            Status = status,
            CreatedByMemberId = member.Id,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = number == null ? null : DateTime.UtcNow
        };
        Db.Insert(motion);
        Db.Insert(new AssemblyMotionSubmitter {
            MotionId = motion.Id,
            MemberId = member.Id,
            IsAuthor = true,
            JoinedAt = DateTime.UtcNow
        });
        return motion;
    }

    [Test]
    public async Task Lists_every_motion_except_drafts() {
        var (portal, type) = SeedPortal();
        SeedMotion(portal, type, "Entwurf", AssemblyMotionStatus.Draft, null);
        SeedMotion(portal, type, "Sammelnd", AssemblyMotionStatus.CollectingCoSubmitters, null);
        SeedMotion(portal, type, "Eingereicht", AssemblyMotionStatus.Submitted, 1);
        SeedMotion(portal, type, "Zugelassen", AssemblyMotionStatus.Admitted, 2);
        SeedMotion(portal, type, "Zurueck ohne Nummer", AssemblyMotionStatus.Withdrawn, null);
        SeedMotion(portal, type, "Zurueck mit Nummer", AssemblyMotionStatus.Withdrawn, 3);
        using var client = AnonymousClient();

        var response = await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/motions");
        var list = await response.Content.ReadFromJsonAsync<AssemblyMotionPublicListResponse>();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Items.Select(i => i.Title).ToArray())
            .IsEquivalentTo(new[] { "Eingereicht", "Zugelassen", "Zurueck mit Nummer", "Sammelnd", "Zurueck ohne Nummer" });
        await Assert.That(list.TotalCount).IsEqualTo(5);
        await Assert.That(list.Items.Take(3).Select(i => i.DisplayNumber).ToArray())
            .IsEquivalentTo(new[] { "SÄA001", "SÄA002", "SÄA003" });
    }

    [Test]
    public async Task Anonymous_responses_contain_no_submitter_names() {
        var (portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, "Eingereicht", AssemblyMotionStatus.Submitted, 1, memberNumber: 987654);
        using var client = AnonymousClient();

        var listBody = await (await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/motions")).Content.ReadAsStringAsync();
        var detailBody = await (await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public")).Content.ReadAsStringAsync();

        foreach (var body in new[] { listBody, detailBody }) {
            await Assert.That(body).DoesNotContain("Zebulon");
            await Assert.That(body).DoesNotContain("Quaxwell");
            await Assert.That(body).DoesNotContain("987654");
        }
        await Assert.That(detailBody).Contains("Eingereicht");
    }

    [Test]
    public async Task Edited_motion_shows_current_text_in_revision_anonymously() {
        var (portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, "Antrag", AssemblyMotionStatus.Submitted, 1, text: "Alt");
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.TextHtml, "<p>Neu</p>")
            .Set(m => m.Revision, 2)
            .Set(m => m.Status, AssemblyMotionStatus.CollectingCoSubmitters)
            .Update();
        using var client = AnonymousClient();

        var listResponse = await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/motions");
        var list = await listResponse.Content.ReadFromJsonAsync<AssemblyMotionPublicListResponse>();
        var detailResponse = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public");
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        var detail = await detailResponse.Content.ReadFromJsonAsync<AssemblyMotionPublicDTO>();

        await Assert.That(list!.Items.Count).IsEqualTo(1);
        await Assert.That(list.Items[0].IsInRevision).IsTrue();
        await Assert.That(detail!.IsInRevision).IsTrue();
        await Assert.That(detail.TextHtml).IsEqualTo("<p>Neu</p>");
        await Assert.That(detailBody).DoesNotContain("Alt");
        await Assert.That(detailBody).DoesNotContain("Zebulon");
        await Assert.That(detailBody).DoesNotContain("Quaxwell");
    }

    [Test]
    public async Task Search_matches_display_number_without_padding() {
        var (portal, type) = SeedPortal();
        SeedMotion(portal, type, "Erster", AssemblyMotionStatus.Submitted, 1);
        SeedMotion(portal, type, "Dritter", AssemblyMotionStatus.Submitted, 3);
        using var client = AnonymousClient();

        var padded = await client.GetFromJsonAsync<AssemblyMotionPublicListResponse>(
            $"/api/assemblymotions/portals/{portal.Id}/motions?search={Uri.EscapeDataString("SÄA003")}");
        var unpadded = await client.GetFromJsonAsync<AssemblyMotionPublicListResponse>(
            $"/api/assemblymotions/portals/{portal.Id}/motions?search={Uri.EscapeDataString("säa3")}");
        var byTitle = await client.GetFromJsonAsync<AssemblyMotionPublicListResponse>(
            $"/api/assemblymotions/portals/{portal.Id}/motions?search=erster");

        await Assert.That(padded!.Items.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Dritter" });
        await Assert.That(unpadded!.Items.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Dritter" });
        await Assert.That(byTitle!.Items.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Erster" });
    }

    [Test]
    public async Task Private_event_portal_returns_404_anonymously() {
        var (portal, type) = SeedPortal(EventVisibility.Private);
        var motion = SeedMotion(portal, type, "Eingereicht", AssemblyMotionStatus.Submitted, 1);
        using var client = AnonymousClient();

        var list = await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/motions");
        var detail = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public");

        await Assert.That(list.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Detail_of_collecting_motion_is_public_without_names() {
        var (portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, "Sammelnd", AssemblyMotionStatus.CollectingCoSubmitters, null);
        using var client = AnonymousClient();

        var detail = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public");
        var dto = await detail.Content.ReadFromJsonAsync<AssemblyMotionPublicDTO>();

        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(dto!.Title).IsEqualTo("Sammelnd");
        await Assert.That(dto.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
    }

    [Test]
    public async Task Detail_of_draft_returns_404() {
        var (portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, "Entwurf", AssemblyMotionStatus.Draft, null);
        using var client = AnonymousClient();

        var detail = await client.GetAsync($"/api/assemblymotions/motions/{motion.Id}/public");

        await Assert.That(detail.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
