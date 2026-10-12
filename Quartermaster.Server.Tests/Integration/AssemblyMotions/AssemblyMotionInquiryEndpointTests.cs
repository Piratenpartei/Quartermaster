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
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionInquiryEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal() {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: EventVisibility.Public);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-1),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            WikiMirrorEnabled = true
        };
        Db.Insert(portal);
        var type = new AssemblyMotionType {
            PortalId = portal.Id,
            Code = "SÄA",
            Name = "Satzungsänderungsantrag",
            MinSubmitters = 1
        };
        Db.Insert(type);
        return (chapter, portal, type);
    }

    private Participant SeedParticipant(Chapter chapter, string firstName, string lastName) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, firstName: firstName, lastName: lastName, userId: user.Id);
        return new Participant(member, token);
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private async Task<AssemblyMotionDetailDTO> CreateMotion(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type, bool release = true) {
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = type.Id,
            Title = "Antrag",
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        if (release)
            await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created;
    }

    private async Task<HttpResponseMessage> Ask(string token, Guid motionId, string question = "Was ist gemeint?", int dueInHours = 48) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PostAsJsonAsync($"/api/assemblymotions/motions/{motionId}/inquiries", new AssemblyMotionInquiryAskRequest {
            QuestionMarkdown = question,
            ResponseDueAt = DateTimeOffset.UtcNow.AddHours(dueInHours)
        });
    }

    private async Task<HttpResponseMessage> Post(string token, string url, object body) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PostAsJsonAsync(url, body);
    }

    private async Task<AssemblyMotionDetailDTO> Detail(string token, Guid motionId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return (await client.GetFromJsonAsync<AssemblyMotionDetailDTO>($"/api/assemblymotions/motions/{motionId}"))!;
    }

    [Test]
    public async Task Ask_answer_close_round_trip() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);

        var asked = await Ask(commission, created.Id, "**Warum?**");
        var askedDetail = await asked.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var inquiryId = askedDetail!.Inquiries.Single().Id;
        var answered = await Post(author.Token, $"/api/assemblymotions/inquiries/{inquiryId}/answer", new { AnswerMarkdown = "Darum." });
        var answeredDetail = await answered.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var closed = await Post(commission, $"/api/assemblymotions/inquiries/{inquiryId}/close", new { });
        var closedDetail = await closed.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(asked.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(askedDetail.OpenInquiryCount).IsEqualTo(1);
        await Assert.That(askedDetail.Inquiries[0].QuestionHtml).Contains("<strong>Warum?</strong>");
        await Assert.That(answered.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(answeredDetail!.Inquiries[0].AnswerHtml).Contains("Darum.");
        await Assert.That(answeredDetail.Inquiries[0].AnsweredByName).IsEqualTo("Anna Autor");
        await Assert.That(closedDetail!.OpenInquiryCount).IsEqualTo(0);
        await Assert.That(closedDetail.Inquiries[0].ClosedAt).IsNotNull();
    }

    [Test]
    public async Task Due_less_than_24h_refused() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);

        var response = await Ask(SeedCommissionToken(chapter), created.Id, dueInHours: 12);

        await Assert.That(response.StatusCode).IsEqualTo((HttpStatusCode)422);
    }

    [Test]
    public async Task Ask_refused_without_number() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        Db.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.MinSubmitters, 5)
            .Update();
        var created = await CreateMotion(author, portal, type);

        var response = await Ask(SeedCommissionToken(chapter), created.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Ask_requires_commission() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Beisitzer");
        var created = await CreateMotion(author, portal, type);

        var asEligible = await Ask(other.Token, created.Id);
        var asAuthor = await Ask(author.Token, created.Id);

        await Assert.That(asEligible.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(asAuthor.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Ask_validates_markdown() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);

        var empty = await Ask(commission, created.Id, "  ");
        var html = await Ask(commission, created.Id, "<b>x</b>");
        var image = await Ask(commission, created.Id, "![a](http://x/y.png)");

        await Assert.That(empty.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(html.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(image.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Close_requires_commission() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);
        var inquiryId = (await (await Ask(commission, created.Id)).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!.Inquiries.Single().Id;

        var asAuthor = await Post(author.Token, $"/api/assemblymotions/inquiries/{inquiryId}/close", new { });
        var unknown = await Post(commission, $"/api/assemblymotions/inquiries/{Guid.NewGuid()}/close", new { });
        var asCommission = await Post(commission, $"/api/assemblymotions/inquiries/{inquiryId}/close", new { });
        var again = await Post(commission, $"/api/assemblymotions/inquiries/{inquiryId}/close", new { });

        await Assert.That(asAuthor.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(asCommission.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(again.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Non_submitter_cannot_answer() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Beisitzer");
        var created = await CreateMotion(author, portal, type);
        var inquiryId = (await (await Ask(SeedCommissionToken(chapter), created.Id)).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!.Inquiries.Single().Id;

        var response = await Post(other.Token, $"/api/assemblymotions/inquiries/{inquiryId}/answer", new { AnswerMarkdown = "Ich auch" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Answer_refused_for_submitter_of_other_motion() {
        var (chapter, portal, type) = SeedPortal();
        var authorA = SeedParticipant(chapter, "Anna", "Autor");
        var authorB = SeedParticipant(chapter, "Bernd", "Beisitzer");
        await CreateMotion(authorA, portal, type);
        var motionB = await CreateMotion(authorB, portal, type);
        var inquiryId = (await (await Ask(SeedCommissionToken(chapter), motionB.Id)).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!.Inquiries.Single().Id;

        var response = await Post(authorA.Token, $"/api/assemblymotions/inquiries/{inquiryId}/answer", new { AnswerMarkdown = "Fremd" });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.AssemblyMotionInquiries.First(i => i.Id == inquiryId).AnswerMarkdown).IsNull();
    }

    [Test]
    public async Task Answer_after_close_refused_and_second_answer_replaces_first() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);
        var inquiryId = (await (await Ask(commission, created.Id)).Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!.Inquiries.Single().Id;
        var url = $"/api/assemblymotions/inquiries/{inquiryId}/answer";

        await Post(author.Token, url, new { AnswerMarkdown = "Erste" });
        var second = await Post(author.Token, url, new { AnswerMarkdown = "Zweite" });
        var secondDetail = await second.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var empty = await Post(author.Token, url, new { AnswerMarkdown = "" });
        await Post(commission, $"/api/assemblymotions/inquiries/{inquiryId}/close", new { });
        var late = await Post(author.Token, url, new { AnswerMarkdown = "Spät" });

        await Assert.That(secondDetail!.Inquiries.Single().AnswerHtml).Contains("Zweite");
        await Assert.That(empty.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(late.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task Inquiry_hidden_from_eligible_non_submitter() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bernd", "Beisitzer");
        var created = await CreateMotion(author, portal, type);
        await Ask(SeedCommissionToken(chapter), created.Id);

        var asOther = await Detail(other.Token, created.Id);
        var asAuthor = await Detail(author.Token, created.Id);
        using var anonymous = CreateClient();
        var publicResponse = await anonymous.GetAsync($"/api/assemblymotions/motions/{created.Id}/public");

        await Assert.That(asOther.Inquiries.Count).IsEqualTo(0);
        await Assert.That(asOther.OpenInquiryCount).IsEqualTo(0);
        await Assert.That(asAuthor.Inquiries.Count).IsEqualTo(1);
        await Assert.That(asAuthor.OpenInquiryCount).IsEqualTo(1);
        await Assert.That((await publicResponse.Content.ReadAsStringAsync()).Contains("Was ist gemeint")).IsFalse();
    }

    [Test]
    public async Task Ask_queues_wiki_sync() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        Db.AssemblyMotions
            .Where(m => m.Id == created.Id)
            .Set(m => m.WikiSyncState, WikiSyncState.Synced)
            .Update();

        await Ask(SeedCommissionToken(chapter), created.Id);

        await Assert.That(Db.AssemblyMotions.First(m => m.Id == created.Id).WikiSyncState).IsEqualTo(WikiSyncState.Pending);
    }

    [Test]
    public async Task Dashboard_filters_open_inquiries() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var withInquiry = await CreateMotion(author, portal, type);
        var without = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);
        await Ask(commission, withInquiry.Id);
        using var client = await AuthenticatedClientWithCsrfAsync(commission);

        var open = await client.GetFromJsonAsync<AssemblyMotionDashboardResponse>($"/api/assemblymotions/portals/{portal.Id}/dashboard?hasOpenInquiry=true");
        var none = await client.GetFromJsonAsync<AssemblyMotionDashboardResponse>($"/api/assemblymotions/portals/{portal.Id}/dashboard?hasOpenInquiry=false");
        var all = await client.GetFromJsonAsync<AssemblyMotionDashboardResponse>($"/api/assemblymotions/portals/{portal.Id}/dashboard");

        await Assert.That(open!.Items.Select(i => i.Id).ToArray()).IsEquivalentTo(new[] { withInquiry.Id });
        await Assert.That(open.Items[0].OpenInquiryCount).IsEqualTo(1);
        await Assert.That(none!.Items.Select(i => i.Id).ToArray()).IsEquivalentTo(new[] { without.Id });
        await Assert.That(all!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Mine_counts_open_inquiries() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        var commission = SeedCommissionToken(chapter);
        await Ask(commission, created.Id);
        await Ask(commission, created.Id, "Noch eine?");
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);

        var items = await client.GetFromJsonAsync<AssemblyMotionMineItemDTO[]>($"/api/assemblymotions/portals/{portal.Id}/mine");

        await Assert.That(items!.Single().OpenInquiryCount).IsEqualTo(2);
    }
}
