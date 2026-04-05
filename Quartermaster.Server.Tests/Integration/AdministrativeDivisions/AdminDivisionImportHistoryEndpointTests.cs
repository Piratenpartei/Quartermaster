using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.AdministrativeDivisions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.AdministrativeDivisions;

public class AdminDivisionImportHistoryEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/admindivisions/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_all_members_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admindivisions/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_logs() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admindivisions/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AdminDivisionImportLogListResponse>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_403_when_user_has_only_view_members_chapter_perm() {
        var chapter = Builder.SeedChapter();
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMembers } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admindivisions/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
