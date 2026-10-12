using System;
using System.Collections.Generic;
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

public class AssemblyMotionRankingEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(
        EventVisibility visibility = EventVisibility.Public, bool windowOpen = true) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT", visibility: visibility);
        var portal = new AssemblyMotionPortal {
            EventId = ev.Id,
            OpensAt = DateTime.UtcNow.AddDays(-10),
            ClosesAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            PrioritizationEnabled = true,
            PrioritizationStartsAt = windowOpen ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(1),
            PrioritizationEndsAt = DateTime.UtcNow.AddDays(2)
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

    private void MoveWindow(AssemblyMotionPortal portal, DateTime startsAt, DateTime endsAt) {
        Db.AssemblyMotionPortals
            .Where(p => p.Id == portal.Id)
            .Set(p => p.PrioritizationStartsAt, startsAt)
            .Set(p => p.PrioritizationEndsAt, endsAt)
            .Update();
    }

    private Participant SeedParticipant(Chapter chapter, string firstName = "Vera", string lastName = "Voter") {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, firstName: firstName, lastName: lastName, userId: user.Id);
        return new Participant(member, token);
    }

    private string SeedCommissionToken(Chapter chapter)
        => Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        }).Token;

    private async Task<AssemblyMotionDetailDTO> CreateSubmitted(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type, string title = "Antrag") {
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new AssemblyMotionSaveRequest {
            TypeId = type.Id,
            Title = title,
            TextMarkdown = "Text",
            ReasonMarkdown = "Grund"
        });
        var created = (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
        await client.PostAsJsonAsync($"/api/assemblymotions/motions/{created.Id}/release", new { });
        return created;
    }

    private async Task<HttpResponseMessage> Send(string token, HttpMethod method, string url) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var request = new HttpRequestMessage(method, url);
        if (method == HttpMethod.Post)
            request.Content = JsonContent.Create(new { });
        return await client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Upvote(Participant voter, Guid motionId)
        => Send(voter.Token, HttpMethod.Post, $"/api/assemblymotions/motions/{motionId}/upvote");

    private async Task<AssemblyMotionDetailDTO> GetDetail(string token, Guid motionId) {
        var response = await Send(token, HttpMethod.Get, $"/api/assemblymotions/motions/{motionId}");
        return (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
    }

    private async Task<AssemblyMotionPublicDTO> GetPublic(Guid motionId) {
        using var anonymous = CreateClient();
        return (await anonymous.GetFromJsonAsync<AssemblyMotionPublicDTO>($"/api/assemblymotions/motions/{motionId}/public"))!;
    }

    [Test]
    public async Task Upvote_round_trip_sets_flags() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);

        var before = await GetDetail(voter.Token, motion.Id);
        var response = await Upvote(voter, motion.Id);
        var after = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var removed = await Send(voter.Token, HttpMethod.Delete, $"/api/assemblymotions/motions/{motion.Id}/upvote");
        var removedDetail = await removed.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(before.CanUpvote).IsTrue();
        await Assert.That(before.HasUpvoted).IsFalse();
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(after!.HasUpvoted).IsTrue();
        await Assert.That(after.UpvoteCount).IsNull();
        await Assert.That(removedDetail!.HasUpvoted).IsFalse();
    }

    [Test]
    public async Task Upvote_requires_eligibility() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var outsider = SeedParticipant(Builder.SeedChapter("Foreign"));
        var motion = await CreateSubmitted(author, portal, type);

        var response = await Upvote(outsider, motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.AssemblyMotionUpvotes.Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Upvote_outside_window_is_conflict() {
        var (chapter, portal, type) = SeedPortal(windowOpen: false);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);

        var response = await Upvote(voter, motion.Id);
        var detail = await GetDetail(voter.Token, motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.assembly_motion.prioritization_not_open");
        await Assert.That(detail.CanUpvote).IsFalse();
    }

    [Test]
    public async Task Remove_upvote_outside_window_refused() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        await Upvote(voter, motion.Id);
        MoveWindow(portal, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1));

        var response = await Send(voter.Token, HttpMethod.Delete, $"/api/assemblymotions/motions/{motion.Id}/upvote");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(Db.AssemblyMotionUpvotes.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task My_upvotes_returns_only_own() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var other = SeedParticipant(chapter, "Otto", "Other");
        var first = await CreateSubmitted(author, portal, type, "Erster");
        var second = await CreateSubmitted(author, portal, type, "Zweiter");
        await Upvote(voter, first.Id);
        await Upvote(other, first.Id);
        await Upvote(other, second.Id);
        var url = $"/api/assemblymotions/portals/{portal.Id}/my-upvotes";

        var mine = await (await Send(voter.Token, HttpMethod.Get, url)).Content.ReadFromJsonAsync<List<Guid>>();
        var theirs = await (await Send(other.Token, HttpMethod.Get, url)).Content.ReadFromJsonAsync<List<Guid>>();
        var noMember = await Send(Builder.SeedAuthenticatedUser().Token, HttpMethod.Get, url);
        using var anonymous = CreateClient();
        var anonymousResponse = await anonymous.GetAsync(url);

        await Assert.That(mine!).IsEquivalentTo(new[] { first.Id });
        await Assert.That(theirs!).IsEquivalentTo(new[] { first.Id, second.Id });
        await Assert.That(noMember.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Counts_hidden_during_window() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var commission = SeedCommissionToken(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        await Upvote(voter, motion.Id);

        var asCommission = await GetDetail(commission, motion.Id);
        var asAuthor = await GetDetail(author.Token, motion.Id);
        var publicDetail = await GetPublic(motion.Id);
        var rawCommission = await (await Send(commission, HttpMethod.Get, $"/api/assemblymotions/motions/{motion.Id}")).Content.ReadAsStringAsync();
        var dashboard = await (await Send(commission, HttpMethod.Get, $"/api/assemblymotions/portals/{portal.Id}/dashboard")).Content.ReadAsStringAsync();

        await Assert.That(asCommission.UpvoteCount).IsNull();
        await Assert.That(asAuthor.UpvoteCount).IsNull();
        await Assert.That(publicDetail.UpvoteCount).IsNull();
        await Assert.That(rawCommission.ToLowerInvariant()).DoesNotContain("\"upvotecount\":1");
        await Assert.That(dashboard.ToLowerInvariant()).DoesNotContain("upvote");
    }

    [Test]
    public async Task Counts_hidden_before_window_too() {
        var (chapter, portal, type) = SeedPortal(windowOpen: false);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var commission = SeedCommissionToken(chapter);
        var motion = await CreateSubmitted(author, portal, type);
        Db.Insert(new AssemblyMotionUpvote { MotionId = motion.Id, MemberId = author.Member.Id, CreatedAt = DateTime.UtcNow });

        var asCommission = await GetDetail(commission, motion.Id);
        var publicDetail = await GetPublic(motion.Id);

        await Assert.That(asCommission.UpvoteCount).IsNull();
        await Assert.That(publicDetail.UpvoteCount).IsNull();
    }

    [Test]
    public async Task Ranking_endpoint_409_before_end() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        await CreateSubmitted(author, portal, type);
        using var anonymous = CreateClient();

        var response = await anonymous.GetAsync($"/api/assemblymotions/portals/{portal.Id}/ranking");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Conflict);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.assembly_motion.ranking_not_available");
    }

    [Test]
    public async Task Ranking_is_public_after_end_with_counts() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var other = SeedParticipant(chapter, "Otto", "Other");
        var popular = await CreateSubmitted(author, portal, type, "Beliebt");
        var quiet = await CreateSubmitted(author, portal, type, "Ruhig");
        await Upvote(voter, quiet.Id);
        await Upvote(voter, popular.Id);
        await Upvote(other, popular.Id);
        MoveWindow(portal, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1));
        using var anonymous = CreateClient();

        var response = await anonymous.GetAsync($"/api/assemblymotions/portals/{portal.Id}/ranking");
        var ranking = await response.Content.ReadFromJsonAsync<AssemblyMotionRankingResponse>();
        var publicDetail = await GetPublic(popular.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(ranking!.Items.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Beliebt", "Ruhig" });
        await Assert.That(ranking.Items[0].Rank).IsEqualTo(1);
        await Assert.That(ranking.Items[0].UpvoteCount).IsEqualTo(2);
        await Assert.That(ranking.Items[0].DisplayNumber).IsEqualTo("SÄA001");
        await Assert.That(ranking.Items[1].UpvoteCount).IsEqualTo(1);
        await Assert.That(publicDetail.UpvoteCount).IsEqualTo(2);
    }

    [Test]
    public async Task Ranking_response_has_no_names() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Zebulon", "Quaxwell");
        var voter = SeedParticipant(chapter, "Valentina", "Wahlhelfer");
        var motion = await CreateSubmitted(author, portal, type);
        await Upvote(voter, motion.Id);
        MoveWindow(portal, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1));
        using var anonymous = CreateClient();

        var body = await anonymous.GetStringAsync($"/api/assemblymotions/portals/{portal.Id}/ranking");

        await Assert.That(body).DoesNotContain("Zebulon");
        await Assert.That(body).DoesNotContain("Quaxwell");
        await Assert.That(body).DoesNotContain("Valentina");
        await Assert.That(body).DoesNotContain("Wahlhelfer");
        await Assert.That(body).DoesNotContain(voter.Member.Id.ToString());
        await Assert.That(body).DoesNotContain(author.Member.Id.ToString());
    }

    [Test]
    public async Task Ranking_respects_event_visibility() {
        var (chapter, portal, type) = SeedPortal(EventVisibility.Private);
        MoveWindow(portal, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1));
        using var anonymous = CreateClient();

        var anonymousResponse = await anonymous.GetAsync($"/api/assemblymotions/portals/{portal.Id}/ranking");
        var unknown = await anonymous.GetAsync($"/api/assemblymotions/portals/{Guid.NewGuid()}/ranking");

        await Assert.That(anonymousResponse.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(unknown.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    [Arguments(AssemblyMotionStatus.Withdrawn)]
    [Arguments(AssemblyMotionStatus.FormallyInsufficient)]
    public async Task Counts_hidden_for_motion_dropped_from_ranking(AssemblyMotionStatus status) {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var voter = SeedParticipant(chapter);
        var dropped = await CreateSubmitted(author, portal, type, "Verworfen");
        var kept = await CreateSubmitted(author, portal, type, "Behalten");
        await Upvote(voter, dropped.Id);
        await Upvote(voter, kept.Id);
        Db.AssemblyMotions
            .Where(m => m.Id == dropped.Id)
            .Set(m => m.Status, status)
            .Update();
        MoveWindow(portal, DateTime.UtcNow.AddDays(-3), DateTime.UtcNow.AddDays(-1));

        var memberDropped = await GetDetail(author.Token, dropped.Id);
        var publicDropped = await GetPublic(dropped.Id);
        var memberKept = await GetDetail(author.Token, kept.Id);
        var publicKept = await GetPublic(kept.Id);

        await Assert.That(memberDropped.UpvoteCount).IsNull();
        await Assert.That(publicDropped.UpvoteCount).IsNull();
        await Assert.That(memberKept.UpvoteCount).IsEqualTo(1);
        await Assert.That(publicKept.UpvoteCount).IsEqualTo(1);
    }
}
