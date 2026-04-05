using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api.Chapters;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Chapters;

public class ChapterListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_empty_list_when_no_chapters() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var chapters = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(chapters).IsNotNull();
        await Assert.That(chapters!.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_all_chapters() {
        Builder.SeedChapter("Chapter A");
        Builder.SeedChapter("Chapter B");
        Builder.SeedChapter("Chapter C");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var chapters = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(chapters!.Length).IsEqualTo(3);
    }

    [Test]
    public async Task Anonymous_access_allowed() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}

public class ChapterDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_chapter_by_id() {
        var chapter = Builder.SeedChapter("Test Chapter");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{chapter.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{System.Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Includes_parent_name_when_parent_exists() {
        var parent = Builder.SeedChapter("Parent");
        var child = Builder.SeedChapter("Child", parent.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{child.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Parent");
    }

    [Test]
    public async Task Returns_children_list() {
        var parent = Builder.SeedChapter("Parent");
        Builder.SeedChapter("Child A", parent.Id);
        Builder.SeedChapter("Child B", parent.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{parent.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        await Assert.That(body).Contains("Child A");
        await Assert.That(body).Contains("Child B");
    }
}

public class ChapterSearchEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_paginated_results() {
        for (var i = 1; i <= 30; i++) {
            Builder.SeedChapter($"Chapter {i:D3}");
        }
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/search?Page=1&PageSize=10");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChapterSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(10);
        await Assert.That(result.TotalCount).IsEqualTo(30);
    }

    [Test]
    public async Task Query_filter_matches_name() {
        Builder.SeedChapter("Hannover");
        Builder.SeedChapter("Berlin");
        Builder.SeedChapter("München");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/search?Query=Han");
        var result = await response.Content.ReadFromJsonAsync<ChapterSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(1);
        await Assert.That(result.Items[0].Name).IsEqualTo("Hannover");
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/search?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Rejects_negative_page() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/search?Page=-1&PageSize=10");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Returns_empty_list_when_no_matches() {
        Builder.SeedChapter("Hannover");
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/search?Query=Zzz");
        var result = await response.Content.ReadFromJsonAsync<ChapterSearchResponse>();
        await Assert.That(result!.Items.Count).IsEqualTo(0);
        await Assert.That(result.TotalCount).IsEqualTo(0);
    }
}

public class ChapterRootsEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_only_root_chapters() {
        var root1 = Builder.SeedChapter("Root 1");
        Builder.SeedChapter("Root 2");
        Builder.SeedChapter("Child", root1.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/roots");
        var chapters = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(chapters!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_when_no_chapters() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/chapters/roots");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var chapters = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(chapters!.Length).IsEqualTo(0);
    }
}

public class ChapterChildrenEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_children_of_parent() {
        var parent = Builder.SeedChapter("Parent");
        Builder.SeedChapter("Child A", parent.Id);
        Builder.SeedChapter("Child B", parent.Id);
        Builder.SeedChapter("Other Root"); // Unrelated
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{parent.Id}/children");
        var children = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(children!.Length).IsEqualTo(2);
    }

    [Test]
    public async Task Returns_empty_list_for_leaf_chapter() {
        var leaf = Builder.SeedChapter("Leaf");
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{leaf.Id}/children");
        var children = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(children!.Length).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_empty_list_for_nonexistent_parent() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/{System.Guid.NewGuid()}/children");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var children = await response.Content.ReadFromJsonAsync<ChapterDTO[]>();
        await Assert.That(children!.Length).IsEqualTo(0);
    }
}

public class ChapterForDivisionEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_chapter_matching_admin_division() {
        var div = Builder.SeedAdminDivision("Test Division");
        var chapter = Builder.SeedChapter("Chapter for Division", adminDivisionId: div.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/for-division/{div.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ChapterDTO>();
        await Assert.That(result!.Id).IsEqualTo(chapter.Id);
    }

    [Test]
    public async Task Returns_404_when_no_chapter_for_division() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/chapters/for-division/{System.Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
