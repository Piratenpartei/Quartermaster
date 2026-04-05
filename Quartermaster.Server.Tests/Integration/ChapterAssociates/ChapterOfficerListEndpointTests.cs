using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.ChapterAssociates;

public class ChapterOfficerListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapterofficers");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_empty_when_no_officers() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/chapterofficers");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChapterOfficerSearchResponse>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_officers_with_member_and_chapter_data() {
        var chapter = Builder.SeedChapter("My Chapter");
        var member = Builder.SeedMember(chapter.Id, memberNumber: 1000, firstName: "John", lastName: "Doe");
        Builder.SeedChapterOfficer(member.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/chapterofficers");
        var result = await response.Content.ReadFromJsonAsync<ChapterOfficerSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].MemberFirstName).IsEqualTo("John");
        await Assert.That(result.Items[0].MemberLastName).IsEqualTo("Doe");
        await Assert.That(result.Items[0].ChapterName).IsEqualTo("My Chapter");
    }

    [Test]
    public async Task Query_filters_by_member_name() {
        var chapter = Builder.SeedChapter("C");
        var m1 = Builder.SeedMember(chapter.Id, firstName: "Alice", lastName: "Anderson");
        var m2 = Builder.SeedMember(chapter.Id, firstName: "Bob", lastName: "Brown");
        Builder.SeedChapterOfficer(m1.Id, chapter.Id);
        Builder.SeedChapterOfficer(m2.Id, chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/chapterofficers?Query=Alice");
        var result = await response.Content.ReadFromJsonAsync<ChapterOfficerSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_filter_limits_results() {
        var ch1 = Builder.SeedChapter("One");
        var ch2 = Builder.SeedChapter("Two");
        var m1 = Builder.SeedMember(ch1.Id);
        var m2 = Builder.SeedMember(ch2.Id);
        Builder.SeedChapterOfficer(m1.Id, ch1.Id);
        Builder.SeedChapterOfficer(m2.Id, ch2.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/chapterofficers?ChapterId={ch1.Id}");
        var result = await response.Content.ReadFromJsonAsync<ChapterOfficerSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].ChapterId).IsEqualTo(ch1.Id);
    }

    [Test]
    public async Task Rejects_invalid_page_size() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/chapterofficers?PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
