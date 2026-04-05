using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Server.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class UserDetailEndpointTests : IntegrationTestBase {
    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        var other = Builder.SeedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{other.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_404_for_nonexistent_id() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{Guid.NewGuid()}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Returns_user_when_authorized() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser(firstName: "Jane", lastName: "Doe", username: "jdoe");
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{target.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<UserDetailResponse>();
        await Assert.That(dto!.Id).IsEqualTo(target.Id);
        await Assert.That(dto.FirstName).IsEqualTo("Jane");
        await Assert.That(dto.LastName).IsEqualTo("Doe");
        await Assert.That(dto.Username).IsEqualTo("jdoe");
    }

    [Test]
    public async Task Returns_404_for_soft_deleted_user() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        var target = Builder.SeedUser();
        Db.Users.Where(u => u.Id == target.Id).Set(u => u.DeletedAt, (DateTime?)DateTime.UtcNow).Update();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/users/{target.Id}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }
}
