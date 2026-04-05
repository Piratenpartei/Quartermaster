using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Data.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class DueSelectionListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/admin/dueselections");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/dueselections");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_for_global_viewer_when_no_selections() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/dueselections");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DueSelectionListResponse>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.TotalCount).IsEqualTo(0);
    }

    [Test]
    public async Task Global_viewer_sees_all_selections() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        Builder.SeedDueSelection("Alice", "Anderson");
        Builder.SeedDueSelection("Bob", "Brown");
        Builder.SeedDueSelection("Carol", "Cook");
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/dueselections");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DueSelectionListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(3);
    }

    [Test]
    public async Task Status_filter_limits_results() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        Builder.SeedDueSelection("A", "A", status: DueSelectionStatus.Pending);
        Builder.SeedDueSelection("B", "B", status: DueSelectionStatus.Approved);
        Builder.SeedDueSelection("C", "C", status: DueSelectionStatus.Pending);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections?Status={(int)DueSelectionStatus.Pending}");
        var result = await response.Content.ReadFromJsonAsync<DueSelectionListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(2);
    }

    [Test]
    public async Task Chapter_viewer_only_sees_selections_linked_to_permitted_chapter() {
        var chapterA = Builder.SeedChapter("Chapter A");
        var chapterB = Builder.SeedChapter("Chapter B");
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewDueSelections } });

        var dueA = Builder.SeedDueSelection("Alice", "A");
        var dueB = Builder.SeedDueSelection("Bob", "B");
        Builder.SeedDueSelection("Orphan", "O");

        var appA = Builder.SeedMembershipApplication(chapterA.Id);
        var appB = Builder.SeedMembershipApplication(chapterB.Id);
        Db.MembershipApplications.Where(a => a.Id == appA.Id)
            .Set(a => a.DueSelectionId, dueA.Id)
            .Update();
        Db.MembershipApplications.Where(a => a.Id == appB.Id)
            .Set(a => a.DueSelectionId, dueB.Id)
            .Update();

        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/admin/dueselections");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DueSelectionListResponse>();
        await Assert.That(result!.TotalCount).IsEqualTo(1);
    }
}
