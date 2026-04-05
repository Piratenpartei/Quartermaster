using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class MembershipApplicationDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/admin/membershipapplications/{app.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_application() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_permission() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications/{app.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_application_detail_for_global_viewer() {
        var chapter = Builder.SeedChapter("Test Chapter");
        var app = Builder.SeedMembershipApplication(chapter.Id, "Alice", "Anderson");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications/{app.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MembershipApplicationDetailDTO>();
        await Assert.That(dto!.FirstName).IsEqualTo("Alice");
        await Assert.That(dto.LastName).IsEqualTo("Anderson");
        await Assert.That(dto.ChapterName).IsEqualTo("Test Chapter");
    }

    [Test]
    public async Task Chapter_viewer_can_see_application_from_permitted_chapter() {
        var chapter = Builder.SeedChapter("C");
        var app = Builder.SeedMembershipApplication(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewApplications } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications/{app.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Chapter_viewer_denied_for_other_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var app = Builder.SeedMembershipApplication(chapterB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewApplications } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications/{app.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
