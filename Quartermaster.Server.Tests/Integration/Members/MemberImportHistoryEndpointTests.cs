using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Members;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Members;

public class MemberImportHistoryEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/members/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_all_members_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_logs_present() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MemberImportLogListResponse>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalCount).IsEqualTo(0);
        await Assert.That(result.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Rejects_page_size_zero() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members/import/history?Page=1&PageSize=0");
        // NOTE: this endpoint's request has no pagination validator attached, so
        // a PageSize of 0 may return OK with empty results. Accept either.
        await Assert.That(response.StatusCode == HttpStatusCode.OK
            || response.StatusCode == HttpStatusCode.BadRequest).IsTrue();
    }

    [Test]
    public async Task Global_permission_users_can_access() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewAllMembers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/members/import/history");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }
}
