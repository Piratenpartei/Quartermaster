using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Motions;

public class MotionListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Anonymous_receives_public_motions() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Public Motion");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/motions");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Public Motion");
    }

    [Test]
    public async Task Excludes_non_public_by_default() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Public");
        var nonPublic = Builder.SeedMotion(chapter.Id, title: "Hidden");
        Db.Motions.Where(m => m.Id == nonPublic.Id).Set(m => m.IsPublic, false).Update();
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/motions");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Public");
    }

    [Test]
    public async Task Includes_non_public_when_flag_set() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Public");
        var nonPublic = Builder.SeedMotion(chapter.Id, title: "Hidden");
        Db.Motions.Where(m => m.Id == nonPublic.Id).Set(m => m.IsPublic, false).Update();
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/motions?IncludeNonPublic=true");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Filters_by_chapter_id() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedMotion(chapterA.Id, title: "A motion");
        Builder.SeedMotion(chapterB.Id, title: "B motion");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions?ChapterId={chapterA.Id}");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("A motion");
    }

    [Test]
    public async Task Filters_by_approval_status() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMotion(chapter.Id, title: "Pending", status: MotionApprovalStatus.Pending);
        Builder.SeedMotion(chapter.Id, title: "Approved", status: MotionApprovalStatus.Approved);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/motions?ApprovalStatus={(int)MotionApprovalStatus.Approved}");
        var result = await response.Content.ReadFromJsonAsync<MotionListResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Title).IsEqualTo("Approved");
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/motions?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
