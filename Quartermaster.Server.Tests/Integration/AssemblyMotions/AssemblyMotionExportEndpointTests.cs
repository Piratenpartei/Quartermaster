using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LinqToDB;
using Microsoft.AspNetCore.Mvc.Testing;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AssemblyMotions;

public class AssemblyMotionExportEndpointTests : IntegrationTestBase {
    private async Task<HttpClient> ClientAsync(string token) {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await AttachAntiforgeryTokenAsync(client);
        return client;
    }

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(EventVisibility visibility = EventVisibility.Public) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT " + Guid.NewGuid().ToString("N")[..6], visibility: visibility);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-10),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            WikiAssemblyLabel = "BPT 2026.1"
        };
        Db.Insert(portal);
        return (chapter, portal, SeedType(portal, "SÄA", "Satzungsänderungsantrag", 1));
    }

    private AssemblyMotionType SeedType(AssemblyMotionPortal portal, string code, string name, int sortOrder) {
        var type = new AssemblyMotionType {
            PortalId = portal.Id,
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            MinSubmitters = 1
        };
        Db.Insert(type);
        return type;
    }

    private void SetRankingWindow(AssemblyMotionPortal portal, DateTime endsAt) {
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.PrioritizationEnabled, true)
            .Set(p => p.PrioritizationStartsAt, DateTime.UtcNow.AddDays(-5))
            .Set(p => p.PrioritizationEndsAt, endsAt)
            .Update();
    }

    private AssemblyMotion SeedMotion(AssemblyMotionPortal portal, AssemblyMotionType type, int? number, AssemblyMotionStatus status = AssemblyMotionStatus.Admitted,
        Member? author = null, DateTime? submittedAt = null) {
        author ??= Builder.SeedMember(firstName: "Anna", lastName: "Autor");
        var motion = new AssemblyMotion {
            PortalId = portal.Id,
            TypeId = type.Id,
            Number = number,
            Title = "Antrag " + Guid.NewGuid().ToString("N")[..6],
            TextMarkdown = "Text",
            TextHtml = "<p>Text</p>",
            ReasonMarkdown = "Grund",
            ReasonHtml = "<p>Grund</p>",
            Status = status,
            CreatedByMemberId = author.Id,
            CreatedAt = DateTime.UtcNow,
            SubmittedAt = submittedAt ?? DateTime.UtcNow
        };
        Db.Insert(motion);
        Db.Insert(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        return motion;
    }

    private void Upvote(AssemblyMotion motion, int times) {
        for (var i = 0; i < times; i++) {
            var voter = Builder.SeedMember(firstName: "Voter" + i, lastName: "Stimme" + i);
            Db.Insert(new AssemblyMotionUpvote { MotionId = motion.Id, MemberId = voter.Id, CreatedAt = DateTime.UtcNow });
        }
    }

    private void Compete(AssemblyMotion first, AssemblyMotion second) {
        var ordered = new[] { first.Id, second.Id }.Order().ToArray();
        Db.Insert(new AssemblyMotionCompetition {
            MotionAId = ordered[0],
            MotionBId = ordered[1],
            DeclaringMotionId = first.Id,
            DeclaredByUserId = Guid.NewGuid(),
            DeclaredAt = DateTime.UtcNow
        });
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private async Task<HttpResponseMessage> Get(string token, AssemblyMotionPortal portal) {
        using var client = await ClientAsync(token);
        return await client.GetAsync($"/api/assemblymotions/portals/{portal.Id}/export");
    }

    private async Task<AssemblyMotionExportDTO> Export(Chapter chapter, AssemblyMotionPortal portal) {
        var response = await Get(SeedCommissionToken(chapter), portal);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AssemblyMotionExportDTO>())!;
    }

    [Test]
    public async Task Export_requires_commission() {
        var (chapter, portal, type) = SeedPortal();
        SeedMotion(portal, type, 1);
        var editor = Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.EditEvents]
        }).Token;

        var response = await Get(editor, portal);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains(I18nKey.Error.AssemblyMotion.NotCommission);
    }

    [Test]
    public async Task Export_on_hidden_event_is_not_found_for_outsiders() {
        var (_, portal, type) = SeedPortal(EventVisibility.Private);
        SeedMotion(portal, type, 1);

        var response = await Get(Builder.SeedAuthenticatedUser().Token, portal);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Export_for_commission_of_another_chapter_is_forbidden() {
        var (_, portal, type) = SeedPortal();
        SeedMotion(portal, type, 1);
        var otherChapter = Builder.SeedChapter("Other");

        var response = await Get(SeedCommissionToken(otherChapter), portal);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Export_contains_names_but_no_member_numbers() {
        var (chapter, portal, type) = SeedPortal();
        var author = Builder.SeedMember(memberNumber: 424242, firstName: "Berta", lastName: "Beispiel", email: "berta.geheim@test.local");
        var motion = SeedMotion(portal, type, 1, author: author);
        var coSubmitter = Builder.SeedMember(memberNumber: 535353, firstName: "Carl", lastName: "Co", email: "carl.geheim@test.local");
        Db.Insert(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter.Id, IsAuthor = false, JoinedAt = DateTime.UtcNow.AddMinutes(1), JoinedRevision = 1 });
        SetRankingWindow(portal, DateTime.UtcNow.AddDays(-1));
        Upvote(motion, 2);

        var response = await Get(SeedCommissionToken(chapter), portal);
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(body).Contains("Berta Beispiel");
        await Assert.That(body).Contains("Carl Co");
        await Assert.That(body.IndexOf("Berta Beispiel")).IsLessThan(body.IndexOf("Carl Co"));
        await Assert.That(body).DoesNotContain("424242");
        await Assert.That(body).DoesNotContain("535353");
        await Assert.That(body).DoesNotContain("geheim@test.local");
        await Assert.That(body).DoesNotContain("Voter");
        await Assert.That(body).DoesNotContain("Stimme");
        await Assert.That(body).DoesNotContain(author.Id.ToString());
        await Assert.That(body).DoesNotContain(motion.Id.ToString());
    }

    [Test]
    public async Task Export_writes_status_result_and_majority_as_names() {
        var (chapter, portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, 1, AssemblyMotionStatus.Admitted);
        Db.AssemblyMotions
            .Where(m => m.Id == motion.Id)
            .Set(m => m.Result, AssemblyMotionResult.AcceptedModified)
            .Update();
        Db.AssemblyMotionTypes
            .Where(t => t.Id == type.Id)
            .Set(t => t.RequiredMajority, RequiredMajority.TwoThirds)
            .Update();

        var response = await Get(SeedCommissionToken(chapter), portal);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = document.RootElement.GetProperty("motions")[0];

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(item.GetProperty("status").GetString()).IsEqualTo("Admitted");
        await Assert.That(item.GetProperty("result").GetString()).IsEqualTo("AcceptedModified");
        await Assert.That(item.GetProperty("requiredMajority").GetString()).IsEqualTo("TwoThirds");
    }

    [Test]
    public async Task Export_excludes_drafts_and_unnumbered() {
        var (chapter, portal, type) = SeedPortal();
        SeedMotion(portal, type, 1);
        SeedMotion(portal, type, null, AssemblyMotionStatus.Draft);
        SeedMotion(portal, type, null, AssemblyMotionStatus.CollectingCoSubmitters);

        var export = await Export(chapter, portal);

        await Assert.That(export.Motions.Select(m => m.DisplayNumber).ToList()).IsEquivalentTo(new[] { "SÄA001" });
        await Assert.That(export.Portal.AssemblyLabel).IsEqualTo("BPT 2026.1");
    }

    [Test]
    public async Task Export_includes_withdrawn_and_lapsed_with_status() {
        var (chapter, portal, type) = SeedPortal();
        SeedMotion(portal, type, 1, AssemblyMotionStatus.Submitted);
        SeedMotion(portal, type, 2, AssemblyMotionStatus.Withdrawn);
        SeedMotion(portal, type, 3, AssemblyMotionStatus.Lapsed);
        SeedMotion(portal, type, 4, AssemblyMotionStatus.FormallyInsufficient);

        var export = await Export(chapter, portal);

        await Assert.That(export.Motions.Select(m => m.Status).ToList()).IsEquivalentTo(new[] {
            AssemblyMotionStatus.Submitted,
            AssemblyMotionStatus.Withdrawn,
            AssemblyMotionStatus.Lapsed,
            AssemblyMotionStatus.FormallyInsufficient
        });
    }

    [Test]
    public async Task Export_rank_null_during_window() {
        var (chapter, portal, type) = SeedPortal();
        SetRankingWindow(portal, DateTime.UtcNow.AddDays(2));
        var motion = SeedMotion(portal, type, 1);
        Upvote(motion, 2);

        var export = await Export(chapter, portal);

        await Assert.That(export.Motions.Single().Rank).IsNull();
        await Assert.That(export.Motions.Single().UpvoteCount).IsNull();
    }

    [Test]
    public async Task Export_orders_by_type_then_number_while_ranking_is_not_public() {
        var (chapter, portal, type) = SeedPortal();
        var first = SeedType(portal, "A", "Erster", 0);
        SeedMotion(portal, type, 2);
        SeedMotion(portal, type, 1);
        SeedMotion(portal, first, 1);
        SeedMotion(portal, type, 3, AssemblyMotionStatus.Withdrawn);

        var export = await Export(chapter, portal);

        await Assert.That(string.Join("|", export.Motions.Select(m => m.DisplayNumber))).IsEqualTo("A001|SÄA001|SÄA002|SÄA003");
    }

    [Test]
    public async Task Export_orders_by_rank_then_unranked_when_ranking_is_public() {
        var (chapter, portal, type) = SeedPortal();
        var first = SeedType(portal, "A", "Erster", 0);
        SetRankingWindow(portal, DateTime.UtcNow.AddDays(-1));
        var lowVotes = SeedMotion(portal, first, 1);
        var topVotes = SeedMotion(portal, type, 2);
        SeedMotion(portal, type, 1, AssemblyMotionStatus.Withdrawn);
        SeedMotion(portal, first, 2, AssemblyMotionStatus.Lapsed);
        Upvote(lowVotes, 1);
        Upvote(topVotes, 3);

        var export = await Export(chapter, portal);

        await Assert.That(string.Join("|", export.Motions.Select(m => m.DisplayNumber))).IsEqualTo("SÄA002|A001|A002|SÄA001");
        await Assert.That(export.Motions[0].Rank).IsEqualTo(1);
        await Assert.That(export.Motions[0].UpvoteCount).IsEqualTo(3);
        await Assert.That(export.Motions[1].Rank).IsEqualTo(2);
        await Assert.That(export.Motions[1].UpvoteCount).IsEqualTo(1);
        await Assert.That(export.Motions[2].Rank).IsNull();
        await Assert.That(export.Motions[2].UpvoteCount).IsNull();
    }

    [Test]
    public async Task Export_competing_numbers_never_include_a_draft() {
        var (chapter, portal, type) = SeedPortal();
        var motion = SeedMotion(portal, type, 1);
        var competitor = SeedMotion(portal, type, 2);
        var draft = SeedMotion(portal, type, null, AssemblyMotionStatus.Draft);
        Compete(motion, competitor);
        Compete(motion, draft);

        var export = await Export(chapter, portal);

        await Assert.That(export.Motions.Single(m => m.DisplayNumber == "SÄA001").CompetingDisplayNumbers.ToList()).IsEquivalentTo(new[] { "SÄA002" });
        await Assert.That(export.Motions.Single(m => m.DisplayNumber == "SÄA002").CompetingDisplayNumbers.ToList()).IsEquivalentTo(new[] { "SÄA001" });
    }

    [Test]
    public async Task Export_writes_one_audit_entry() {
        var (chapter, portal, type) = SeedPortal();
        SeedMotion(portal, type, 1);

        await Export(chapter, portal);

        await Assert.That(Db.AuditLogs.Count(a => a.EntityId == portal.Id && a.Action == "Exported")).IsEqualTo(1);
    }
}
