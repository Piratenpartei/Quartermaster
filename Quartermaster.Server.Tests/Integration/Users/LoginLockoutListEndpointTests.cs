using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Users;
using Quartermaster.Data.Users;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Integration.Users;

public class LoginLockoutListEndpointTests : IntegrationTestBase {
    private void SeedFailedAttempts(string ip, string identifier, int count) {
        for (var i = 0; i < count; i++) {
            Db.Insert(new LoginAttempt {
                Id = Guid.NewGuid(),
                IpAddress = ip,
                UsernameOrEmail = identifier,
                Success = false,
                AttemptedAt = DateTime.UtcNow.AddMinutes(-1)
            });
        }
    }

    [Test]
    public async Task Returns_401_when_anonymous() {
        using var client = AnonymousClient();
        var response = await client.GetAsync("/api/users/lockouts");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Returns_403_when_user_lacks_view_users_permission() {
        var (_, token) = Builder.SeedAuthenticatedUser();
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/lockouts");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Returns_empty_list_when_no_lockouts() {
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/lockouts");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<LoginLockoutListResponse>();
        await Assert.That(dto).IsNotNull();
        await Assert.That(dto!.Items.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Returns_locked_out_entries() {
        SeedFailedAttempts("10.0.0.1", "victim@example.com", 5);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/lockouts");
        var dto = await response.Content.ReadFromJsonAsync<LoginLockoutListResponse>();
        await Assert.That(dto!.Items.Count).IsEqualTo(1);
        await Assert.That(dto.Items[0].IpAddress).IsEqualTo("10.0.0.1");
        await Assert.That(dto.Items[0].UsernameOrEmail).IsEqualTo("victim@example.com");
        await Assert.That(dto.Items[0].FailedAttempts).IsEqualTo(5);
    }

    [Test]
    public async Task Ignores_entries_below_lockout_threshold() {
        // Only 3 failures — below the default 5 threshold
        SeedFailedAttempts("10.0.0.2", "minor@example.com", 3);
        var (_, token) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers });
        using var client = AuthenticatedClient(token);
        var response = await client.GetAsync("/api/users/lockouts");
        var dto = await response.Content.ReadFromJsonAsync<LoginLockoutListResponse>();
        await Assert.That(dto!.Items.Count).IsEqualTo(0);
    }
}
