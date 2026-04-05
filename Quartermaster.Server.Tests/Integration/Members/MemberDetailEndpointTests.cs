using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

public class MemberDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/members/{member.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/members/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_has_no_permission() {
        var chapter = Builder.SeedChapter();
        var member = Builder.SeedMember(chapter.Id);
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/members/{member.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_403_for_member_outside_permitted_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var member = Builder.SeedMember(chapterB.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewMembers } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/members/{member.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_member_when_user_has_global_permission() {
        var chapter = Builder.SeedChapter("Chapter X");
        var member = Builder.SeedMember(chapter.Id, firstName: "Detail", lastName: "Tester");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/members/{member.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MemberDetailDTO>();
        await Assert.That(dto!.FirstName).IsEqualTo("Detail");
        await Assert.That(dto.ChapterName).IsEqualTo("Chapter X");
    }

    [Test]
    public async Task Returns_member_when_user_has_chapter_permission() {
        var chapter = Builder.SeedChapter("Chapter X");
        var member = Builder.SeedMember(chapter.Id, firstName: "Detail");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMembers } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/members/{member.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
