using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
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
        var user = Builder.SeedUser(firstName: "Voter", lastName: "One");
        var motion = Builder.SeedMotion(chapter.Id);
        Builder.SeedMotionVote(motion.Id, user.Id, VoteType.Approve);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions/{motion.Id}");
        var dto = await response.Content.ReadFromJsonAsync<MotionDetailDTO>();
        await Assert.That(dto!.Votes.Count).IsEqualTo(1);
        await Assert.That(dto.Votes[0].Vote).IsEqualTo((int)VoteType.Approve);
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
}
