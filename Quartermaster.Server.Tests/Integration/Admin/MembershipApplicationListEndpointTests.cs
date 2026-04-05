using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class MembershipApplicationListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/admin/membershipapplications");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/membershipapplications");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Global_viewer_sees_all_applications() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMembershipApplication(chapter.Id, "Alice", "A");
        Builder.SeedMembershipApplication(chapter.Id, "Bob", "B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/membershipapplications");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<MembershipApplicationListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task Status_filter_limits_results() {
        var chapter = Builder.SeedChapter("C");
        Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Pending);
        Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Approved);
        Builder.SeedMembershipApplication(chapter.Id, status: ApplicationStatus.Rejected);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/membershipapplications?Status={(int)ApplicationStatus.Pending}");
        var result = await response.Content.ReadFromJsonAsync<MembershipApplicationListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
    }

    [Test]
    public async Task Chapter_viewer_only_sees_applications_from_permitted_chapters() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        Builder.SeedMembershipApplication(chapterA.Id, "X", "A");
        Builder.SeedMembershipApplication(chapterB.Id, "Y", "B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewApplications } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/membershipapplications");
        var result = await response.Content.ReadFromJsonAsync<MembershipApplicationListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
        await Assert.That(result.Items[0].FirstName).IsEqualTo("X");
    }

    [Test]
    public async Task Page_size_over_100_rejected() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewApplications });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/membershipapplications?PageSize=500");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}
