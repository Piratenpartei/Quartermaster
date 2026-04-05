using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Quartermaster.Api;
using Quartermaster.Api.Roles;
using Quartermaster.Data.Roles;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Roles;

public class RoleAssignmentListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/roleassignments");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_manage_roles() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roleassignments");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_assignments() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roleassignments");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<UserRoleAssignmentDTO>>();
        await Assert.That(list).IsNotNull();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_global_role_assignment() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        var role = Builder.SeedRole("custom", "CustomRole", RoleScope.Global);
        var target = Builder.SeedUser(firstName: "Tim", lastName: "Target");
        Builder.AssignRoleToUser(target.Id, role.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roleassignments");
        var list = await response.Content.ReadFromJsonAsync<List<UserRoleAssignmentDTO>>();
        await Assert.That(list!.Count).IsEqualTo(1);
        await Assert.That(list[0].RoleName).IsEqualTo("CustomRole");
        await Assert.That(list[0].UserDisplayName).IsEqualTo("Tim Target");
        await Assert.That(list[0].ChapterId).IsNull();
    }

    [Test]
    public async Task Returns_chapter_role_assignment_with_chapter_name() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ManageRoles });
        var chapter = Builder.SeedChapter("MyChapter");
        var role = Builder.SeedRole("cr", "ChRole", RoleScope.ChapterScoped);
        var target = Builder.SeedUser(firstName: "X", lastName: "Y");
        Builder.AssignRoleToUser(target.Id, role.Id, chapter.Id);
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/roleassignments");
        var list = await response.Content.ReadFromJsonAsync<List<UserRoleAssignmentDTO>>();
        var chAssignment = list!.First(a => a.RoleName == "ChRole");
        await Assert.That(chAssignment.ChapterId).IsEqualTo(chapter.Id);
        await Assert.That(chAssignment.ChapterName).IsEqualTo("MyChapter");
    }
}
