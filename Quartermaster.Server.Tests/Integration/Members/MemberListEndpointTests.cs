using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

public class MemberListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/members");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_has_no_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Global_view_all_members_sees_all() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedMember(chapterA.Id, firstName: "Alice", lastName: "Alpha");
        Builder.SeedMember(chapterB.Id, firstName: "Bob", lastName: "Beta");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MemberSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task Chapter_scoped_user_sees_only_permitted_chapters() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedMember(chapterA.Id, firstName: "Alice", lastName: "Alpha");
        Builder.SeedMember(chapterB.Id, firstName: "Bob", lastName: "Beta");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewMembers } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members");
        var result = await response.Content.ReadFromJsonAsync<MemberSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].FirstName).IsEqualTo("Alice");
    }

    [Test]
    public async Task Search_query_filters_by_name() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMember(chapter.Id, firstName: "Alice", lastName: "Alpha");
        Builder.SeedMember(chapter.Id, firstName: "Bob", lastName: "Beta");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members?Query=Alice");
        var result = await response.Content.ReadFromJsonAsync<MemberSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].FirstName).IsEqualTo("Alice");
    }

    [Test]
    public async Task Search_query_filters_by_member_number() {
        var chapter = Builder.SeedChapter("Chapter");
        Builder.SeedMember(chapter.Id, memberNumber: 12345, firstName: "Needle");
        Builder.SeedMember(chapter.Id, memberNumber: 67890, firstName: "Haystack");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members?Query=12345");
        var result = await response.Content.ReadFromJsonAsync<MemberSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].FirstName).IsEqualTo("Needle");
    }

    [Test]
    public async Task Orphaned_only_filters_to_orphaned_divisions() {
        var chapter = Builder.SeedChapter("Chapter");
        var orphanDiv = Builder.SeedAdminDivision("Orphan", isOrphaned: true);
        var goodDiv = Builder.SeedAdminDivision("Good", isOrphaned: false);
        var orphan = Builder.SeedMember(chapter.Id, firstName: "Orphan", lastName: "Member");
        var normal = Builder.SeedMember(chapter.Id, firstName: "Normal", lastName: "Member");
        Db.Members.Where(m => m.Id == orphan.Id)
            .Set(m => m.ResidenceAdministrativeDivisionId, orphanDiv.Id).Update();
        Db.Members.Where(m => m.Id == normal.Id)
            .Set(m => m.ResidenceAdministrativeDivisionId, goodDiv.Id).Update();
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members?OrphanedOnly=true");
        var result = await response.Content.ReadFromJsonAsync<MemberSearchResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].FirstName).IsEqualTo("Orphan");
    }

    [Test]
    public async Task Rejects_page_size_over_100() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members?Page=1&PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
