using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Admin;

public class DueSelectionDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        var due = Builder.SeedDueSelection();
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/admin/dueselections/{due.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_selection() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_permission_and_no_linked_app() {
        var due = Builder.SeedDueSelection();
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections/{due.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_selection_for_global_viewer() {
        var due = Builder.SeedDueSelection("Alice", "Anderson");
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewDueSelections });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections/{due.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<DueSelectionDetailDTO>();
        await Assert.That(dto!.FirstName).IsEqualTo("Alice");
        await Assert.That(dto.LastName).IsEqualTo("Anderson");
    }

    [Test]
    public async Task Chapter_viewer_can_see_selection_linked_to_permitted_chapter() {
        var chapter = Builder.SeedChapter("C");
        var due = Builder.SeedDueSelection();
        var app = Builder.SeedMembershipApplication(chapter.Id);
        Db.MembershipApplications.Where(a => a.Id == app.Id)
            .Set(a => a.DueSelectionId, due.Id)
            .Update();

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewDueSelections } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections/{due.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Chapter_viewer_denied_for_selection_in_other_chapter() {
        var chapterA = Builder.SeedChapter("A");
        var chapterB = Builder.SeedChapter("B");
        var due = Builder.SeedDueSelection();
        var app = Builder.SeedMembershipApplication(chapterB.Id);
        Db.MembershipApplications.Where(a => a.Id == app.Id)
            .Set(a => a.DueSelectionId, due.Id)
            .Update();

        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapterA.Id] = new[] { PermissionIdentifier.ViewDueSelections } });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/admin/dueselections/{due.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }
}
