using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_motion_by_id() {
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id, title: "Detail Motion", text: "Some text");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MotionDetailDTO>();
        await Assert.That(dto!.Id).IsEqualTo(motion.Id);
        await Assert.That(dto.Title).IsEqualTo("Detail Motion");
        await Assert.That(dto.ChapterName).IsEqualTo("Chapter");
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Includes_votes_list() {
        var chapter = Builder.SeedChapter("Chapter");
        var member = Builder.SeedMember(chapter.Id, firstName: "Voter", lastName: "One");
        var caster = Builder.SeedUser();
        var motion = Builder.SeedMotion(chapter.Id);
        Builder.SeedMotionVote(motion.Id, member.Id, caster.Id, VoteType.Approve);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        var dto = await response.Content.ReadFromJsonAsync<MotionDetailDTO>();
        await Assert.That(dto!.Votes.Count).IsEqualTo(1);
        await Assert.That(dto.Votes[0].Vote).IsEqualTo(VoteType.Approve);
    }

    [Test]
    public async Task Includes_officers_list_with_total_count() {
        var chapter = Builder.SeedChapter("Chapter");
        var member1 = Builder.SeedMember(chapter.Id, firstName: "Off", lastName: "One");
        var member2 = Builder.SeedMember(chapter.Id, firstName: "Off", lastName: "Two");
        Builder.SeedChapterOfficer(member1.Id, chapter.Id);
        Builder.SeedChapterOfficer(member2.Id, chapter.Id);
        var motion = Builder.SeedMotion(chapter.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        var dto = await response.Content.ReadFromJsonAsync<MotionDetailDTO>();
        await Assert.That(dto!.TotalOfficers).IsEqualTo(2);
        await Assert.That(dto.Officers.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_votes_and_officers_when_none() {
        var chapter = Builder.SeedChapter("Chapter");
        var motion = Builder.SeedMotion(chapter.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        var dto = await response.Content.ReadFromJsonAsync<MotionDetailDTO>();
        await Assert.That(dto!.Votes.Count).IsEqualTo(0);
        await Assert.That(dto.Officers.Count).IsEqualTo(0);
        await Assert.That(dto.TotalOfficers).IsEqualTo(0);
    }

    [Test]
    public async Task Public_motion_hides_on_behalf_from_anonymous() {
        var (motion, _) = SeedHandedOverMotion();
        using var client = AnonymousClient();

        var dto = await client.GetFromJsonAsync<MotionDetailDTO>($"/api/motions/{motion.Id}");

        await Assert.That(dto!.OnBehalfOfChapterId).IsNull();
        await Assert.That(dto.OnBehalfOfChapterName).IsNull();
        await Assert.That(dto.OnBehalfOfReason).IsNull();
        await Assert.That(dto.HandedOverToMotionId).IsNull();
        await Assert.That(dto.HandedOverToChapterName).IsNull();
    }

    [Test]
    public async Task On_behalf_shown_to_member() {
        var (motion, target) = SeedHandedOverMotion();
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);

        var dto = await client.GetFromJsonAsync<MotionDetailDTO>($"/api/motions/{motion.Id}");
        var list = await client.GetFromJsonAsync<MotionListResponse>("/api/motions?page=1&pageSize=25");

        await Assert.That(dto!.OnBehalfOfChapterName).IsEqualTo("OV");
        await Assert.That(dto.OnBehalfOfReason).IsEqualTo(ChapterMark.UnableToAct);
        await Assert.That(dto.HandedOverToMotionId).IsEqualTo(target.Id);
        await Assert.That(dto.HandedOverToChapterName).IsEqualTo("KV");
        var listed = list!.Items.Single(m => m.Id == motion.Id);
        await Assert.That(listed.OnBehalfOfChapterName).IsEqualTo("OV");
        await Assert.That(listed.HandedOverToChapterName).IsEqualTo("KV");
        await Assert.That(list.Items.Single(m => m.Id == target.Id).OnBehalfOfReason).IsEqualTo(ChapterMark.UnableToAct);
    }

    private (Motion Closed, Motion Replacement) SeedHandedOverMotion() {
        var tree = Builder.SeedChapterHierarchy("Bund", "KV", "OV");
        var closed = Builder.SeedMotion(tree[2].Id, title: "Alt");
        var replacement = Builder.SeedMotion(tree[1].Id, title: "Neu");
        Db.Motions.Where(m => m.Id == replacement.Id)
            .Set(m => m.OnBehalfOfChapterId, tree[2].Id)
            .Set(m => m.OnBehalfOfReason, ChapterMark.UnableToAct)
            .Update();
        Db.Motions.Where(m => m.Id == closed.Id)
            .Set(m => m.OnBehalfOfChapterId, tree[2].Id)
            .Set(m => m.OnBehalfOfReason, ChapterMark.UnableToAct)
            .Set(m => m.HandedOverToMotionId, replacement.Id)
            .Update();
        return (closed, replacement);
    }
}
