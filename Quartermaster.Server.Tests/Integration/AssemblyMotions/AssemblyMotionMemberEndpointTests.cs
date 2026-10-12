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

public class AssemblyMotionMemberEndpointTests : IntegrationTestBase {
    private record Participant(Member Member, string Token);

    private (Chapter Chapter, AssemblyMotionPortal Portal, AssemblyMotionType Type) SeedPortal(int minSubmitters = 1) {
        var chapter = Builder.SeedChapter("Chapter");
        var ev = Builder.SeedEvent(chapter.Id, publicName: "BPT", visibility: EventVisibility.Public);
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
            MinSubmitters = minSubmitters
        };
        Db.Insert(type);
        return (chapter, portal, type);
    }

    private Participant SeedParticipant(Chapter chapter, string firstName, string lastName, int memberNumber = 0) {
        var (user, token) = Builder.SeedAuthenticatedUser();
        var member = Builder.SeedMember(chapterId: chapter.Id, memberNumber: memberNumber, firstName: firstName, lastName: lastName, userId: user.Id);
        return new Participant(member, token);
    }

    private static AssemblyMotionSaveRequest SaveRequest(AssemblyMotionType type, string title = "Antrag") => new() {
        TypeId = type.Id,
        Title = title,
        TextMarkdown = "Text",
        ReasonMarkdown = "Grund"
    };

    private async Task<AssemblyMotionDetailDTO> CreateMotion(Participant author, AssemblyMotionPortal portal, AssemblyMotionType type, string title = "Antrag") {
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);
        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", SaveRequest(type, title));
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>())!;
    }

    private async Task<HttpResponseMessage> Post(Participant participant, string url) {
        using var client = await AuthenticatedClientWithCsrfAsync(participant.Token);
        return await client.PostAsJsonAsync(url, new { });
    }

    private async Task<AssemblyMotionDetailDTO?> GetDetail(string token, Guid motionId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        var response = await client.GetAsync($"/api/assemblymotions/motions/{motionId}");
        return response.StatusCode == HttpStatusCode.OK
            ? await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>()
            : null;
    }

    [Test]
    public async Task Create_requires_eligibility() {
        var (_, portal, type) = SeedPortal();
        var foreignChapter = Builder.SeedChapter("Foreign");
        var outsider = SeedParticipant(foreignChapter, "Out", "Sider");
        using var client = await AuthenticatedClientWithCsrfAsync(outsider.Token);

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", SaveRequest(type));
        var body = await response.Content.ReadAsStringAsync();

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(body).Contains("error.assembly_motion.not_eligible");
    }

    [Test]
    public async Task Full_flow_release_join_submit() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 2);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var second = SeedParticipant(chapter, "Bert", "Zweiter");

        var created = await CreateMotion(author, portal, type);
        var released = await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");
        var releasedDetail = await released.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var joined = await Post(second, $"/api/assemblymotions/motions/{created.Id}/join");
        var detail = await GetDetail(author.Token, created.Id);

        await Assert.That(created.Status).IsEqualTo(AssemblyMotionStatus.Draft);
        await Assert.That(created.CanRelease).IsTrue();
        await Assert.That(released.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(releasedDetail!.Status).IsEqualTo(AssemblyMotionStatus.CollectingCoSubmitters);
        await Assert.That(joined.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.Status).IsEqualTo(AssemblyMotionStatus.Submitted);
        await Assert.That(detail.DisplayNumber).IsEqualTo("SÄA001");
        await Assert.That(detail.Submitters.Select(s => s.DisplayName).ToArray())
            .IsEquivalentTo(new[] { "Anna Autor", "Bert Zweiter" });
    }

    [Test]
    public async Task Collecting_list_hidden_from_ineligible_member() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 2);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var outsider = SeedParticipant(Builder.SeedChapter("Foreign"), "Out", "Sider");
        var created = await CreateMotion(author, portal, type);
        await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");

        using var outsiderClient = await AuthenticatedClientWithCsrfAsync(outsider.Token);
        var denied = await outsiderClient.GetAsync($"/api/assemblymotions/portals/{portal.Id}/collecting");
        using var authorClient = await AuthenticatedClientWithCsrfAsync(author.Token);
        var allowed = await authorClient.GetAsync($"/api/assemblymotions/portals/{portal.Id}/collecting");
        var list = await allowed.Content.ReadFromJsonAsync<AssemblyMotionCollectingListResponse>();

        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(allowed.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(list!.Items.Count).IsEqualTo(1);
        await Assert.That(list.Items[0].SubmitterCount).IsEqualTo(1);
        await Assert.That(list.Items[0].MinSubmitters).IsEqualTo(2);
    }

    [Test]
    public async Task Draft_detail_404_for_other_member() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bert", "Anderer");
        var created = await CreateMotion(author, portal, type);

        using var client = await AuthenticatedClientWithCsrfAsync(other.Token);
        var response = await client.GetAsync($"/api/assemblymotions/motions/{created.Id}");
        var release = await Post(other, $"/api/assemblymotions/motions/{created.Id}/release");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That(release.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
        await Assert.That((await GetDetail(author.Token, created.Id))!.Title).IsEqualTo("Antrag");
    }

    [Test]
    public async Task Member_numbers_only_for_commission() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 2);
        var author = SeedParticipant(chapter, "Anna", "Autor", memberNumber: 111111);
        var second = SeedParticipant(chapter, "Bert", "Zweiter", memberNumber: 222222);
        var commission = Builder.SeedAuthenticatedUser(chapterPermissions: new() {
            [chapter.Id] = [PermissionIdentifier.ManageAssemblyMotions]
        });
        var created = await CreateMotion(author, portal, type);
        await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");
        await Post(second, $"/api/assemblymotions/motions/{created.Id}/join");

        var asSubmitter = await GetDetail(second.Token, created.Id);
        var asCommission = await GetDetail(commission.Token, created.Id);

        await Assert.That(asSubmitter!.Submitters.Count).IsEqualTo(2);
        await Assert.That(asSubmitter.Submitters.All(s => s.MemberNumber == null)).IsTrue();
        await Assert.That(asCommission!.Submitters.Select(s => s.MemberNumber).ToArray())
            .IsEquivalentTo(new int?[] { 111111, 222222 });
    }

    [Test]
    public async Task Exited_author_can_withdraw() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 2);
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");
        Db.Members
            .Where(m => m.Id == author.Member.Id)
            .Set(m => m.ExitDate, DateTime.UtcNow.Date.AddDays(-1))
            .Update();

        var release = await Post(author, $"/api/assemblymotions/motions/{created.Id}/join");
        var withdraw = await Post(author, $"/api/assemblymotions/motions/{created.Id}/withdraw");
        var detail = await withdraw.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();

        await Assert.That(release.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(withdraw.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(detail!.Status).IsEqualTo(AssemblyMotionStatus.Withdrawn);
    }

    [Test]
    public async Task Mine_lists_authored_and_cosubmitted() {
        var (chapter, portal, type) = SeedPortal(minSubmitters: 2);
        var anna = SeedParticipant(chapter, "Anna", "Autor");
        var bert = SeedParticipant(chapter, "Bert", "Zweiter");
        var own = await CreateMotion(anna, portal, type, "Eigener");
        var foreign = await CreateMotion(bert, portal, type, "Fremder");
        await CreateMotion(bert, portal, type, "Nicht beteiligt");
        await Post(bert, $"/api/assemblymotions/motions/{foreign.Id}/release");
        await Post(anna, $"/api/assemblymotions/motions/{foreign.Id}/join");

        using var client = await AuthenticatedClientWithCsrfAsync(anna.Token);
        var items = await client.GetFromJsonAsync<AssemblyMotionMineItemDTO[]>($"/api/assemblymotions/portals/{portal.Id}/mine");

        await Assert.That(items!.Select(i => i.Title).ToArray()).IsEquivalentTo(new[] { "Eigener", "Fremder" });
        await Assert.That(items.Single(i => i.Id == own.Id).IsAuthor).IsTrue();
        await Assert.That(items.Single(i => i.Id == foreign.Id).IsAuthor).IsFalse();
        await Assert.That(items.Single(i => i.Id == foreign.Id).DisplayNumber).IsEqualTo("SÄA001");
    }

    [Test]
    public async Task Update_and_delete_draft_by_author() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);

        var request = SaveRequest(type, "Neu");
        request.BaseRevision = created.Revision;
        var updated = await client.PutAsJsonAsync($"/api/assemblymotions/motions/{created.Id}", request);
        var updatedDetail = await updated.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
        var deleted = await client.DeleteAsync($"/api/assemblymotions/motions/{created.Id}");
        var afterDelete = await client.GetAsync($"/api/assemblymotions/motions/{created.Id}");

        await Assert.That(updated.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(updatedDetail!.Title).IsEqualTo("Neu");
        await Assert.That(updatedDetail.Revision).IsEqualTo(2);
        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
        await Assert.That(afterDelete.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Exited_author_can_delete_draft() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        Db.Members
            .Where(m => m.Id == author.Member.Id)
            .Set(m => m.ExitDate, DateTime.UtcNow.Date.AddDays(-1))
            .Update();
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);

        var deleted = await client.DeleteAsync($"/api/assemblymotions/motions/{created.Id}");

        await Assert.That(deleted.StatusCode).IsEqualTo(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task Create_without_text_returns_400() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        using var client = await AuthenticatedClientWithCsrfAsync(author.Token);

        var response = await client.PostAsJsonAsync($"/api/assemblymotions/portals/{portal.Id}/motions", new {
            TypeId = type.Id,
            Title = "Antrag",
            ReasonMarkdown = "Grund"
        });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Context_reports_eligibility_and_commission() {
        var (chapter, portal, _) = SeedPortal();
        var member = SeedParticipant(chapter, "Anna", "Autor");
        var outsider = SeedParticipant(Builder.SeedChapter("Foreign"), "Out", "Sider");
        using var anonymous = AnonymousClient();
        using var memberClient = await AuthenticatedClientWithCsrfAsync(member.Token);
        using var outsiderClient = await AuthenticatedClientWithCsrfAsync(outsider.Token);

        var anonymousContext = await anonymous.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{portal.Id}/context");
        var memberContext = await memberClient.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{portal.Id}/context");
        var outsiderContext = await outsiderClient.GetFromJsonAsync<AssemblyMotionPortalContextDTO>($"/api/assemblymotions/portals/{portal.Id}/context");

        await Assert.That(anonymousContext!.IsSignedIn).IsFalse();
        await Assert.That(anonymousContext.Problem).IsEqualTo(EligibilityProblem.NotSignedIn);
        await Assert.That(memberContext!.IsEligible).IsTrue();
        await Assert.That(memberContext.IsCommission).IsFalse();
        await Assert.That(outsiderContext!.IsEligible).IsFalse();
        await Assert.That(outsiderContext.Problem).IsEqualTo(EligibilityProblem.OutsideChapter);
    }

    [Test]
    public async Task Revisions_hidden_from_anonymous() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var created = await CreateMotion(author, portal, type);
        await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");
        using var anonymous = AnonymousClient();

        var response = await anonymous.GetAsync($"/api/assemblymotions/motions/{created.Id}/revisions");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Revisions_visible_to_eligible_member() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bert", "Zweiter");
        var outsider = SeedParticipant(Builder.SeedChapter("Foreign"), "Out", "Sider");
        var created = await CreateMotion(author, portal, type);
        await Post(author, $"/api/assemblymotions/motions/{created.Id}/release");
        using var otherClient = await AuthenticatedClientWithCsrfAsync(other.Token);
        using var outsiderClient = await AuthenticatedClientWithCsrfAsync(outsider.Token);

        var response = await otherClient.GetAsync($"/api/assemblymotions/motions/{created.Id}/revisions");
        var revisions = await response.Content.ReadFromJsonAsync<List<AssemblyMotionRevisionDTO>>();
        var denied = await outsiderClient.GetAsync($"/api/assemblymotions/motions/{created.Id}/revisions");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(revisions!.Select(r => r.Revision).ToArray()).IsEquivalentTo(new[] { 1 });
        await Assert.That(denied.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Revisions_of_draft_only_for_author() {
        var (chapter, portal, type) = SeedPortal();
        var author = SeedParticipant(chapter, "Anna", "Autor");
        var other = SeedParticipant(chapter, "Bert", "Zweiter");
        var created = await CreateMotion(author, portal, type);
        using var authorClient = await AuthenticatedClientWithCsrfAsync(author.Token);
        using var otherClient = await AuthenticatedClientWithCsrfAsync(other.Token);

        var own = await authorClient.GetAsync($"/api/assemblymotions/motions/{created.Id}/revisions");
        var foreign = await otherClient.GetAsync($"/api/assemblymotions/motions/{created.Id}/revisions");

        await Assert.That(own.StatusCode).IsEqualTo(HttpStatusCode.OK);
        await Assert.That(foreign.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
