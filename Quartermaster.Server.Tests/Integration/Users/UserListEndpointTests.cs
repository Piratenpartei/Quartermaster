using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class UserListEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_list_including_caller_when_authorized() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        Builder.SeedUser(firstName: "Alice", lastName: "Anderson");
        Builder.SeedUser(firstName: "Bob", lastName: "Brown");
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserListItem>>();
        await Assert.That(users).IsNotNull();
        // caller + 2 = 3
        await Assert.That(users!.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Excludes_soft_deleted_users() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var deleted = Builder.SeedUser(firstName: "Deleted", lastName: "User");
        Db.Users.Where(u => u.Id == deleted.Id).Set(u => u.DeletedAt, System.DateTime.UtcNow).Update();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<UserListItem>>();
        await Assert.That(users!.Any(u => u.Id == deleted.Id)).IsFalse();
    }

    [Test]
    public async Task Returns_users_sorted_by_last_name_then_first_name() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        Builder.SeedUser(firstName: "Zed", lastName: "Zappa");
        Builder.SeedUser(firstName: "Alice", lastName: "Adams");
        Builder.SeedUser(firstName: "Bob", lastName: "Adams");
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users");
        var users = await response.Content.ReadFromJsonAsync<List<UserListItem>>();
        // Adams Alice < Adams Bob < (caller: Test User) < Zappa Zed
        await Assert.That(users![0].LastName).IsEqualTo("Adams");
        await Assert.That(users[0].FirstName).IsEqualTo("Alice");
        await Assert.That(users[1].LastName).IsEqualTo("Adams");
        await Assert.That(users[1].FirstName).IsEqualTo("Bob");
    }
}
